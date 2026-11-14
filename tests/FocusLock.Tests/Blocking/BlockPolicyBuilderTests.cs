using FocusLock.Core.Blocking;
using FocusLock.Core.Detection;
using FocusLock.Core.Settings;
using FocusLock.Core.Websites;
using FocusLock.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace FocusLock.Tests.Blocking;

public class BlockPolicyBuilderTests
{
    private readonly FakeFileSystem _fileSystem = new();
    private readonly FakeSitePresetCatalog _sitePresets = new(
        new SitePreset("youtube", "YouTube", ["youtube.com", "www.youtube.com", "YouTu.be"]),
        new SitePreset("reddit", "Reddit", ["reddit.com"]));

    [Fact]
    public void Includes_only_enabled_entries_and_collects_executable_names_for_folders()
    {
        _fileSystem.AddFile(@"C:\Games\Foo\foo.exe");
        _fileSystem.AddFile(@"C:\Games\Foo\readme.txt");
        _fileSystem.AddFile(@"C:\Games\Foo\bin\Helper.exe");
        _fileSystem.AddFile(@"C:\Games\Foo\a\b\c\deep.exe");
        _fileSystem.AddFile(@"C:\Games\Foo\a\b\c\d\too-deep.exe");

        var entries = new List<CatalogEntry>
        {
            new(new DetectedGame("steam:1", "Foo", GameSource.Steam, @"C:\Games\Foo", MatchKind.Folder), true),
            new(new DetectedGame("steam:2", "Bar", GameSource.Steam, @"C:\Games\Bar", MatchKind.Folder), false),
            new(new DetectedGame("launcher:steam.exe", "Steam", GameSource.Launcher, "steam.exe", MatchKind.ProcessName), true),
            new(new DetectedGame("custom:x", "x", GameSource.Custom, @"C:\Apps\x.exe", MatchKind.Executable), true),
        };

        var policy = Build(entries, new AppSettings());

        Assert.Equal(3, policy.Rules.Count);
        var foo = Assert.Single(policy.Rules, rule => rule.Name == "Foo");
        Assert.Equal(MatchKind.Folder, foo.Kind);
        Assert.NotNull(foo.ExecutableNames);
        Assert.Equal(["deep.exe", "foo.exe", "Helper.exe"], foo.ExecutableNames.OrderBy(name => name, StringComparer.OrdinalIgnoreCase));
        Assert.DoesNotContain(policy.Rules, rule => rule.Name == "Bar");
        Assert.Null(Assert.Single(policy.Rules, rule => rule.Name == "Steam").ExecutableNames);
        Assert.Null(Assert.Single(policy.Rules, rule => rule.Name == "x").ExecutableNames);
    }

    [Fact]
    public void Missing_folder_yields_empty_executable_names()
    {
        var entries = new List<CatalogEntry>
        {
            new(new DetectedGame("steam:1", "Gone", GameSource.Steam, @"C:\Games\Gone", MatchKind.Folder), true),
        };

        var policy = Build(entries, new AppSettings());

        Assert.Empty(Assert.Single(policy.Rules).ExecutableNames!);
    }

    [Fact]
    public void Task_manager_is_blocked_only_when_committed_to_the_timer()
    {
        Assert.False(Build([], new AppSettings()).BlockTaskManager);
        Assert.True(Build([], new AppSettings { CommitToTimer = true }).BlockTaskManager);
    }

    [Fact]
    public void Allowlist_entries_become_allow_rules()
    {
        var settings = new AppSettings
        {
            Allowlist =
            [
                new CustomEntry { Path = @"C:\Tools\notepad.exe" },
                new CustomEntry { Path = @"C:\Games\Foo\Tools", IsFolder = true },
                new CustomEntry { Path = string.Empty },
            ],
        };

        var policy = Build([], settings);

        Assert.Empty(policy.Rules);
        Assert.Equal(2, policy.Allow.Count);
        Assert.Contains(policy.Allow, rule => rule.Kind == MatchKind.Executable && rule.Target == @"C:\Tools\notepad.exe");
        Assert.Contains(policy.Allow, rule => rule.Kind == MatchKind.Folder && rule.Target == @"C:\Games\Foo\Tools");
    }

    [Fact]
    public async Task BuildFreshAsync_scans_again_so_games_installed_since_the_last_scan_are_blocked()
    {
        var detector = new ChangingDetector();
        var catalog = new GameCatalog([detector], TestSettings.Service(), NullLogger<GameCatalog>.Instance);
        await catalog.RefreshAsync();
        detector.Games.Add(new DetectedGame("steam:9", "New Game", GameSource.Steam, @"C:\Games\New", MatchKind.Folder));

        var policy = await new BlockPolicyBuilder(_fileSystem, _sitePresets, NullLogger<BlockPolicyBuilder>.Instance).BuildFreshAsync(catalog, new AppSettings());

        Assert.Equal("New Game", Assert.Single(policy.Rules).Name);
        Assert.Single(catalog.Entries);
    }

    [Fact]
    public void No_sites_selected_means_no_blocked_domains()
    {
        Assert.Empty(Build([], new AppSettings()).BlockedDomains);
    }

    [Fact]
    public void Selected_presets_contribute_their_domains_and_unknown_ids_are_ignored()
    {
        var settings = new AppSettings { BlockedSitePresetIds = ["youtube", "gone"] };

        var policy = Build([], settings);

        Assert.Equal(["youtube.com", "www.youtube.com", "youtu.be"], policy.BlockedDomains);
    }

    [Fact]
    public void Custom_domains_get_variants_and_everything_is_lowercased_deduped_and_valid()
    {
        var settings = new AppSettings
        {
            BlockedSitePresetIds = ["youtube"],
            CustomBlockedDomains = ["https://www.YouTube.com/feed", "Example.org", "not a domain", "localhost"],
        };

        var policy = Build([], settings);

        Assert.Equal(
            ["youtube.com", "www.youtube.com", "youtu.be", "m.youtube.com", "example.org", "www.example.org", "m.example.org"],
            policy.BlockedDomains);
    }

    [Fact]
    public void Invalid_preset_domains_are_dropped()
    {
        var builder = new BlockPolicyBuilder(
            _fileSystem,
            new FakeSitePresetCatalog(new SitePreset("bad", "Bad", ["Good.COM", "bad domain", "http://x.com/"])),
            NullLogger<BlockPolicyBuilder>.Instance);

        var policy = builder.Build([], new AppSettings { BlockedSitePresetIds = ["bad"] });

        Assert.Equal(["good.com"], policy.BlockedDomains);
    }

    private BlockPolicy Build(IReadOnlyList<CatalogEntry> entries, AppSettings settings) =>
        new BlockPolicyBuilder(_fileSystem, _sitePresets, NullLogger<BlockPolicyBuilder>.Instance).Build(entries, settings);

    /// <summary>A detector whose results can change between scans, like a game being installed.</summary>
    private sealed class ChangingDetector : IGameDetector
    {
        public List<DetectedGame> Games { get; } = [];

        public GameSource Source => GameSource.Steam;

        public Task<IReadOnlyList<DetectedGame>> DetectAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<DetectedGame>>(Games.ToList());
    }
}
