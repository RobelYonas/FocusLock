using FocusLock.Core.Abstractions;
using FocusLock.Core.Blocking;
using FocusLock.Core.Detection;
using FocusLock.Core.Sessions;
using FocusLock.Core.Settings;
using FocusLock.Core.Websites;
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
    private readonly FakeSiteBlockBackend _siteBackend = new();
    private readonly FakeProcessProvider _browserProcesses = new();
    private readonly FakeProcessTerminator _browserTerminator = new();
    private readonly WebsiteBlocker _websites;
    private readonly List<WebsiteNotice> _websiteNotices = [];

    public InProcessSessionServiceTests()
    {
        _blocking = new BlockingEngine(_watcher, _terminator, new BlockRuleEvaluator(new FakeAppEnvironment()), NullLogger<BlockingEngine>.Instance);
        _websites = new WebsiteBlocker(_siteBackend, NullLogger<WebsiteBlocker>.Instance);
        var browsers = new BrowserCloser(_browserProcesses, _browserTerminator, NullLogger<BrowserCloser>.Instance);
        _service = new InProcessSessionService(new FocusSessionEngine(), _blocking, _websites, browsers, _timer, _clock, _stats, TestSettings.Service(_settings), _logger);
        _service.StateChanged += (_, state) => _states.Add(state);
        _service.LaunchBlocked += (_, notice) => _notices.Add(notice);
        _service.Completed += (_, summary) => _summaries.Add(summary);
        _service.WebsiteNotice += (_, notice) =>
        {
            lock (_websiteNotices)
            {
                _websiteNotices.Add(notice);
            }
        };
    }

    private static BlockPolicy SitePolicy => Policy with { BlockedDomains = ["youtube.com"] };

    private List<WebsiteNoticeKind> NoticeKinds
    {
        get
        {
            lock (_websiteNotices)
            {
                return _websiteNotices.Select(notice => notice.Kind).ToList();
            }
        }
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
    public async Task A_committed_session_cannot_be_ended_early_and_still_finishes_on_time()
    {
        await _service.StartAsync(SessionPlan.Single(TimeSpan.FromMinutes(25)) with { Committed = true }, Policy);
        Assert.False(_service.State.CanEndEarly);
        _clock.Advance(TimeSpan.FromMinutes(10));

        await _service.EndEarlyAsync();

        Assert.Empty(_summaries);
        Assert.True(_service.State.IsRunning);
        Assert.True(_blocking.IsActive);

        _clock.Advance(TimeSpan.FromMinutes(15));
        _timer.Tick();

        var summary = Assert.Single(_summaries);
        Assert.False(summary.EndedEarly);
        Assert.Equal(TimeSpan.FromMinutes(25), summary.FocusedTime);
    }

    [Fact]
    public async Task A_normal_session_can_be_ended_early()
    {
        await _service.StartAsync(SessionPlan.Single(TimeSpan.FromMinutes(25)), Policy);

        Assert.True(_service.State.CanEndEarly);
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

    [Fact]
    public async Task Start_blocks_the_websites_and_says_so()
    {
        await _service.StartAsync(SessionPlan.Single(TimeSpan.FromMinutes(25)), SitePolicy);
        await _websites.WhenIdleAsync();

        Assert.Equal(["apply:youtube.com"], _siteBackend.Calls);
        Assert.Equal([WebsiteNoticeKind.Blocked], NoticeKinds);
    }

    [Fact]
    public async Task Breaks_unblock_websites_and_focus_blocks_them_again_with_one_notice()
    {
        await _service.StartAsync(Pomodoro, SitePolicy);
        await _websites.WhenIdleAsync();

        _clock.Advance(TimeSpan.FromMinutes(25).Add(TimeSpan.FromSeconds(1)));
        _timer.Tick();
        await _websites.WhenIdleAsync();
        Assert.Equal(["apply:youtube.com", "clear"], _siteBackend.Calls);

        _clock.Advance(TimeSpan.FromMinutes(5));
        _timer.Tick();
        await _websites.WhenIdleAsync();

        Assert.Equal(["apply:youtube.com", "clear", "apply:youtube.com"], _siteBackend.Calls);
        Assert.Equal([WebsiteNoticeKind.Blocked], NoticeKinds);
    }

    [Fact]
    public async Task Finishing_on_time_unblocks_the_websites()
    {
        await _service.StartAsync(SessionPlan.Single(TimeSpan.FromMinutes(25)), SitePolicy);
        await _websites.WhenIdleAsync();

        _clock.Advance(TimeSpan.FromMinutes(25));
        _timer.Tick();
        await _websites.WhenIdleAsync();

        Assert.Equal(["apply:youtube.com", "clear"], _siteBackend.Calls);
    }

    [Fact]
    public async Task Ending_early_waits_until_the_websites_are_unblocked()
    {
        await _service.StartAsync(SessionPlan.Single(TimeSpan.FromMinutes(25)), SitePolicy);
        await _websites.WhenIdleAsync();
        await _siteBackend.WaitForCallAsync();
        _siteBackend.Hold = new TaskCompletionSource();

        var ending = _service.EndEarlyAsync();
        await _siteBackend.WaitForCallAsync();
        Assert.False(ending.IsCompleted);

        _siteBackend.Hold.SetResult();
        await ending;

        Assert.Equal(["apply:youtube.com", "clear"], _siteBackend.Calls);
    }

    [Fact]
    public async Task A_committed_session_keeps_websites_blocked_when_ending_early_is_tried()
    {
        await _service.StartAsync(SessionPlan.Single(TimeSpan.FromMinutes(25)) with { Committed = true }, SitePolicy);

        await _service.EndEarlyAsync();
        await _websites.WhenIdleAsync();

        Assert.Equal(["apply:youtube.com"], _siteBackend.Calls);
    }

    [Fact]
    public async Task A_declined_permission_prompt_is_announced_once_per_session()
    {
        _siteBackend.ApplyResult = SiteBlockResult.UserCancelled;
        await _service.StartAsync(Pomodoro, SitePolicy);
        await _websites.WhenIdleAsync();

        _clock.Advance(TimeSpan.FromMinutes(25).Add(TimeSpan.FromSeconds(1)));
        _timer.Tick();
        _clock.Advance(TimeSpan.FromMinutes(5));
        _timer.Tick();
        await _websites.WhenIdleAsync();

        Assert.Equal(["apply:youtube.com", "apply:youtube.com"], _siteBackend.Calls);
        Assert.Equal([WebsiteNoticeKind.PermissionDeclined], NoticeKinds);
    }

    [Fact]
    public async Task A_declined_unblock_says_websites_are_still_blocked()
    {
        _siteBackend.ClearResult = SiteBlockResult.UserCancelled;
        await _service.StartAsync(SessionPlan.Single(TimeSpan.FromMinutes(25)), SitePolicy);
        await _websites.WhenIdleAsync();

        await _service.EndEarlyAsync();

        Assert.Equal([WebsiteNoticeKind.Blocked, WebsiteNoticeKind.StillBlocked], NoticeKinds);
    }

    [Fact]
    public async Task A_throwing_backend_is_announced_as_failed_and_the_session_keeps_running()
    {
        _siteBackend.ThrowOnCall = true;

        await _service.StartAsync(SessionPlan.Single(TimeSpan.FromMinutes(25)), SitePolicy);
        await _websites.WhenIdleAsync();

        Assert.Equal([WebsiteNoticeKind.Failed], NoticeKinds);
        Assert.True(_service.State.IsRunning);
        Assert.True(_blocking.IsActive);
    }

    [Fact]
    public async Task Without_blocked_sites_the_backend_and_browsers_are_left_alone()
    {
        _settings.CloseBrowsersOnSessionStart = true;
        _browserProcesses.Add(30, "chrome.exe");

        await _service.StartAsync(Pomodoro, Policy);
        _clock.Advance(TimeSpan.FromMinutes(25).Add(TimeSpan.FromSeconds(1)));
        _timer.Tick();
        await _service.EndEarlyAsync();
        await _websites.WhenIdleAsync();

        Assert.Empty(_siteBackend.Calls);
        Assert.Empty(_browserTerminator.Terminated);
        Assert.Empty(NoticeKinds);
    }

    [Fact]
    public async Task Browsers_are_closed_at_start_when_the_setting_is_on()
    {
        _settings.CloseBrowsersOnSessionStart = true;
        _browserProcesses.Add(30, "chrome.exe");

        await _service.StartAsync(SessionPlan.Single(TimeSpan.FromMinutes(25)), SitePolicy);
        await _websites.WhenIdleAsync();

        Assert.Equal([30], _browserTerminator.Terminated);
        var notice = Assert.Single(_websiteNotices);
        Assert.Equal(WebsiteNoticeKind.Blocked, notice.Kind);
        Assert.Empty(notice.Browsers);
    }

    [Fact]
    public async Task Browsers_open_at_start_are_listed_in_the_first_blocked_notice()
    {
        _browserProcesses.Add(30, "chrome.exe");

        await _service.StartAsync(SessionPlan.Single(TimeSpan.FromMinutes(25)), SitePolicy);
        await _websites.WhenIdleAsync();

        Assert.Empty(_browserTerminator.Terminated);
        var notice = Assert.Single(_websiteNotices);
        Assert.Equal(WebsiteNoticeKind.Blocked, notice.Kind);
        Assert.NotEmpty(notice.Browsers);
    }

    [Fact]
    public async Task A_declined_block_gives_no_browser_reminder()
    {
        _siteBackend.ApplyResult = SiteBlockResult.UserCancelled;
        _browserProcesses.Add(30, "chrome.exe");

        await _service.StartAsync(SessionPlan.Single(TimeSpan.FromMinutes(25)), SitePolicy);
        await _websites.WhenIdleAsync();

        var notice = Assert.Single(_websiteNotices);
        Assert.Equal(WebsiteNoticeKind.PermissionDeclined, notice.Kind);
        Assert.Empty(notice.Browsers);
    }

    [Fact]
    public async Task A_failing_browser_check_does_not_stop_website_blocking()
    {
        _browserProcesses.ThrowOnList = true;

        await _service.StartAsync(SessionPlan.Single(TimeSpan.FromMinutes(25)), SitePolicy);
        await _websites.WhenIdleAsync();

        Assert.True(_service.State.IsRunning);
        Assert.Equal(["apply:youtube.com"], _siteBackend.Calls);
    }
}
