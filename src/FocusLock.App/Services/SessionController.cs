using CommunityToolkit.Mvvm.ComponentModel;
using FocusLock.Core.Abstractions;
using FocusLock.Core.Blocking;
using FocusLock.Core.Detection;
using FocusLock.Core.Sessions;
using FocusLock.Core.Settings;
using Microsoft.Extensions.Logging;

namespace FocusLock.App.Services;

/// <summary>
/// The one place the UI talks to sessions. Builds plan and policy from settings and the catalog,
/// and re-raises Core's background-thread events on the UI thread.
/// </summary>
public sealed partial class SessionController : ObservableObject
{
    private readonly ISessionService _sessions;
    private readonly GameCatalog _catalog;
    private readonly BlockPolicyBuilder _policies;
    private readonly SettingsService _settings;
    private readonly IStatsStore _stats;
    private readonly IClock _clock;
    private readonly ILogger<SessionController> _logger;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRunning))]
    private SessionState _state = SessionState.Idle;

    [ObservableProperty]
    private SessionSummary? _lastSummary;

    public SessionController(
        ISessionService sessions,
        GameCatalog catalog,
        BlockPolicyBuilder policies,
        SettingsService settings,
        IStatsStore stats,
        IClock clock,
        IUiDispatcher dispatcher,
        ILogger<SessionController> logger)
    {
        _sessions = sessions;
        _catalog = catalog;
        _policies = policies;
        _settings = settings;
        _stats = stats;
        _clock = clock;
        _logger = logger;

        sessions.StateChanged += (_, state) => dispatcher.Post(() => ApplyState(state));
        sessions.LaunchBlocked += (_, notice) => dispatcher.Post(() => LaunchBlocked?.Invoke(this, notice));
        sessions.Completed += (_, summary) => dispatcher.Post(() =>
        {
            LastSummary = summary;
            Completed?.Invoke(this, summary);
        });
    }

    public event EventHandler<BlockedLaunchNotice>? LaunchBlocked;

    public event EventHandler<SessionPhase>? PhaseChanged;

    public event EventHandler<SessionSummary>? Completed;

    public bool IsRunning => State.IsRunning;

    public SessionPlan BuildPlan(TimeSpan focusLength)
    {
        var pomodoro = _settings.Current.Pomodoro;
        return pomodoro.Enabled
            ? new SessionPlan(focusLength, Pomodoro: true, TimeSpan.FromMinutes(pomodoro.BreakMinutes), pomodoro.Cycles)
            : SessionPlan.Single(focusLength);
    }

    public async Task StartAsync(TimeSpan focusLength)
    {
        LastSummary = null;
        var plan = BuildPlan(focusLength);
        var entries = _catalog.Entries;
        var settings = _settings.Current;
        await Task.Run(async () =>
        {
            var policy = _policies.Build(entries, settings);
            await _sessions.StartAsync(plan, policy).ConfigureAwait(false);
        });
        _logger.LogInformation("Session of {Length} started from the UI", focusLength);
    }

    public Task EndEarlyAsync() => Task.Run(() => _sessions.EndEarlyAsync());

    public void DismissSummary() => LastSummary = null;

    /// <summary>Focused time of sessions that ended today (local time).</summary>
    public TimeSpan FocusedToday()
    {
        var now = _clock.UtcNow.ToLocalTime();
        var startOfDay = new DateTimeOffset(now.Date, now.Offset);
        return _stats.GetFocusedTime(startOfDay, startOfDay.AddDays(1));
    }

    private void ApplyState(SessionState state)
    {
        var previous = State;
        State = state;
        if (state.IsRunning && previous.IsRunning && state.Phase != previous.Phase)
        {
            PhaseChanged?.Invoke(this, state.Phase);
        }
    }
}
