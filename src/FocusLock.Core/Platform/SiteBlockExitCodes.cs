using FocusLock.Core.Abstractions;

namespace FocusLock.Core.Platform;

/// <summary>Exit codes of the elevated <c>--site-block</c> helper, shared by the helper and <see cref="ElevatedSiteBlockBackend"/>.</summary>
public static class SiteBlockExitCodes
{
    public const int Success = 0;

    /// <summary>Something unexpected went wrong.</summary>
    public const int Unexpected = 1;

    /// <summary>Unknown verb, wrong argument count or an invalid domain. Nothing was changed.</summary>
    public const int BadArguments = 2;

    /// <summary>The hosts file could not be read or written.</summary>
    public const int HostsFailed = 3;

    /// <summary>The browser DNS-over-HTTPS policies or their backup could not be changed.</summary>
    public const int PolicyFailed = 4;

    /// <summary>Win32 ERROR_CANCELLED: the user declined the UAC prompt.</summary>
    public const int UacCancelledWin32Error = 1223;

    public static SiteBlockResult ToResult(int exitCode, bool apply) => exitCode switch
    {
        Success => apply ? SiteBlockResult.Applied : SiteBlockResult.Cleared,
        _ => SiteBlockResult.Failed,
    };
}
