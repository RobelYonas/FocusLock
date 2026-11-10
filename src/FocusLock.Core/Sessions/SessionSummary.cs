namespace FocusLock.Core.Sessions;

public sealed record SessionSummary(
    TimeSpan FocusedTime,
    int BlockedAttempts,
    bool EndedEarly,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt);
