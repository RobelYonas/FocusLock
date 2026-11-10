using FocusLock.Core.Abstractions;
using FocusLock.Core.Common;
using Microsoft.Extensions.Logging;

namespace FocusLock.Core.Detection;

/// <summary>
/// Finds Ubisoft Connect games. The launcher lists each install folder under Launcher\Installs;
/// the game's name is in its "Uplay Install {id}" entry in the Windows installed-programs list.
/// </summary>
public sealed class UbisoftGameDetector(IRegistry registry, ILogger<UbisoftGameDetector> logger) : IGameDetector
{
    private const string InstallsKey = @"SOFTWARE\WOW6432Node\Ubisoft\Launcher\Installs";
    private const string UninstallKey = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall";

    public GameSource Source => GameSource.Ubisoft;

    public Task<IReadOnlyList<DetectedGame>> DetectAsync(CancellationToken cancellationToken)
    {
        var games = new List<DetectedGame>();
        foreach (var id in registry.GetSubKeyNames(RegistryRoot.LocalMachine, InstallsKey))
        {
            var folder = PathNormalizer.TryNormalize(registry.GetString(RegistryRoot.LocalMachine, $@"{InstallsKey}\{id}", "InstallDir"));
            if (folder is not null)
            {
                var name = registry.GetString(RegistryRoot.LocalMachine, $@"{UninstallKey}\Uplay Install {id}", "DisplayName");
                games.Add(new DetectedGame($"ubisoft:{id}", string.IsNullOrWhiteSpace(name) ? Path.GetFileName(folder) : name.Trim(), Source, folder, MatchKind.Folder));
            }
        }

        logger.LogInformation("Ubisoft: found {Count} games", games.Count);
        return Task.FromResult<IReadOnlyList<DetectedGame>>(games);
    }
}
