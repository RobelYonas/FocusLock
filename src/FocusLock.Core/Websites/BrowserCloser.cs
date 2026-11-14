using FocusLock.Core.Abstractions;
using FocusLock.Core.Common;
using Microsoft.Extensions.Logging;

namespace FocusLock.Core.Websites;

/// <summary>Finds and closes known web browsers at session start.</summary>
public sealed class BrowserCloser(IProcessProvider processes, IProcessTerminator terminator, ILogger<BrowserCloser> logger)
{
    // Executable name -> display name. The order here is the order RunningBrowsers reports them in.
    private static readonly (string ProcessName, string DisplayName)[] KnownBrowsers =
    [
        ("chrome.exe", "Chrome"),
        ("msedge.exe", "Edge"),
        ("firefox.exe", "Firefox"),
        ("brave.exe", "Brave"),
        ("opera.exe", "Opera"),
        ("vivaldi.exe", "Vivaldi"),
    ];

    /// <summary>Display names of the known browsers that are running (e.g. "Chrome", "Edge"), each listed once.</summary>
    public IReadOnlyList<string> RunningBrowsers()
    {
        var running = ListProcesses();

        return KnownBrowsers
            .Where(browser => running.Any(process => PathNormalizer.NameEquals(process.Name, browser.ProcessName)))
            .Select(browser => browser.DisplayName)
            .ToList();
    }

    /// <summary>Closes every running process of a known browser. Never closes anything else, never throws.</summary>
    public void CloseAll()
    {
        foreach (var process in ListProcesses())
        {
            var browser = KnownBrowsers.FirstOrDefault(known => PathNormalizer.NameEquals(process.Name, known.ProcessName));
            if (browser.ProcessName is null)
            {
                continue;
            }

            Close(process, browser.DisplayName);
        }
    }

    private IReadOnlyList<ProcessInfo> ListProcesses()
    {
        try
        {
            return processes.ListProcesses();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not list running programs to find browsers");
            return [];
        }
    }

    private void Close(ProcessInfo process, string browser)
    {
        TerminateResult result;
        try
        {
            result = terminator.Terminate(process.Id);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not close {Browser} ({ProcessName}, pid {Pid})", browser, process.Name, process.Id);
            return;
        }

        switch (result)
        {
            case TerminateResult.Terminated:
                logger.LogInformation("Closed {Browser} ({ProcessName}, pid {Pid})", browser, process.Name, process.Id);
                break;
            case TerminateResult.AlreadyExited:
                logger.LogDebug("{Browser} ({ProcessName}, pid {Pid}) had already exited", browser, process.Name, process.Id);
                break;
            default:
                logger.LogWarning("Could not close {Browser} ({ProcessName}, pid {Pid}): {Result}", browser, process.Name, process.Id, result);
                break;
        }
    }
}
