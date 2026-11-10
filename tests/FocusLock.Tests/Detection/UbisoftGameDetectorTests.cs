using FocusLock.Core.Abstractions;
using FocusLock.Core.Detection;
using FocusLock.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace FocusLock.Tests.Detection;

public class UbisoftGameDetectorTests
{
    private const string InstallsKey = @"SOFTWARE\WOW6432Node\Ubisoft\Launcher\Installs";
    private const string UninstallKey = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall";

    private readonly FakeRegistry _registry = new();

    [Fact]
    public async Task Finds_games_and_takes_the_name_from_the_installed_programs_list()
    {
        _registry
            .Set(RegistryRoot.LocalMachine, $@"{InstallsKey}\635", "InstallDir", "D:/Tom Clancy's Rainbow Six Siege/")
            .Set(RegistryRoot.LocalMachine, $@"{UninstallKey}\Uplay Install 635", "DisplayName", "Tom Clancy's Rainbow Six Siege");

        var game = Assert.Single(await Detect());

        Assert.Equal("ubisoft:635", game.Id);
        Assert.Equal("Tom Clancy's Rainbow Six Siege", game.Name);
        Assert.Equal(GameSource.Ubisoft, game.Source);
        Assert.Equal(@"D:\Tom Clancy's Rainbow Six Siege", game.Target);
        Assert.Equal(MatchKind.Folder, game.Kind);
    }

    [Fact]
    public async Task Uses_the_folder_name_when_no_name_is_registered_and_skips_entries_without_a_folder()
    {
        _registry
            .Set(RegistryRoot.LocalMachine, $@"{InstallsKey}\15657", "InstallDir", "C:/Program Files (x86)/Ubisoft/Ubisoft Game Launcher/games/XDefiant/")
            .Set(RegistryRoot.LocalMachine, $@"{InstallsKey}\99", "Language", "en-US");

        var game = Assert.Single(await Detect());

        Assert.Equal("XDefiant", game.Name);
        Assert.Equal(@"C:\Program Files (x86)\Ubisoft\Ubisoft Game Launcher\games\XDefiant", game.Target);
    }

    [Fact]
    public async Task Returns_nothing_when_Ubisoft_Connect_is_not_installed()
    {
        Assert.Empty(await Detect());
    }

    private Task<IReadOnlyList<DetectedGame>> Detect() =>
        new UbisoftGameDetector(_registry, NullLogger<UbisoftGameDetector>.Instance).DetectAsync(CancellationToken.None);
}
