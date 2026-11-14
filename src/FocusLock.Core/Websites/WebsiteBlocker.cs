using FocusLock.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace FocusLock.Core.Websites;

/// <summary>
/// Turns the website block on and off for sessions. Calls never block the caller and never overlap:
/// a quick on/off/on ends in the last requested state. Contract stub: Task C implements it.
/// </summary>
public sealed class WebsiteBlocker(ISiteBlockBackend backend, ILogger<WebsiteBlocker> logger)
{
#pragma warning disable CS0067 // Contract stub: Task C raises it.
    /// <summary>Raised after each backend call with its result. Handlers run on a background thread.</summary>
    public event EventHandler<SiteBlockResult>? StatusChanged;
#pragma warning restore CS0067

    /// <summary>Requests that exactly <paramref name="domains"/> be blocked. An empty list means nothing to block.</summary>
    public void Activate(IReadOnlyList<string> domains) => throw new NotImplementedException();

    /// <summary>Requests that the block be removed.</summary>
    public void Deactivate() => throw new NotImplementedException();

    /// <summary>Completes when every requested change has finished, so quitting can wait for the clean-up.</summary>
    public Task WhenIdleAsync() => throw new NotImplementedException();

    /// <summary>App startup with no session running: removes a block left behind by a crash.</summary>
    public Task RecoverAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();

    // Keeps the primary-constructor parameters referenced until Task C uses them.
    internal object[] Dependencies => [backend, logger];
}
