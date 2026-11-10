using FocusLock.Core.Sessions;

namespace FocusLock.Core.Settings;

public interface IStatsStore
{
    void Append(SessionSummary summary);

    /// <summary>Total focused time of sessions that ended in [<paramref name="from"/>, <paramref name="to"/>).</summary>
    TimeSpan GetFocusedTime(DateTimeOffset from, DateTimeOffset to);

    IReadOnlyList<SessionRecord> GetSessions();
}
