namespace FocusLock.Core.Abstractions;

public enum SiteBlockResult
{
    Applied,
    Cleared,

    /// <summary>The user declined the Windows permission prompt (UAC).</summary>
    UserCancelled,

    Failed,
}

/// <summary>
/// Makes the system-wide website block happen: the FocusLock section of the hosts file, the browsers'
/// DNS-over-HTTPS policies and the DNS cache. Today this runs FocusLock elevated for each change;
/// a Windows Service can implement the same interface later.
/// </summary>
public interface ISiteBlockBackend
{
    /// <summary>Blocks exactly <paramref name="domains"/>, replacing any earlier FocusLock block. Never throws for expected failures.</summary>
    Task<SiteBlockResult> ApplyAsync(IReadOnlyList<string> domains, CancellationToken cancellationToken);

    /// <summary>Removes FocusLock's block and restores the earlier browser policies. Succeeds when there is nothing to clear.</summary>
    Task<SiteBlockResult> ClearAsync(CancellationToken cancellationToken);

    /// <summary>True when the hosts file contains a FocusLock section. Read-only; needs no elevation.</summary>
    bool IsBlockPresent();
}
