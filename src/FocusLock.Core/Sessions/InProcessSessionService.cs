using FocusLock.Core.Abstractions;
using FocusLock.Core.Blocking;
using FocusLock.Core.Settings;
using FocusLock.Core.Websites;
using Microsoft.Extensions.Logging;

namespace FocusLock.Core.Sessions;

/// <summary>Runs sessions inside the app: drives the engine from a one-second timer and turns blocking on during focus phases.</summary>
public sealed class InProcessSessionService : ISessionService, IDisposable
{
    public static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(1);

    private readonly FocusSessionEngine _engine;
    private readonly BlockingEngine _blocking;
    private readonly WebsiteBlocker _websites;
    private readonly BrowserCloser _browsers;
    private readonly ITickTimer _timer;
    private readonly IClock _clock;
    private readonly IStatsStore _stats;
    private readonly SettingsService _settings;
    private readonly ILogger<InProcessSessionService> _logger;
    private readonly object _gate = new();
    private IDisposable? _ticks;
    private BlockPolicy _policy = BlockPolicy.Empty;
    private int _blockedAttempts;
    private bool _committed;
    private bool _noticedBlocked;
    private bool _noticedDeclined;
    private bool _noticedFailed;
    private IReadOnlyList<string> _openBrowsers = [];

    public InProcessSessionService(
        FocusSessionEngine engine,
        BlockingEngine blocking,
        WebsiteBlocker websites,
        BrowserCloser browsers,
        ITickTimer timer,
        IClock clock,
        IStatsStore stats,
        SettingsService settings,
        ILogger<InProcessSessionService> logger)
    {
        _engine = engine;
        _blocking = blocking;
        _websites = websites;
        _browsers = browsers;
        _timer = timer;
        _clock = clock;
        _stats = stats;
        _settings = settings;
        _logger = logger;
        _websites.CallCompleted += OnWebsiteCallCompleted;
    }

    public event EventHandler<SessionState>? StateChanged;

    public event EventHandler<BlockedLaunchNotice>? LaunchBlocked;

    public event EventHandler<SessionSummary>? Completed;

    public event EventHandler<WebsiteNotice>? WebsiteNotice;

    public SessionState State { get; private set; } = SessionState.Idle;

    public Task StartAsync(SessionPlan plan, BlockPolicy policy, CancellationToken cancellationToken = default)
    {
        SessionState state;
        lock (_gate)
        {
            if (_engine.IsRunning)
            {
                throw new InvalidOperationException("A session is already running.");
            }

            var now = _clock.UtcNow;
            _engine.Start(plan, now);
            _policy = policy;
            _blockedAttempts = 0;
            _committed = plan.Committed;
            (_noticedBlocked, _noticedDeclined, _noticedFailed) = (false, false, false);
            _openBrowsers = [];
            _blocking.LaunchBlocked += OnLaunchBlocked;
            state = Apply(_engine.Advance(now));
            _ticks = _timer.Start(TickInterval, Tick);
        }

        _logger.LogInformation("Session started: {Plan}", plan);
        StateChanged?.Invoke(this, state);
        _blocking.Activate(policy, _settings.Current.CloseAlreadyRunningGames);
        if (policy.BlockedDomains.Count > 0)
        {
            var openBrowsers = HandleOpenBrowsers();
            lock (_gate)
            {
                _openBrowsers = openBrowsers;
            }
        }

        _websites.Activate(policy.BlockedDomains);
        return Task.CompletedTask;
    }

    public async Task EndEarlyAsync(CancellationToken cancellationToken = default)
    {
        SessionSummary? summary = null;
        lock (_gate)
        {
            if (_engine.IsRunning && !_committed)
            {
                summary = Finish(_clock.UtcNow, endedEarly: true);
            }
        }

        if (summary is not null)
        {
            RaiseCompleted(summary);

            // Quitting awaits this, so the app does not exit before the hosts file is cleaned.
            await _websites.WhenIdleAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _ticks?.Dispose();
            _ticks = null;
            _blocking.LaunchBlocked -= OnLaunchBlocked;
        }

        _websites.CallCompleted -= OnWebsiteCallCompleted;
    }

    private void Tick()
    {
        SessionState? state = null;
        SessionSummary? summary = null;
        var activateBlocking = false;
        var deactivateBlocking = false;

        lock (_gate)
        {
            if (!_engine.IsRunning)
            {
                return;
            }

            var now = _clock.UtcNow;
            var snapshot = _engine.Advance(now);
            if (snapshot.IsFinished)
            {
                summary = Finish(now, endedEarly: false);
            }
            else
            {
                if (snapshot.PhaseChanged)
                {
                    activateBlocking = snapshot.Phase == SessionPhase.Focus;
                    deactivateBlocking = !activateBlocking;
                }

                state = Apply(snapshot);
            }
        }

        if (activateBlocking)
        {
            _logger.LogInformation("Focus time again");
            _blocking.Activate(_policy, _settings.Current.CloseAlreadyRunningGames);
            _websites.Activate(_policy.BlockedDomains);
        }
        else if (deactivateBlocking)
        {
            _logger.LogInformation("Break time");
            _blocking.Deactivate();
            _websites.Deactivate();
        }

        if (state is not null)
        {
            StateChanged?.Invoke(this, state);
        }

        if (summary is not null)
        {
            RaiseCompleted(summary);
        }
    }

    private void OnLaunchBlocked(object? sender, LaunchBlockedEventArgs e)
    {
        SessionState state;
        lock (_gate)
        {
            if (!_engine.IsRunning)
            {
                return;
            }

            if (e.Result == TerminateResult.Terminated)
            {
                _blockedAttempts++;
            }

            state = Apply(_engine.Advance(_clock.UtcNow));
        }

        LaunchBlocked?.Invoke(this, new BlockedLaunchNotice(e.GameName, e.Result, state.RemainingInPhase));
        StateChanged?.Invoke(this, state);
    }

    /// <summary>
    /// Tabs that are already open keep working until the browser restarts, so close browsers, or return the open ones
    /// to name in the "Websites blocked" notice.
    /// </summary>
    private IReadOnlyList<string> HandleOpenBrowsers()
    {
        try
        {
            if (_settings.Current.CloseBrowsersOnSessionStart)
            {
                _browsers.CloseAll();
                return [];
            }

            return _browsers.RunningBrowsers();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Checking for open browsers failed");
            return [];
        }
    }

    // Runs on the website blocker's worker thread.
    private void OnWebsiteCallCompleted(object? sender, SiteBlockCall call)
    {
        WebsiteNoticeKind? kind = null;
        IReadOnlyList<string> browsers = [];
        lock (_gate)
        {
            if (call.IsClear)
            {
                if (call.Result is SiteBlockResult.UserCancelled or SiteBlockResult.Failed)
                {
                    kind = WebsiteNoticeKind.StillBlocked;
                }
            }
            else if (_engine.IsRunning)
            {
                // Once per session each, so every focus block doesn't repeat the same message.
                if (call.Result == SiteBlockResult.Applied && !_noticedBlocked)
                {
                    (_noticedBlocked, kind, browsers) = (true, WebsiteNoticeKind.Blocked, _openBrowsers);
                }
                else if (call.Result == SiteBlockResult.UserCancelled && !_noticedDeclined)
                {
                    (_noticedDeclined, kind) = (true, WebsiteNoticeKind.PermissionDeclined);
                }
                else if (call.Result == SiteBlockResult.Failed && !_noticedFailed)
                {
                    (_noticedFailed, kind) = (true, WebsiteNoticeKind.Failed);
                }
            }
        }

        if (kind is { } noticeKind)
        {
            WebsiteNotice?.Invoke(this, new WebsiteNotice(noticeKind, browsers));
        }
    }

    // Caller holds _gate.
    private SessionSummary Finish(DateTimeOffset now, bool endedEarly)
    {
        _ticks?.Dispose();
        _ticks = null;
        var startedAt = _engine.StartedAt;
        var snapshot = _engine.Stop(now);
        _blocking.LaunchBlocked -= OnLaunchBlocked;
        State = SessionState.Idle;
        return new SessionSummary(snapshot.FocusedTime, _blockedAttempts, endedEarly, startedAt, now);
    }

    private void RaiseCompleted(SessionSummary summary)
    {
        _blocking.Deactivate();
        _websites.Deactivate();
        try
        {
            _stats.Append(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Saving the session summary failed");
        }

        _logger.LogInformation(
            "Session {Outcome}: focused {Focused}, blocked {Blocked} launches",
            summary.EndedEarly ? "ended early" : "completed",
            summary.FocusedTime,
            summary.BlockedAttempts);
        StateChanged?.Invoke(this, SessionState.Idle);
        Completed?.Invoke(this, summary);
    }

    // Caller holds _gate.
    private SessionState Apply(EngineSnapshot snapshot)
    {
        State = new SessionState(
            IsRunning: true,
            snapshot.Phase,
            snapshot.Cycle,
            snapshot.TotalCycles,
            snapshot.RemainingInPhase,
            snapshot.RemainingTotal,
            snapshot.PhaseProgress,
            _blockedAttempts,
            CanEndEarly: !_committed);
        return State;
    }
}
