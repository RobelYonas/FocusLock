namespace FocusLock.Core.Detection;

/// <summary>Well-known game launchers, matched by process name. Blocking them is a single user toggle.</summary>
public sealed class LauncherDetector : IGameDetector
{
    private static readonly IReadOnlyList<(string Name, string Executable)> KnownLaunchers =
    [
        ("Steam", "steam.exe"),
        ("Epic Games Launcher", "EpicGamesLauncher.exe"),
        ("GOG Galaxy", "GalaxyClient.exe"),
        ("EA app", "EADesktop.exe"),
        ("Ubisoft Connect", "UbisoftConnect.exe"),
        ("Battle.net", "Battle.net.exe"),
        ("Riot Client", "RiotClientServices.exe"),
    ];

    public GameSource Source => GameSource.Launcher;

    public Task<IReadOnlyList<DetectedGame>> DetectAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<DetectedGame> launchers = KnownLaunchers
            .Select(launcher => new DetectedGame(
                $"launcher:{launcher.Executable.ToLowerInvariant()}",
                launcher.Name,
                GameSource.Launcher,
                launcher.Executable,
                MatchKind.ProcessName))
            .ToList();

        return Task.FromResult(launchers);
    }
}
