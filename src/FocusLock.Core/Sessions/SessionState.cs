namespace FocusLock.Core.Sessions;

/// <summary>What the UI shows while a session runs. Plain data so it can be sent over a pipe later.</summary>
public sealed record SessionState(
    bool IsRunning,
    SessionPhase Phase,
    int Cycle,
    int TotalCycles,
    TimeSpan RemainingInPhase,
    TimeSpan RemainingTotal,
    double PhaseProgress,
    int BlockedAttempts)
{
    public static SessionState Idle { get; } = new(false, SessionPhase.Focus, 0, 0, TimeSpan.Zero, TimeSpan.Zero, 0d, 0);
}
