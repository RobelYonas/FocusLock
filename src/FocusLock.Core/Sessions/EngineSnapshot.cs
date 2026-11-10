namespace FocusLock.Core.Sessions;

/// <summary>Where a session is at one moment in time, as computed by <see cref="FocusSessionEngine"/>.</summary>
public sealed record EngineSnapshot(
    SessionPhase Phase,
    int Cycle,
    int TotalCycles,
    TimeSpan RemainingInPhase,
    TimeSpan RemainingTotal,
    double PhaseProgress,
    TimeSpan FocusedTime,
    bool IsFinished,
    bool PhaseChanged);
