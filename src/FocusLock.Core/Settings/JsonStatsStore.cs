using FocusLock.Core.Abstractions;
using FocusLock.Core.Sessions;
using Microsoft.Extensions.Logging;

namespace FocusLock.Core.Settings;

public sealed class JsonStatsStore : IStatsStore
{
    private const int MaxRecords = 2000;

    private readonly JsonFile _file;
    private readonly object _gate = new();

    public JsonStatsStore(IFileSystem fileSystem, IAppEnvironment environment, ILogger<JsonStatsStore> logger)
    {
        _file = new JsonFile(fileSystem, logger);
        FilePath = System.IO.Path.Combine(environment.DataDirectory, "stats.json");
    }

    public string FilePath { get; }

    public void Append(SessionSummary summary)
    {
        lock (_gate)
        {
            var data = Load();
            data.Sessions.Add(new SessionRecord
            {
                StartedAt = summary.StartedAt,
                EndedAt = summary.EndedAt,
                FocusedSeconds = (int)Math.Round(summary.FocusedTime.TotalSeconds),
                BlockedAttempts = summary.BlockedAttempts,
                EndedEarly = summary.EndedEarly,
            });

            if (data.Sessions.Count > MaxRecords)
            {
                data.Sessions.RemoveRange(0, data.Sessions.Count - MaxRecords);
            }

            _file.Save(FilePath, data, FocusLockJsonContext.Default.StatsData);
        }
    }

    public TimeSpan GetFocusedTime(DateTimeOffset from, DateTimeOffset to)
    {
        lock (_gate)
        {
            var seconds = Load().Sessions
                .Where(session => session.EndedAt >= from && session.EndedAt < to)
                .Sum(session => (long)session.FocusedSeconds);
            return TimeSpan.FromSeconds(seconds);
        }
    }

    public IReadOnlyList<SessionRecord> GetSessions()
    {
        lock (_gate)
        {
            return Load().Sessions;
        }
    }

    private StatsData Load() => _file.Load(FilePath, FocusLockJsonContext.Default.StatsData, () => new StatsData());
}
