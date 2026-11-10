using FocusLock.Core.Abstractions;
using FocusLock.Core.Processes;
using FocusLock.Tests.Fakes;
using Microsoft.Extensions.Logging;

namespace FocusLock.Tests.Processes;

public class PollingProcessWatcherTests
{
    private readonly FakeProcessProvider _provider = new();
    private readonly FakeTimer _timer = new();
    private readonly ListLogger<PollingProcessWatcher> _logger = new();
    private readonly PollingProcessWatcher _watcher;
    private readonly List<ProcessInfo> _started = [];

    public PollingProcessWatcherTests()
    {
        _watcher = new PollingProcessWatcher(_provider, _timer, new ProcessWatcherOptions(TimeSpan.FromSeconds(1)), _logger);
        _watcher.ProcessStarted += (_, process) => _started.Add(process);
        _provider.Add(1, "explorer.exe", @"C:\Windows\explorer.exe").Add(2, "code.exe", @"C:\Apps\code.exe");
    }

    [Fact]
    public void Start_takes_a_baseline_and_only_reports_processes_started_afterwards()
    {
        _watcher.Start();
        Assert.Equal(TimeSpan.FromSeconds(1), _timer.LastInterval);
        Assert.Empty(_started);

        _provider.Add(3, "foo.exe", @"C:\Games\Foo\foo.exe");
        _timer.Tick();

        var started = Assert.Single(_started);
        Assert.Equal(3, started.Id);
        Assert.Equal(@"C:\Games\Foo\foo.exe", started.ExecutablePath);

        _timer.Tick();
        Assert.Single(_started);
    }

    [Fact]
    public void Exited_processes_are_not_reported()
    {
        _watcher.Start();

        _provider.Remove(2);
        _timer.Tick();

        Assert.Empty(_started);
    }

    [Fact]
    public void Paths_are_only_resolved_for_new_processes()
    {
        _watcher.Start();
        _provider.Add(3, "foo.exe", @"C:\Games\Foo\foo.exe");

        _timer.Tick(3);

        Assert.Equal(1, _provider.PathLookups);
    }

    [Fact]
    public void Unreadable_path_is_reported_as_null_and_logged()
    {
        _watcher.Start();
        _provider.Add(3, "protected.exe");

        _timer.Tick();

        Assert.Null(Assert.Single(_started).ExecutablePath);
        Assert.True(_logger.Has(LogLevel.Debug, "protected.exe"));
    }

    [Fact]
    public void Listing_failure_is_logged_and_polling_recovers()
    {
        _watcher.Start();
        _provider.ThrowOnList = true;

        _timer.Tick();
        Assert.Empty(_started);
        Assert.True(_logger.Has(LogLevel.Warning, "Listing"));

        _provider.ThrowOnList = false;
        _provider.Add(4, "foo.exe", @"C:\Games\Foo\foo.exe");
        _timer.Tick();

        Assert.Equal(4, Assert.Single(_started).Id);
    }

    [Fact]
    public void Stop_disposes_the_timer_and_start_is_idempotent()
    {
        _watcher.Start();
        _watcher.Start();
        Assert.Equal(1, _timer.ActiveCount);

        _watcher.Stop();
        Assert.Equal(0, _timer.ActiveCount);

        _provider.Add(3, "foo.exe", @"C:\Games\Foo\foo.exe");
        _timer.Tick();
        Assert.Empty(_started);
    }

    [Fact]
    public void Snapshot_resolves_paths_for_everything()
    {
        _provider.Add(3, "protected.exe");

        var snapshot = _watcher.Snapshot();

        Assert.Equal(3, snapshot.Count);
        Assert.Equal(@"C:\Apps\code.exe", snapshot.Single(process => process.Id == 2).ExecutablePath);
        Assert.Null(snapshot.Single(process => process.Id == 3).ExecutablePath);
    }

    [Fact]
    public void A_failing_handler_is_logged_and_does_not_stop_later_reports()
    {
        _watcher.ProcessStarted += (_, _) => throw new InvalidOperationException("handler broke");
        _watcher.Start();

        _provider.Add(3, "a.exe", @"C:\a.exe");
        _timer.Tick();
        _provider.Add(4, "b.exe", @"C:\b.exe");
        _timer.Tick();

        Assert.Equal([3, 4], _started.Select(process => process.Id));
        Assert.True(_logger.Has(LogLevel.Error, "a.exe"));
    }
}
