using FocusLock.Core.Abstractions;
using FocusLock.Core.Blocking;
using FocusLock.Core.Settings;
using Microsoft.Extensions.Logging;

namespace FocusLock.Core.Sessions;

/// <summary>Runs sessions inside the app: drives the engine from a one-second timer and turns blocking on during focus phases.</summary>
public sealed class InProcessSessionService : ISessionService, IDisposable
{
    public static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(1);

    private readonly FocusSessionEngine _engine;
    private readonly BlockingEngine _blocking;
    private readonly ITickTimer _timer;
    private readonly IClock _clock;
    private readonly IStatsStore _stats;
    private readonly SettingsService _settings;
    private readonly ILogger<InProcessSessionService> _logger;
    private readonly object _gate = new();
    private IDisposable? _ticks;
    private BlockPolicy _policy = BlockPolicy.Empty;
    private int _blockedAttempts;

    public InProcessSessionService(
        FocusSessionEngine engine,
        BlockingEngine blocking,
        ITickTimer timer,
        IClock clock,
        IStatsStore stats,
        SettingsService settings,
        ILogger<InProcessSessionService> logger)
    {
        _engine = engine;
        _blocking = blocking;
        _timer = timer;
        _clock = clock;
        _stats = stats;
        _settings = settings;
        _logger = logger;
    }

    public event EventHandler<SessionState>? StateChanged;

    public event EventHandler<BlockedLaunchNotice>? LaunchBlocked;

    public event EventHandler<SessionSummary>? Completed;

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
            _blocking.LaunchBlocked += OnLaunchBlocked;
            state = Apply(_engine.Advance(now));
            _ticks = _timer.Start(TickInterval, Tick);
        }

        _logger.LogInformation("Session started: {Plan}", plan);
        StateChanged?.Invoke(this, state);
        _blocking.Activate(policy, _settings.Current.CloseAlreadyRunningGames);
        return Task.CompletedTask;
    }

    public Task EndEarlyAsync(CancellationToken cancellationToken = default)
    {
        SessionSummary? summary = null;
        lock (_gate)
        {
            if (_engine.IsRunning)
            {
                summary = Finish(_clock.UtcNow, endedEarly: true);
            }
        }

        if (summary is not null)
        {
            RaiseCompleted(summary);
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _ticks?.Dispose();
            _ticks = null;
            _blocking.LaunchBlocked -= OnLaunchBlocked;
        }
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
        }
        else if (deactivateBlocking)
        {
            _logger.LogInformation("Break time");
            _blocking.Deactivate();
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
            _blockedAttempts);
        return State;
    }
}
