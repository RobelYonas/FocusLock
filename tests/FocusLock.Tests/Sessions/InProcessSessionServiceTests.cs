using FocusLock.Core.Abstractions;
using FocusLock.Core.Blocking;
using FocusLock.Core.Detection;
using FocusLock.Core.Sessions;
using FocusLock.Core.Settings;
using FocusLock.Tests.Fakes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FocusLock.Tests.Sessions;

public class InProcessSessionServiceTests
{
    private static readonly BlockPolicy Policy = new([new BlockRule("Foo", MatchKind.Folder, @"C:\Games\Foo")], []);
    private static readonly SessionPlan Pomodoro = new(TimeSpan.FromMinutes(25), Pomodoro: true, TimeSpan.FromMinutes(5), Cycles: 2);

    private readonly FakeClock _clock = new();
    private readonly FakeTimer _timer = new();
    private readonly FakeProcessWatcher _watcher = new();
    private readonly FakeProcessTerminator _terminator = new();
    private readonly FakeStatsStore _stats = new();
    private readonly AppSettings _settings = new();
    private readonly ListLogger<InProcessSessionService> _logger = new();
    private readonly BlockingEngine _blocking;
    private readonly InProcessSessionService _service;
    private readonly List<SessionState> _states = [];
    private readonly List<BlockedLaunchNotice> _notices = [];
    private readonly List<SessionSummary> _summaries = [];

    public InProcessSessionServiceTests()
    {
        _blocking = new BlockingEngine(_watcher, _terminator, new BlockRuleEvaluator(new FakeAppEnvironment()), NullLogger<BlockingEngine>.Instance);
        _service = new InProcessSessionService(new FocusSessionEngine(), _blocking, _timer, _clock, _stats, TestSettings.Service(_settings), _logger);
        _service.StateChanged += (_, state) => _states.Add(state);
        _service.LaunchBlocked += (_, notice) => _notices.Add(notice);
        _service.Completed += (_, summary) => _summaries.Add(summary);
    }

    [Fact]
    public async Task Start_turns_blocking_on_and_reports_the_initial_state()
    {
        await _service.StartAsync(SessionPlan.Single(TimeSpan.FromMinutes(25)), Policy);

        Assert.True(_blocking.IsActive);
        Assert.True(_watcher.IsStarted);
        Assert.True(_service.State.IsRunning);
        Assert.Equal(TimeSpan.FromMinutes(25), _service.State.RemainingInPhase);
        Assert.Equal(SessionPhase.Focus, _service.State.Phase);
        Assert.Single(_states);
        Assert.Equal(1, _timer.ActiveCount);
        Assert.Equal(InProcessSessionService.TickInterval, _timer.LastInterval);
    }

    [Fact]
    public async Task Ticks_update_the_remaining_time()
    {
        await _service.StartAsync(SessionPlan.Single(TimeSpan.FromMinutes(25)), Policy);

        _clock.Advance(TimeSpan.FromSeconds(1));
        _timer.Tick();

        Assert.Equal(TimeSpan.FromSeconds(24 * 60 + 59), _service.State.RemainingInPhase);
        Assert.Equal(2, _states.Count);
    }

    [Fact]
    public async Task A_blocked_launch_is_counted_and_announced_with_the_time_left()
    {
        await _service.StartAsync(SessionPlan.Single(TimeSpan.FromMinutes(25)), Policy);
        _clock.Advance(TimeSpan.FromMinutes(3));

        _watcher.RaiseStarted(10, "foo.exe", @"C:\Games\Foo\foo.exe");

        Assert.Equal(1, _service.State.BlockedAttempts);
        var notice = Assert.Single(_notices);
        Assert.Equal("Foo", notice.GameName);
        Assert.Equal(TerminateResult.Terminated, notice.Result);
        Assert.Equal(TimeSpan.FromMinutes(22), notice.RemainingInPhase);
        Assert.Equal(1, _states.Last().BlockedAttempts);
    }

    [Fact]
    public async Task A_launch_that_could_not_be_closed_is_announced_but_not_counted()
    {
        _terminator.Results[10] = TerminateResult.AccessDenied;
        await _service.StartAsync(SessionPlan.Single(TimeSpan.FromMinutes(25)), Policy);

        _watcher.RaiseStarted(10, "foo.exe", @"C:\Games\Foo\foo.exe");

        Assert.Equal(0, _service.State.BlockedAttempts);
        Assert.Equal(TerminateResult.AccessDenied, Assert.Single(_notices).Result);
    }

    [Fact]
    public async Task Breaks_pause_blocking_and_the_next_focus_block_resumes_it()
    {
        await _service.StartAsync(Pomodoro, Policy);

        _clock.Advance(TimeSpan.FromMinutes(25).Add(TimeSpan.FromSeconds(1)));
        _timer.Tick();
        Assert.Equal(SessionPhase.Break, _service.State.Phase);
        Assert.False(_blocking.IsActive);
        Assert.Equal(1, _watcher.StopCount);

        _watcher.RaiseStarted(10, "foo.exe", @"C:\Games\Foo\foo.exe");
        Assert.Empty(_terminator.Terminated);

        _clock.Advance(TimeSpan.FromMinutes(5));
        _timer.Tick();
        Assert.Equal(SessionPhase.Focus, _service.State.Phase);
        Assert.Equal(2, _service.State.Cycle);
        Assert.True(_blocking.IsActive);
        Assert.Equal(2, _watcher.StartCount);
    }

    [Fact]
    public async Task Completion_reports_a_summary_saves_stats_and_resets()
    {
        await _service.StartAsync(SessionPlan.Single(TimeSpan.FromMinutes(25)), Policy);
        _watcher.RaiseStarted(10, "foo.exe", @"C:\Games\Foo\foo.exe");

        _clock.Advance(TimeSpan.FromMinutes(25));
        _timer.Tick();

        var summary = Assert.Single(_summaries);
        Assert.Equal(TimeSpan.FromMinutes(25), summary.FocusedTime);
        Assert.Equal(1, summary.BlockedAttempts);
        Assert.False(summary.EndedEarly);
        Assert.Equal(_clock.UtcNow.AddMinutes(-25), summary.StartedAt);
        Assert.Equal(_clock.UtcNow, summary.EndedAt);
        Assert.Equal(summary, Assert.Single(_stats.Appended));
        Assert.False(_service.State.IsRunning);
        Assert.False(_blocking.IsActive);
        Assert.Equal(0, _timer.ActiveCount);
        Assert.False(_states.Last().IsRunning);
    }

    [Fact]
    public async Task Ending_early_reports_the_partial_focus_time()
    {
        await _service.StartAsync(SessionPlan.Single(TimeSpan.FromMinutes(25)), Policy);
        _clock.Advance(TimeSpan.FromMinutes(10));

        await _service.EndEarlyAsync();

        var summary = Assert.Single(_summaries);
        Assert.True(summary.EndedEarly);
        Assert.Equal(TimeSpan.FromMinutes(10), summary.FocusedTime);
        Assert.False(_service.State.IsRunning);
        Assert.False(_blocking.IsActive);

        await _service.EndEarlyAsync();
        Assert.Single(_summaries);
    }

    [Fact]
    public async Task Starting_while_running_is_rejected()
    {
        await _service.StartAsync(SessionPlan.Single(TimeSpan.FromMinutes(25)), Policy);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.StartAsync(SessionPlan.Single(TimeSpan.FromMinutes(25)), Policy));
    }

    [Fact]
    public async Task A_stats_failure_is_logged_and_the_summary_is_still_delivered()
    {
        _stats.ThrowOnAppend = true;
        await _service.StartAsync(SessionPlan.Single(TimeSpan.FromMinutes(1)), Policy);

        _clock.Advance(TimeSpan.FromMinutes(1));
        _timer.Tick();

        Assert.Single(_summaries);
        Assert.True(_logger.Has(LogLevel.Error, "summary"));
    }

    [Fact]
    public async Task Games_already_open_are_closed_at_start_when_the_setting_is_on()
    {
        _watcher.Running.Add(new ProcessInfo(20, "foo.exe", @"C:\Games\Foo\foo.exe"));

        await _service.StartAsync(SessionPlan.Single(TimeSpan.FromMinutes(25)), Policy);

        Assert.Equal([20], _terminator.Terminated);
        Assert.Equal(1, _service.State.BlockedAttempts);
    }

    [Fact]
    public async Task Games_already_open_are_left_alone_when_the_setting_is_off()
    {
        _settings.CloseAlreadyRunningGames = false;
        _watcher.Running.Add(new ProcessInfo(20, "foo.exe", @"C:\Games\Foo\foo.exe"));

        await _service.StartAsync(SessionPlan.Single(TimeSpan.FromMinutes(25)), Policy);

        Assert.Empty(_terminator.Terminated);
    }
}
