using FocusLock.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace FocusLock.Core.Platform;

/// <summary>
/// Runs FocusLock's own exe elevated (<c>--site-block apply|clear</c>) for every change and maps its exit code.
/// Contract stub: Task A implements it.
/// </summary>
public sealed class ElevatedSiteBlockBackend(ILogger<ElevatedSiteBlockBackend> logger) : ISiteBlockBackend
{
    public Task<SiteBlockResult> ApplyAsync(IReadOnlyList<string> domains, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task<SiteBlockResult> ClearAsync(CancellationToken cancellationToken) => throw new NotImplementedException();

    public bool IsBlockPresent() => throw new NotImplementedException();

    // Keeps the primary-constructor parameter referenced until Task A uses it.
    internal ILogger Logger => logger;
}
