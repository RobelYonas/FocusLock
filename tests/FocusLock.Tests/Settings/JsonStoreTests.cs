using FocusLock.Core.Sessions;
using FocusLock.Core.Settings;
using FocusLock.Tests.Fakes;
using Microsoft.Extensions.Logging;

namespace FocusLock.Tests.Settings;

public class JsonSettingsStoreTests
{
    private readonly FakeFileSystem _fileSystem = new();
    private readonly FakeAppEnvironment _environment = new();
    private readonly ListLogger<JsonSettingsStore> _logger = new();

    [Fact]
    public void Load_returns_defaults_when_there_is_no_file()
    {
        var settings = Store().Load();

        Assert.Equal(25, settings.DefaultMinutes);
        Assert.Equal([25, 50, 60], settings.PresetMinutes);
        Assert.True(settings.CloseAlreadyRunningGames);
        Assert.False(settings.StrictMode);
        Assert.Empty(settings.CustomBlocklist);
    }

    [Fact]
    public void Saved_settings_round_trip()
    {
        var settings = new AppSettings
        {
            DefaultMinutes = 50,
            BlockLaunchers = true,
            StrictMode = true,
            DisabledGameIds = ["steam:620"],
            CustomBlocklist = [new CustomEntry { Path = @"C:\Games\x.exe" }],
            Allowlist = [new CustomEntry { Path = @"C:\Tools", IsFolder = true }],
            Pomodoro = new PomodoroSettings { Enabled = true, BreakMinutes = 10, Cycles = 3 },
        };

        Store().Save(settings);
        var loaded = Store().Load();

        Assert.True(_fileSystem.FileExists(@"C:\Users\test\AppData\Roaming\FocusLock\settings.json"));
        Assert.False(_fileSystem.FileExists(@"C:\Users\test\AppData\Roaming\FocusLock\settings.json.tmp"));
        Assert.Equal(50, loaded.DefaultMinutes);
        Assert.True(loaded.BlockLaunchers);
        Assert.True(loaded.StrictMode);
        Assert.Equal(["steam:620"], loaded.DisabledGameIds);
        Assert.Equal(@"C:\Games\x.exe", Assert.Single(loaded.CustomBlocklist).Path);
        Assert.True(Assert.Single(loaded.Allowlist).IsFolder);
        Assert.Equal(10, loaded.Pomodoro.BreakMinutes);
        Assert.Equal(3, loaded.Pomodoro.Cycles);
    }

    [Fact]
    public void A_corrupt_file_is_moved_aside_and_defaults_are_used()
    {
        _fileSystem.AddFile(@"C:\Users\test\AppData\Roaming\FocusLock\settings.json", "{ this is not json");

        var settings = Store().Load();

        Assert.Equal(25, settings.DefaultMinutes);
        Assert.True(_fileSystem.FileExists(@"C:\Users\test\AppData\Roaming\FocusLock\settings.json.bak"));
        Assert.False(_fileSystem.FileExists(@"C:\Users\test\AppData\Roaming\FocusLock\settings.json"));
        Assert.True(_logger.Has(LogLevel.Warning, "could not be read"));
    }

    [Fact]
    public void Unknown_properties_are_ignored_and_missing_ones_keep_defaults()
    {
        _fileSystem.AddFile(@"C:\Users\test\AppData\Roaming\FocusLock\settings.json", """{ "defaultMinutes": 60, "futureSetting": true }""");

        var settings = Store().Load();

        Assert.Equal(60, settings.DefaultMinutes);
        Assert.True(settings.ShowNotifications);
    }

    private JsonSettingsStore Store() => new(_fileSystem, _environment, _logger);
}

public class JsonStatsStoreTests
{
    private readonly FakeFileSystem _fileSystem = new();
    private readonly FakeAppEnvironment _environment = new();

    [Fact]
    public void Appended_sessions_are_persisted_and_summed_by_end_time()
    {
        var day = new DateTimeOffset(2026, 11, 10, 0, 0, 0, TimeSpan.FromHours(1));
        var store = Store();
        store.Append(new SessionSummary(TimeSpan.FromMinutes(25), 2, false, day.AddHours(9), day.AddHours(9).AddMinutes(25)));
        store.Append(new SessionSummary(TimeSpan.FromMinutes(10), 0, true, day.AddHours(14), day.AddHours(14).AddMinutes(10)));
        store.Append(new SessionSummary(TimeSpan.FromMinutes(50), 1, false, day.AddDays(-1), day.AddDays(-1).AddMinutes(50)));

        var reopened = Store();

        Assert.Equal(3, reopened.GetSessions().Count);
        Assert.Equal(TimeSpan.FromMinutes(35), reopened.GetFocusedTime(day, day.AddDays(1)));
        Assert.Equal(TimeSpan.FromMinutes(85), reopened.GetFocusedTime(day.AddDays(-1), day.AddDays(1)));
        var record = reopened.GetSessions()[1];
        Assert.True(record.EndedEarly);
        Assert.Equal(600, record.FocusedSeconds);
    }

    [Fact]
    public void A_corrupt_stats_file_does_not_prevent_new_records()
    {
        _fileSystem.AddFile(@"C:\Users\test\AppData\Roaming\FocusLock\stats.json", "garbage");
        var store = Store();

        store.Append(new SessionSummary(TimeSpan.FromMinutes(5), 0, false, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddMinutes(5)));

        Assert.Single(store.GetSessions());
        Assert.True(_fileSystem.FileExists(@"C:\Users\test\AppData\Roaming\FocusLock\stats.json.bak"));
    }

    private JsonStatsStore Store() => new(_fileSystem, _environment, new ListLogger<JsonStatsStore>());
}
