using FocusLock.Core.Detection;
using FocusLock.Core.Settings;
using FocusLock.Tests.Fakes;

namespace FocusLock.Tests.Detection;

public class LauncherDetectorTests
{
    [Fact]
    public async Task Lists_known_launchers_by_process_name()
    {
        var launchers = await new LauncherDetector().DetectAsync(CancellationToken.None);

        Assert.Equal(7, launchers.Count);
        Assert.All(launchers, launcher =>
        {
            Assert.Equal(GameSource.Launcher, launcher.Source);
            Assert.Equal(MatchKind.ProcessName, launcher.Kind);
            Assert.StartsWith("launcher:", launcher.Id, StringComparison.Ordinal);
        });
        Assert.Contains(launchers, launcher => launcher.Target == "steam.exe" && launcher.Name == "Steam");
        Assert.Contains(launchers, launcher => launcher.Target == "EpicGamesLauncher.exe" && launcher.Id == "launcher:epicgameslauncher.exe");
    }
}

public class CustomListDetectorTests
{
    [Fact]
    public async Task Converts_entries_and_skips_unusable_paths()
    {
        var settings = new AppSettings
        {
            CustomBlocklist =
            [
                new CustomEntry { Path = "C:/Games/foo/game.exe", IsFolder = false },
                new CustomEntry { Path = @"D:\Games\Bar\", IsFolder = true },
                new CustomEntry { Path = "   ", IsFolder = false },
            ],
        };

        var games = await new CustomListDetector(TestSettings.Service(settings)).DetectAsync(CancellationToken.None);

        Assert.Equal(2, games.Count);

        var executable = games[0];
        Assert.Equal(@"C:\Games\foo\game.exe", executable.Target);
        Assert.Equal(MatchKind.Executable, executable.Kind);
        Assert.Equal("game", executable.Name);
        Assert.Equal(@"custom:c:\games\foo\game.exe", executable.Id);
        Assert.Equal(GameSource.Custom, executable.Source);

        var folder = games[1];
        Assert.Equal(@"D:\Games\Bar", folder.Target);
        Assert.Equal(MatchKind.Folder, folder.Kind);
        Assert.Equal("Bar", folder.Name);
    }
}
