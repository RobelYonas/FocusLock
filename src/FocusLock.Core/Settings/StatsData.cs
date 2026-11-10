namespace FocusLock.Core.Settings;

/// <summary>Session history, stored as JSON in %AppData%\FocusLock\stats.json.</summary>
public sealed class StatsData
{
    public List<SessionRecord> Sessions { get; set; } = [];
}

public sealed class SessionRecord
{
    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset EndedAt { get; set; }

    public int FocusedSeconds { get; set; }

    public int BlockedAttempts { get; set; }

    public bool EndedEarly { get; set; }
}
