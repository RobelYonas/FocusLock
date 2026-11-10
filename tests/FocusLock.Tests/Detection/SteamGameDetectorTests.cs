using FocusLock.Core.Abstractions;
using FocusLock.Core.Detection;
using FocusLock.Core.Detection.Steam;
using FocusLock.Tests.Fakes;
using Microsoft.Extensions.Logging;

namespace FocusLock.Tests.Detection;

public class SteamGameDetectorTests
{
    private const string SteamRoot = @"C:\Program Files (x86)\Steam";
    private const string SecondLibrary = @"D:\SteamLibrary";

    private readonly FakeFileSystem _fileSystem = new();
    private readonly FakeRegistry _registry = new();
    private readonly FakeAppEnvironment _environment = new();
    private readonly ListLogger<SteamGameDetector> _logger = new();

    [Fact]
    public async Task Finds_games_in_every_library()
    {
        SetUpTypicalInstall();

        var games = await Detect();

        var portal = Assert.Single(games, game => game.Id == "steam:620");
        Assert.Equal("Portal 2", portal.Name);
        Assert.Equal(@"C:\Program Files (x86)\Steam\steamapps\common\Portal 2", portal.Target);
        Assert.Equal(MatchKind.Folder, portal.Kind);
        Assert.Equal(GameSource.Steam, portal.Source);

        var valheim = Assert.Single(games, game => game.Id == "steam:892970");
        Assert.Equal(@"D:\SteamLibrary\steamapps\common\Valheim", valheim.Target);
    }

    [Fact]
    public async Task Skips_steamworks_redistributables_including_its_folder()
    {
        SetUpTypicalInstall();

        var games = await Detect();

        Assert.DoesNotContain(games, game => game.Id == "steam:228980");
        Assert.DoesNotContain(games, game => game.Target.EndsWith("Steamworks Shared", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Includes_folders_under_common_that_have_no_manifest()
    {
        SetUpTypicalInstall();

        var games = await Detect();

        var leftover = Assert.Single(games, game => game.Name == "Leftover Game");
        Assert.StartsWith("steam:folder:", leftover.Id, StringComparison.Ordinal);
        Assert.Equal(@"C:\Program Files (x86)\Steam\steamapps\common\Leftover Game", leftover.Target);
    }

    [Fact]
    public async Task Skips_libraries_that_are_not_available()
    {
        SetUpTypicalInstall();

        var games = await Detect();

        Assert.DoesNotContain(games, game => game.Target.StartsWith("E:", StringComparison.OrdinalIgnoreCase));
        Assert.True(_logger.Has(LogLevel.Information, "not available"));
    }

    [Fact]
    public async Task Falls_back_to_program_files_when_registry_has_no_entry()
    {
        SetUpTypicalInstall(registerInRegistry: false);

        var games = await Detect();

        Assert.Contains(games, game => game.Id == "steam:620");
    }

    [Fact]
    public async Task Falls_back_to_the_per_user_registry_entry()
    {
        SetUpTypicalInstall(registerInRegistry: false);
        _registry.Set(RegistryRoot.CurrentUser, @"Software\Valve\Steam", "SteamPath", "c:/program files (x86)/steam");

        var games = await Detect();

        var portal = Assert.Single(games, game => game.Id == "steam:620");
        Assert.Equal(@"C:\Program Files (x86)\Steam\steamapps\common\Portal 2", portal.Target, ignoreCase: true);
    }

    [Fact]
    public async Task Returns_nothing_when_steam_is_not_installed()
    {
        var games = await Detect();

        Assert.Empty(games);
        Assert.True(_logger.Has(LogLevel.Information, "not found"));
    }

    [Fact]
    public async Task Understands_the_old_library_file_format()
    {
        _registry.Set(RegistryRoot.CurrentUser, @"Software\Valve\Steam", "SteamPath", "c:/program files (x86)/steam");
        _fileSystem.AddFile($@"{SteamRoot}\steamapps\libraryfolders.vdf", """
            "LibraryFolders"
            {
                "TimeNextStatsReport"		"1"
                "1"		"D:\\SteamLibrary"
            }
            """);
        _fileSystem.AddFile($@"{SecondLibrary}\steamapps\appmanifest_892970.acf", Manifest("892970", "Valheim", "Valheim"));

        var games = await Detect();

        Assert.Single(games, game => game.Id == "steam:892970");
    }

    [Fact]
    public async Task Unreadable_manifest_is_skipped_and_logged()
    {
        SetUpTypicalInstall();
        _fileSystem.UnreadablePaths.Add($@"{SteamRoot}\steamapps\appmanifest_620.acf");

        var games = await Detect();

        Assert.DoesNotContain(games, game => game.Id == "steam:620");
        Assert.Contains(games, game => game.Id == "steam:892970");
        Assert.True(_logger.Has(LogLevel.Warning, "manifest"));
    }

    [Fact]
    public async Task Uses_install_folder_name_when_manifest_has_no_name()
    {
        _registry.Set(RegistryRoot.CurrentUser, @"Software\Valve\Steam", "SteamPath", SteamRoot);
        _fileSystem.AddDirectory(SteamRoot);
        _fileSystem.AddFile($@"{SteamRoot}\steamapps\appmanifest_1.acf", """
            "AppState"
            {
                "appid"		"1"
                "installdir"		"Nameless"
            }
            """);

        var games = await Detect();

        Assert.Equal("Nameless", Assert.Single(games).Name);
    }

    private async Task<IReadOnlyList<DetectedGame>> Detect()
    {
        var detector = new SteamGameDetector(_registry, _fileSystem, _environment, _logger);
        return await detector.DetectAsync(CancellationToken.None);
    }

    private void SetUpTypicalInstall(bool registerInRegistry = true)
    {
        if (registerInRegistry)
        {
            _registry.Set(RegistryRoot.LocalMachine, @"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath", SteamRoot);
            _registry.Set(RegistryRoot.CurrentUser, @"Software\Valve\Steam", "SteamPath", "c:/program files (x86)/steam");
        }

        _fileSystem.AddFile($@"{SteamRoot}\steamapps\libraryfolders.vdf", """
            "libraryfolders"
            {
                "0"
                {
                    "path"		"C:\\Program Files (x86)\\Steam"
                    "label"		""
                }
                "1"
                {
                    "path"		"D:\\SteamLibrary"
                }
                "2"
                {
                    "path"		"E:\\Unplugged"
                }
            }
            """);

        _fileSystem.AddFile($@"{SteamRoot}\steamapps\appmanifest_620.acf", Manifest("620", "Portal 2", "Portal 2"));
        _fileSystem.AddFile($@"{SteamRoot}\steamapps\appmanifest_228980.acf", Manifest("228980", "Steamworks Common Redistributables", "Steamworks Shared"));
        _fileSystem.AddDirectory($@"{SteamRoot}\steamapps\common\Portal 2");
        _fileSystem.AddDirectory($@"{SteamRoot}\steamapps\common\Steamworks Shared");
        _fileSystem.AddDirectory($@"{SteamRoot}\steamapps\common\Leftover Game");

        _fileSystem.AddFile($@"{SecondLibrary}\steamapps\appmanifest_892970.acf", Manifest("892970", "Valheim", "Valheim"));
        _fileSystem.AddDirectory($@"{SecondLibrary}\steamapps\common\Valheim");
    }

    private static string Manifest(string appId, string name, string installDir) => $$"""
        "AppState"
        {
            "appid"		"{{appId}}"
            "name"		"{{name}}"
            "StateFlags"		"4"
            "installdir"		"{{installDir}}"
        }
        """;
}
