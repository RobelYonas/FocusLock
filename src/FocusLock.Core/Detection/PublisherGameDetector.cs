using FocusLock.Core.Abstractions;
using FocusLock.Core.Common;
using Microsoft.Extensions.Logging;

namespace FocusLock.Core.Detection;

/// <summary>
/// Finds a store's games in the Windows installed-programs list by publisher. Used for the EA app and Battle.net,
/// which register every game there. The launcher's own entry and Steam's copies of these games are skipped.
/// </summary>
public sealed class PublisherGameDetector(
    GameSource source,
    string publisher,
    string[] launcherNames,
    IRegistry registry,
    ILogger<PublisherGameDetector> logger) : IGameDetector
{
    private static readonly (RegistryRoot Root, string Key)[] UninstallKeys =
    [
        (RegistryRoot.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
        (RegistryRoot.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"),
        (RegistryRoot.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
    ];

    public GameSource Source => source;

    public static PublisherGameDetector Ea(IRegistry registry, ILogger<PublisherGameDetector> logger) =>
        new(GameSource.Ea, "Electronic Arts", ["EA app", "Origin"], registry, logger);

    public static PublisherGameDetector BattleNet(IRegistry registry, ILogger<PublisherGameDetector> logger) =>
        new(GameSource.BattleNet, "Blizzard Entertainment", ["Battle.net"], registry, logger);

    public Task<IReadOnlyList<DetectedGame>> DetectAsync(CancellationToken cancellationToken)
    {
        var games = new List<DetectedGame>();
        var seenFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (root, uninstallKey) in UninstallKeys)
        {
            foreach (var keyName in registry.GetSubKeyNames(root, uninstallKey))
            {
                if (keyName.StartsWith("Steam App ", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var key = $@"{uninstallKey}\{keyName}";
                var name = registry.GetString(root, key, "DisplayName")?.Trim();
                var folder = PathNormalizer.TryNormalize(registry.GetString(root, key, "InstallLocation"));
                var isFromPublisher = registry.GetString(root, key, "Publisher")?.StartsWith(publisher, StringComparison.OrdinalIgnoreCase) == true;
                if (!isFromPublisher || string.IsNullOrEmpty(name) || folder is null
                    || launcherNames.Contains(name, StringComparer.OrdinalIgnoreCase) || !seenFolders.Add(folder))
                {
                    continue;
                }

                games.Add(new DetectedGame($"{source.ToString().ToLowerInvariant()}:{keyName}", name, source, folder, MatchKind.Folder));
            }
        }

        logger.LogInformation("{Source}: found {Count} games", source, games.Count);
        return Task.FromResult<IReadOnlyList<DetectedGame>>(games);
    }
}
