using FocusLock.Core.Abstractions;
using FocusLock.Core.Websites;
using FocusLock.Tests.Fakes;
using Microsoft.Extensions.Logging;

namespace FocusLock.Tests.Websites;

public class BrowserCloserTests
{
    private readonly FakeProcessProvider _processes = new();
    private readonly FakeProcessTerminator _terminator = new();
    private readonly ListLogger<BrowserCloser> _logger = new();

    private BrowserCloser Create() => new(_processes, _terminator, _logger);

    [Fact]
    public void Lists_each_running_browser_once_in_a_stable_order()
    {
        _processes
            .Add(1, "firefox.exe")
            .Add(2, "chrome.exe")
            .Add(3, "chrome.exe")
            .Add(4, "chrome.exe")
            .Add(5, "msedge.exe");

        Assert.Equal(["Chrome", "Edge", "Firefox"], Create().RunningBrowsers());
    }

    [Fact]
    public void Matches_process_names_case_insensitively()
    {
        _processes.Add(1, "CHROME.EXE").Add(2, "Brave.exe");

        Assert.Equal(["Chrome", "Brave"], Create().RunningBrowsers());
    }

    [Fact]
    public void Ignores_processes_that_are_not_browsers()
    {
        _processes.Add(1, "chromedriver.exe").Add(2, "notepad.exe");

        Assert.Empty(Create().RunningBrowsers());
    }

    [Fact]
    public void Closes_every_browser_process_and_nothing_else()
    {
        _processes
            .Add(1, "chrome.exe")
            .Add(2, "notepad.exe")
            .Add(3, "chrome.exe")
            .Add(4, "chromedriver.exe")
            .Add(5, "msedge.exe")
            .Add(6, "chrome.exe");

        Create().CloseAll();

        Assert.Equal([1, 3, 5, 6], _terminator.Terminated);
    }

    [Fact]
    public void Process_listing_failure_gives_no_browsers_and_closes_nothing()
    {
        _processes.Add(1, "chrome.exe");
        _processes.ThrowOnList = true;

        var closer = Create();

        Assert.Empty(closer.RunningBrowsers());
        closer.CloseAll();
        Assert.Empty(_terminator.Terminated);
        Assert.True(_logger.Has(LogLevel.Warning, "Could not list"));
    }

    [Fact]
    public void Access_denied_on_one_process_does_not_stop_the_others()
    {
        _processes.Add(1, "chrome.exe").Add(2, "chrome.exe");
        _terminator.Results[1] = TerminateResult.AccessDenied;

        Create().CloseAll();

        Assert.Equal([1, 2], _terminator.Terminated);
        Assert.True(_logger.Has(LogLevel.Warning, "AccessDenied"));
    }
}
