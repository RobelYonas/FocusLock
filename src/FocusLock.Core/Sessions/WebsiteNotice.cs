namespace FocusLock.Core.Sessions;

public enum WebsiteNoticeKind
{
    /// <summary>The websites are blocked. Raised once per session, after the first successful block.</summary>
    Blocked,

    /// <summary>The user declined the permission prompt for blocking, so websites aren't blocked this time.</summary>
    PermissionDeclined,

    /// <summary>Blocking failed for another reason, so websites aren't blocked this time.</summary>
    Failed,

    /// <summary>Unblocking was declined or failed, so websites are still blocked. FocusLock tries again the next time it starts.</summary>
    StillBlocked,

    /// <summary>Browsers were already open at session start; tabs that are open need a browser restart.</summary>
    RestartBrowsers,
}

/// <summary>A message about website blocking for the UI. Plain data, so a Windows Service could raise it later.</summary>
public sealed record WebsiteNotice(WebsiteNoticeKind Kind, IReadOnlyList<string> Browsers)
{
    public WebsiteNotice(WebsiteNoticeKind kind)
        : this(kind, [])
    {
    }
}
