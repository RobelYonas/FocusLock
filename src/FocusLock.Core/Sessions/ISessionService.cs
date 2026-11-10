using FocusLock.Core.Blocking;

namespace FocusLock.Core.Sessions;

/// <summary>
/// The only session API the UI uses. Today it runs in-process; the same contract can later be served
/// by a Windows Service over named pipes, which is why every type involved is plain data.
/// </summary>
public interface ISessionService
{
    SessionState State { get; }

    Task StartAsync(SessionPlan plan, BlockPolicy policy, CancellationToken cancellationToken = default);

    Task EndEarlyAsync(CancellationToken cancellationToken = default);

    /// <summary>Raised about once a second while a session runs, and once more when it ends. Handlers run on a background thread.</summary>
    event EventHandler<SessionState>? StateChanged;

    event EventHandler<BlockedLaunchNotice>? LaunchBlocked;

    event EventHandler<SessionSummary>? Completed;
}
