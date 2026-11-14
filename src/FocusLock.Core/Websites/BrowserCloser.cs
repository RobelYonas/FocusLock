using FocusLock.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace FocusLock.Core.Websites;

/// <summary>Finds and closes known web browsers at session start. Contract stub: Task F implements it.</summary>
public sealed class BrowserCloser(IProcessProvider processes, IProcessTerminator terminator, ILogger<BrowserCloser> logger)
{
    /// <summary>Display names of the known browsers that are running (e.g. "Chrome", "Edge"), each listed once.</summary>
    public IReadOnlyList<string> RunningBrowsers() => throw new NotImplementedException();

    /// <summary>Closes every running process of a known browser. Never closes anything else, never throws.</summary>
    public void CloseAll() => throw new NotImplementedException();

    // Keeps the primary-constructor parameters referenced until Task F uses them.
    internal object[] Dependencies => [processes, terminator, logger];
}
