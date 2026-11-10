using FocusLock.Core.Abstractions;
using FocusLock.Core.Blocking;
using FocusLock.Core.Detection;
using FocusLock.Tests.Fakes;
using Microsoft.Extensions.Logging;

namespace FocusLock.Tests.Blocking;

public class BlockingEngineTests
{
    private static readonly BlockPolicy Policy = new([new BlockRule("Foo", MatchKind.Folder, @"C:\Games\Foo")], []);

    private readonly FakeProcessWatcher _watcher = new();
    private readonly FakeProcessTerminator _terminator = new();
    private readonly ListLogger<BlockingEngine> _logger = new();
    private readonly BlockingEngine _engine;
    private readonly List<LaunchBlockedEventArgs> _blocked = [];

    public BlockingEngineTests()
    {
        _engine = new BlockingEngine(_watcher, _terminator, new BlockRuleEvaluator(new FakeAppEnvironment()), _logger);
        _engine.LaunchBlocked += (_, e) => _blocked.Add(e);
    }

    [Fact]
    public void Activate_starts_the_watcher_and_closes_new_matching_processes()
    {
        _engine.Activate(Policy, closeAlreadyRunning: false);

        Assert.True(_watcher.IsStarted);
        Assert.True(_engine.IsActive);

        _watcher.RaiseStarted(10, "foo.exe", @"C:\Games\Foo\foo.exe");

        Assert.Equal([10], _terminator.Terminated);
        var blocked = Assert.Single(_blocked);
        Assert.Equal("Foo", blocked.GameName);
        Assert.Equal(TerminateResult.Terminated, blocked.Result);
        Assert.Equal(10, blocked.Process.Id);
        Assert.True(_logger.Has(LogLevel.Information, "Closed Foo"));
    }

    [Fact]
    public void Ignores_processes_that_do_not_match()
    {
        _engine.Activate(Policy, closeAlreadyRunning: false);

        _watcher.RaiseStarted(11, "code.exe", @"C:\Apps\code.exe");

        Assert.Empty(_terminator.Terminated);
        Assert.Empty(_blocked);
    }

    [Fact]
    public void Initial_sweep_closes_already_running_games_only_when_requested()
    {
        _watcher.Running.Add(new ProcessInfo(20, "foo.exe", @"C:\Games\Foo\foo.exe"));
        _watcher.Running.Add(new ProcessInfo(21, "code.exe", @"C:\Apps\code.exe"));

        _engine.Activate(Policy, closeAlreadyRunning: false);
        Assert.Empty(_terminator.Terminated);

        _engine.Deactivate();
        _engine.Activate(Policy, closeAlreadyRunning: true);
        Assert.Equal([20], _terminator.Terminated);
    }

    [Fact]
    public void Deactivate_stops_the_watcher_and_ignores_later_starts()
    {
        _engine.Activate(Policy, closeAlreadyRunning: false);

        _engine.Deactivate();
        _watcher.RaiseStarted(10, "foo.exe", @"C:\Games\Foo\foo.exe");

        Assert.False(_engine.IsActive);
        Assert.False(_watcher.IsStarted);
        Assert.Equal(1, _watcher.StopCount);
        Assert.Empty(_terminator.Terminated);
        Assert.Empty(_blocked);
    }

    [Fact]
    public void Deactivate_when_inactive_does_nothing()
    {
        _engine.Deactivate();

        Assert.Equal(0, _watcher.StopCount);
    }

    [Fact]
    public void Reports_access_denied_and_logs_a_warning()
    {
        _terminator.Results[10] = TerminateResult.AccessDenied;
        _engine.Activate(Policy, closeAlreadyRunning: false);

        _watcher.RaiseStarted(10, "foo.exe", @"C:\Games\Foo\foo.exe");

        Assert.Equal(TerminateResult.AccessDenied, Assert.Single(_blocked).Result);
        Assert.True(_logger.Has(LogLevel.Warning, "access denied"));
    }

    [Fact]
    public void Already_exited_is_reported_and_logged_quietly()
    {
        _terminator.Results[10] = TerminateResult.AlreadyExited;
        _engine.Activate(Policy, closeAlreadyRunning: false);

        _watcher.RaiseStarted(10, "foo.exe", @"C:\Games\Foo\foo.exe");

        Assert.Equal(TerminateResult.AlreadyExited, Assert.Single(_blocked).Result);
        Assert.True(_logger.Has(LogLevel.Debug, "already exited"));
        Assert.False(_logger.Has(LogLevel.Warning, "foo"));
    }

    [Fact]
    public void Activating_twice_replaces_the_policy_without_double_subscribing()
    {
        _engine.Activate(Policy, closeAlreadyRunning: false);
        _engine.Activate(new BlockPolicy([new BlockRule("Bar", MatchKind.Folder, @"C:\Games\Bar")], []), closeAlreadyRunning: false);

        _watcher.RaiseStarted(10, "foo.exe", @"C:\Games\Foo\foo.exe");
        _watcher.RaiseStarted(11, "bar.exe", @"C:\Games\Bar\bar.exe");

        Assert.Equal(1, _watcher.StartCount);
        Assert.Equal([11], _terminator.Terminated);
        Assert.Equal("Bar", Assert.Single(_blocked).GameName);
    }
}
