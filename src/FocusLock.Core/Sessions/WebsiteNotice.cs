namespace FocusLock.Core.Sessions;

public enum WebsiteNoticeKind
{
    /// <summary>
    /// The websites are blocked. Raised once per session, after the first successful block. <c>Browsers</c> lists the
    /// browsers that were already open, whose open tabs need a restart.
    /// </summary>
    Blocked,

    /// <summary>The user declined the permission prompt for blocking, so websites aren't blocked this time.</summary>
    PermissionDeclined,

    /// <summary>Blocking failed for another reason, so websites aren't blocked this time.</summary>
    Failed,

    /// <summary>Unblocking was declined or failed, so websites are still blocked. FocusLock tries again the next time it starts.</summary>
    StillBlocked,
}

/// <summary>A message about website blocking for the UI. Plain data, so a Windows Service could raise it later.</summary>
public sealed record WebsiteNotice(WebsiteNoticeKind Kind, IReadOnlyList<string> Browsers)
{
    public WebsiteNotice(WebsiteNoticeKind kind)
        : this(kind, [])
    {
    }
}
