using FocusLock.Core.Abstractions;
using FocusLock.Core.Detection;
using FocusLock.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace FocusLock.Tests.Detection;

public class PublisherGameDetectorTests
{
    private const string Uninstall64 = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
    private const string Uninstall32 = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall";

    private readonly FakeRegistry _registry = new();

    [Fact]
    public async Task BattleNet_finds_Blizzard_games_but_not_the_launcher_itself()
    {
        AddProgram(RegistryRoot.LocalMachine, Uninstall32, "Battle.net", "Battle.net", "Blizzard Entertainment", @"C:\Program Files (x86)\Battle.net");
        AddProgram(RegistryRoot.LocalMachine, Uninstall32, "Overwatch", "Overwatch", "Blizzard Entertainment", @"C:\Program Files (x86)\Overwatch\");
        AddProgram(RegistryRoot.CurrentUser, Uninstall64, "Diablo IV", "Diablo IV", "Blizzard Entertainment, Inc.", @"D:\Games\Diablo IV");

        var games = await Detect(PublisherGameDetector.BattleNet);

        Assert.Equal(2, games.Count);
        var overwatch = Assert.Single(games, game => game.Name == "Overwatch");
        Assert.Equal("battlenet:Overwatch", overwatch.Id);
        Assert.Equal(GameSource.BattleNet, overwatch.Source);
        Assert.Equal(@"C:\Program Files (x86)\Overwatch", overwatch.Target);
        Assert.Equal(MatchKind.Folder, overwatch.Kind);
        Assert.Contains(games, game => game.Name == "Diablo IV");
    }

    [Fact]
    public async Task EA_finds_EA_games_but_not_the_EA_app()
    {
        AddProgram(RegistryRoot.LocalMachine, Uninstall64, "{C2622085}", "EA app", "Electronic Arts", @"C:\Program Files\Electronic Arts\EA Desktop");
        AddProgram(RegistryRoot.LocalMachine, Uninstall32, "{BF2042}", "Battlefield™ 2042", "Electronic Arts", @"D:\EA Games\Battlefield 2042");

        var game = Assert.Single(await Detect(PublisherGameDetector.Ea));

        Assert.Equal("ea:{BF2042}", game.Id);
        Assert.Equal("Battlefield™ 2042", game.Name);
        Assert.Equal(GameSource.Ea, game.Source);
        Assert.Equal(@"D:\EA Games\Battlefield 2042", game.Target);
    }

    [Fact]
    public async Task Skips_Steam_copies_entries_without_a_folder_other_publishers_and_duplicates()
    {
        // Steam already covers its own games; a second entry would ignore the user's Steam toggle.
        AddProgram(RegistryRoot.LocalMachine, Uninstall64, "Steam App 2357570", "Overwatch®", "Blizzard Entertainment, Inc.", @"C:\Steam\steamapps\common\Overwatch");
        AddProgram(RegistryRoot.LocalMachine, Uninstall32, "No Folder", "No Folder", "Blizzard Entertainment", "");
        AddProgram(RegistryRoot.LocalMachine, Uninstall32, "Uplay", "Ubisoft Connect", "Ubisoft", @"C:\Program Files (x86)\Ubisoft");
        AddProgram(RegistryRoot.LocalMachine, Uninstall32, "Hearthstone", "Hearthstone", "Blizzard Entertainment", @"C:\Games\Hearthstone");
        AddProgram(RegistryRoot.CurrentUser, Uninstall64, "Hearthstone", "Hearthstone", "Blizzard Entertainment", @"C:\Games\Hearthstone\");

        var game = Assert.Single(await Detect(PublisherGameDetector.BattleNet));

        Assert.Equal("Hearthstone", game.Name);
    }

    [Fact]
    public async Task Returns_nothing_when_no_programs_are_registered()
    {
        Assert.Empty(await Detect(PublisherGameDetector.Ea));
    }

    private void AddProgram(RegistryRoot root, string uninstallKey, string keyName, string name, string publisher, string location) =>
        _registry
            .Set(root, $@"{uninstallKey}\{keyName}", "DisplayName", name)
            .Set(root, $@"{uninstallKey}\{keyName}", "Publisher", publisher)
            .Set(root, $@"{uninstallKey}\{keyName}", "InstallLocation", location);

    private Task<IReadOnlyList<DetectedGame>> Detect(Func<IRegistry, Microsoft.Extensions.Logging.ILogger<PublisherGameDetector>, PublisherGameDetector> create) =>
        create(_registry, NullLogger<PublisherGameDetector>.Instance).DetectAsync(CancellationToken.None);
}
