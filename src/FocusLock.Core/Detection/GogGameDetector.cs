using FocusLock.Core.Abstractions;
using FocusLock.Core.Common;
using Microsoft.Extensions.Logging;

namespace FocusLock.Core.Detection;

/// <summary>Finds GOG games. GOG Galaxy and the GOG offline installers register each game under GOG.com\Games.</summary>
public sealed class GogGameDetector(IRegistry registry, ILogger<GogGameDetector> logger) : IGameDetector
{
    private const string GamesKey = @"SOFTWARE\WOW6432Node\GOG.com\Games";

    public GameSource Source => GameSource.Gog;

    public Task<IReadOnlyList<DetectedGame>> DetectAsync(CancellationToken cancellationToken)
    {
        var games = new List<DetectedGame>();
        foreach (var id in registry.GetSubKeyNames(RegistryRoot.LocalMachine, GamesKey))
        {
            var key = $@"{GamesKey}\{id}";
            var folder = PathNormalizer.TryNormalize(registry.GetString(RegistryRoot.LocalMachine, key, "path"));
            if (folder is not null)
            {
                var name = registry.GetString(RegistryRoot.LocalMachine, key, "gameName");
                games.Add(new DetectedGame($"gog:{id}", string.IsNullOrWhiteSpace(name) ? Path.GetFileName(folder) : name.Trim(), Source, folder, MatchKind.Folder));
            }
        }

        logger.LogInformation("GOG: found {Count} games", games.Count);
        return Task.FromResult<IReadOnlyList<DetectedGame>>(games);
    }
}
