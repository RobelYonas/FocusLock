using FocusLock.Core.Abstractions;
using FocusLock.Core.Detection;
using FocusLock.Core.Detection.Epic;
using FocusLock.Tests.Fakes;
using Microsoft.Extensions.Logging;

namespace FocusLock.Tests.Detection;

public class EpicGameDetectorTests
{
    private const string ManifestDirectory = @"C:\ProgramData\Epic\EpicGamesLauncher\Data\Manifests";

    private readonly FakeFileSystem _fileSystem = new();
    private readonly FakeRegistry _registry = new();
    private readonly FakeAppEnvironment _environment = new();
    private readonly ListLogger<EpicGameDetector> _logger = new();

    [Fact]
    public async Task Finds_installed_games_and_normalizes_paths()
    {
        SetUpTypicalInstall();

        var games = await Detect();

        Assert.Equal(2, games.Count);

        var rocketLeague = Assert.Single(games, game => game.Id == "epic:Sugar");
        Assert.Equal("Rocket League®", rocketLeague.Name);
        Assert.Equal(@"C:\Program Files\Epic Games\rocketleague", rocketLeague.Target);
        Assert.Equal(MatchKind.Folder, rocketLeague.Kind);
        Assert.Equal(GameSource.Epic, rocketLeague.Source);

        var fortnite = Assert.Single(games, game => game.Id == "epic:Fortnite");
        Assert.Equal(@"D:\Fortnite\Fortnite", fortnite.Target);
    }

    [Fact]
    public async Task Skips_add_ons_incomplete_installs_and_non_games()
    {
        SetUpTypicalInstall();

        var games = await Detect();

        Assert.DoesNotContain(games, game => game.Id == "epic:RL-DLC");
        Assert.DoesNotContain(games, game => game.Id == "epic:Half");
        Assert.DoesNotContain(games, game => game.Id == "epic:UE_5.4");
    }

    [Fact]
    public async Task Logs_and_skips_invalid_manifest()
    {
        SetUpTypicalInstall();
        _fileSystem.AddFile($@"{ManifestDirectory}\broken.item", "{ not json");

        var games = await Detect();

        Assert.Equal(2, games.Count);
        Assert.True(_logger.Has(LogLevel.Warning, "unreadable"));
    }

    [Fact]
    public async Task Uses_app_data_path_from_registry_when_present()
    {
        _registry.Set(RegistryRoot.LocalMachine, @"SOFTWARE\Epic Games\EpicGamesLauncher", "AppDataPath", @"D:\EpicData\");
        _fileSystem.AddFile(@"D:\EpicData\Manifests\a.item", Manifest("Fortnite", "Fortnite", @"D:\\Fortnite\\Fortnite"));

        var games = await Detect();

        Assert.Single(games, game => game.Id == "epic:Fortnite");
    }

    [Fact]
    public async Task Returns_nothing_when_launcher_is_not_installed()
    {
        var games = await Detect();

        Assert.Empty(games);
        Assert.True(_logger.Has(LogLevel.Information, "not found"));
    }

    [Fact]
    public async Task Falls_back_to_app_name_when_display_name_is_missing()
    {
        _fileSystem.AddFile($@"{ManifestDirectory}\a.item", """{ "AppName": "Mystery", "InstallLocation": "C:\\Games\\Mystery" }""");

        var games = await Detect();

        Assert.Equal("Mystery", Assert.Single(games).Name);
    }

    private async Task<IReadOnlyList<DetectedGame>> Detect()
    {
        var detector = new EpicGameDetector(_registry, _fileSystem, _environment, _logger);
        return await detector.DetectAsync(CancellationToken.None);
    }

    private void SetUpTypicalInstall()
    {
        _fileSystem.AddFile($@"{ManifestDirectory}\rocket.item", Manifest("Rocket League®", "Sugar", "C:/Program Files/Epic Games/rocketleague"));
        _fileSystem.AddFile($@"{ManifestDirectory}\fortnite.item", Manifest("Fortnite", "Fortnite", @"D:\\Fortnite\\Fortnite"));
        _fileSystem.AddFile($@"{ManifestDirectory}\dlc.item", Manifest("Rocket League DLC", "RL-DLC", "C:/Program Files/Epic Games/rocketleague", mainGameAppName: "Sugar"));
        _fileSystem.AddFile($@"{ManifestDirectory}\half.item", Manifest("Half Installed", "Half", "C:/Games/Half", incomplete: true));
        _fileSystem.AddFile($@"{ManifestDirectory}\ue.item", Manifest("Unreal Engine", "UE_5.4", "C:/Program Files/Epic Games/UE_5.4", categories: """["public", "applications"]"""));
    }

    private static string Manifest(
        string displayName,
        string appName,
        string installLocation,
        string? mainGameAppName = null,
        bool incomplete = false,
        string categories = """["public", "games", "applications"]""") => $$"""
        {
            "FormatVersion": 0,
            "bIsIncompleteInstall": {{(incomplete ? "true" : "false")}},
            "AppCategories": {{categories}},
            "DisplayName": "{{displayName}}",
            "AppName": "{{appName}}",
            "MainGameAppName": "{{mainGameAppName ?? appName}}",
            "InstallLocation": "{{installLocation}}",
            "LaunchExecutable": "Binaries/Win64/Game.exe"
        }
        """;
}
