using FocusLock.Core.Abstractions;
using FocusLock.Core.Detection;
using FocusLock.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace FocusLock.Tests.Detection;

public class GogGameDetectorTests
{
    private const string GamesKey = @"SOFTWARE\WOW6432Node\GOG.com\Games";

    private readonly FakeRegistry _registry = new();

    [Fact]
    public async Task Finds_installed_games_from_the_registry()
    {
        _registry
            .Set(RegistryRoot.LocalMachine, $@"{GamesKey}\1207658924", "gameName", "Stardew Valley")
            .Set(RegistryRoot.LocalMachine, $@"{GamesKey}\1207658924", "path", @"C:\GOG Games\Stardew Valley\");

        var game = Assert.Single(await Detect());

        Assert.Equal("gog:1207658924", game.Id);
        Assert.Equal("Stardew Valley", game.Name);
        Assert.Equal(GameSource.Gog, game.Source);
        Assert.Equal(@"C:\GOG Games\Stardew Valley", game.Target);
        Assert.Equal(MatchKind.Folder, game.Kind);
    }

    [Fact]
    public async Task Uses_the_folder_name_when_the_name_is_missing_and_skips_entries_without_a_path()
    {
        _registry
            .Set(RegistryRoot.LocalMachine, $@"{GamesKey}\1", "path", @"D:\GOG\Hades")
            .Set(RegistryRoot.LocalMachine, $@"{GamesKey}\2", "gameName", "No Path");

        var game = Assert.Single(await Detect());

        Assert.Equal("Hades", game.Name);
    }

    [Fact]
    public async Task Returns_nothing_when_GOG_is_not_installed()
    {
        Assert.Empty(await Detect());
    }

    private Task<IReadOnlyList<DetectedGame>> Detect() =>
        new GogGameDetector(_registry, NullLogger<GogGameDetector>.Instance).DetectAsync(CancellationToken.None);
}
