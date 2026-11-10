using FocusLock.Core.Detection;
using FocusLock.Core.Settings;
using FocusLock.Tests.Fakes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FocusLock.Tests.Detection;

public class GameCatalogTests
{
    private static readonly DetectedGame Portal = new("steam:620", "Portal 2", GameSource.Steam, @"C:\Steam\steamapps\common\Portal 2", MatchKind.Folder);
    private static readonly DetectedGame Valheim = new("steam:892970", "Valheim", GameSource.Steam, @"D:\SteamLibrary\steamapps\common\Valheim", MatchKind.Folder);
    private static readonly DetectedGame Steam = new("launcher:steam.exe", "Steam", GameSource.Launcher, "steam.exe", MatchKind.ProcessName);

    [Fact]
    public async Task Refresh_applies_disabled_ids_and_launcher_setting()
    {
        var settings = new AppSettings { DisabledGameIds = ["STEAM:620"], BlockLaunchers = false };
        var catalog = Catalog(settings, new StubDetector(GameSource.Steam, Portal, Valheim), new StubDetector(GameSource.Launcher, Steam));

        await catalog.RefreshAsync();

        Assert.False(Entry(catalog, Portal.Id).IsEnabled);
        Assert.True(Entry(catalog, Valheim.Id).IsEnabled);
        Assert.False(Entry(catalog, Steam.Id).IsEnabled);
        Assert.Equal(1, catalog.BlockedGameCount);
    }

    [Fact]
    public async Task SetEnabled_persists_the_choice_and_updates_entries()
    {
        var store = new FakeSettingsStore(new AppSettings { DisabledGameIds = ["steam:620"] });
        var catalog = new GameCatalog([new StubDetector(GameSource.Steam, Portal, Valheim)], new SettingsService(store, NullLogger<SettingsService>.Instance), NullLogger<GameCatalog>.Instance);
        await catalog.RefreshAsync();

        catalog.SetEnabled(Portal.Id, true);
        Assert.True(Entry(catalog, Portal.Id).IsEnabled);
        Assert.Empty(store.Settings.DisabledGameIds);
        Assert.Equal(1, store.SaveCount);

        catalog.SetEnabled(Valheim.Id, false);
        catalog.SetEnabled(Valheim.Id, false);
        Assert.False(Entry(catalog, Valheim.Id).IsEnabled);
        Assert.Equal(["steam:892970"], store.Settings.DisabledGameIds);
        Assert.Equal(0, catalog.BlockedGameCount - 1);
    }

    [Fact]
    public async Task SetLaunchersEnabled_updates_setting_and_all_launcher_entries()
    {
        var store = new FakeSettingsStore();
        var catalog = new GameCatalog([new StubDetector(GameSource.Launcher, Steam)], new SettingsService(store, NullLogger<SettingsService>.Instance), NullLogger<GameCatalog>.Instance);
        await catalog.RefreshAsync();
        Assert.False(Entry(catalog, Steam.Id).IsEnabled);

        catalog.SetLaunchersEnabled(true);

        Assert.True(store.Settings.BlockLaunchers);
        Assert.True(Entry(catalog, Steam.Id).IsEnabled);
        Assert.Equal(0, catalog.BlockedGameCount);
    }

    [Fact]
    public async Task A_failing_detector_is_logged_and_the_others_still_count()
    {
        var logger = new ListLogger<GameCatalog>();
        var catalog = new GameCatalog(
            [new ThrowingDetector(), new StubDetector(GameSource.Steam, Portal)],
            TestSettings.Service(),
            logger);

        await catalog.RefreshAsync();

        Assert.Single(catalog.Entries);
        Assert.True(logger.Has(LogLevel.Error, "detector failed"));
    }

    [Fact]
    public async Task Refresh_raises_changed()
    {
        var catalog = Catalog(new AppSettings(), new StubDetector(GameSource.Steam, Portal));
        var raised = 0;
        catalog.Changed += (_, _) => raised++;

        await catalog.RefreshAsync();

        Assert.Equal(1, raised);
    }

    private static GameCatalog Catalog(AppSettings settings, params IGameDetector[] detectors) =>
        new(detectors, TestSettings.Service(settings), NullLogger<GameCatalog>.Instance);

    private static CatalogEntry Entry(GameCatalog catalog, string id) => catalog.Entries.Single(entry => entry.Game.Id == id);

    private sealed class StubDetector(GameSource source, params DetectedGame[] games) : IGameDetector
    {
        public GameSource Source => source;

        public Task<IReadOnlyList<DetectedGame>> DetectAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<DetectedGame>>(games);
    }

    private sealed class ThrowingDetector : IGameDetector
    {
        public GameSource Source => GameSource.Epic;

        public Task<IReadOnlyList<DetectedGame>> DetectAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("boom");
    }
}
