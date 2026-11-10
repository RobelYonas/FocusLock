namespace FocusLock.Core.Sessions;

/// <summary>One stretch of a session: a focus block or a break, and which cycle it belongs to.</summary>
public sealed record SessionSegment(SessionPhase Phase, TimeSpan Length, int Cycle);
