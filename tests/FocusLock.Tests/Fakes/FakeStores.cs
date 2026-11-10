using FocusLock.Core.Sessions;
using FocusLock.Core.Settings;
using Microsoft.Extensions.Logging.Abstractions;

namespace FocusLock.Tests.Fakes;

public sealed class FakeSettingsStore(AppSettings? settings = null) : ISettingsStore
{
    public AppSettings Settings { get; private set; } = settings ?? new AppSettings();

    public int SaveCount { get; private set; }

    public AppSettings Load() => Settings;

    public void Save(AppSettings settings)
    {
        Settings = settings;
        SaveCount++;
    }
}

public sealed class FakeStatsStore : IStatsStore
{
    public List<SessionSummary> Appended { get; } = [];

    public bool ThrowOnAppend { get; set; }

    public void Append(SessionSummary summary)
    {
        if (ThrowOnAppend)
        {
            throw new IOException("Disk full.");
        }

        Appended.Add(summary);
    }

    public TimeSpan GetFocusedTime(DateTimeOffset from, DateTimeOffset to) =>
        TimeSpan.FromTicks(Appended.Where(summary => summary.EndedAt >= from && summary.EndedAt < to).Sum(summary => summary.FocusedTime.Ticks));

    public IReadOnlyList<SessionRecord> GetSessions() => [];
}

public static class TestSettings
{
    public static SettingsService Service(AppSettings? settings = null) =>
        new(new FakeSettingsStore(settings), NullLogger<SettingsService>.Instance);
}
