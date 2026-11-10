using System.Text.Json;
using FocusLock.Core.Abstractions;
using FocusLock.Core.Common;
using Microsoft.Extensions.Logging;

namespace FocusLock.Core.Detection.Epic;

/// <summary>Finds Epic Games Store games from the launcher's manifest files (one .item JSON file per installed app).</summary>
public sealed class EpicGameDetector(
    IRegistry registry,
    IFileSystem fileSystem,
    IAppEnvironment environment,
    ILogger<EpicGameDetector> logger) : IGameDetector
{
    public GameSource Source => GameSource.Epic;

    public Task<IReadOnlyList<DetectedGame>> DetectAsync(CancellationToken cancellationToken)
    {
        var games = new List<DetectedGame>();
        var manifestDirectory = FindManifestDirectory();
        if (manifestDirectory is null)
        {
            logger.LogInformation("Epic Games Launcher was not found; skipping Epic games");
            return Task.FromResult<IReadOnlyList<DetectedGame>>(games);
        }

        var seenLocations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in EnumerateManifests(manifestDirectory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var manifest = ReadManifest(file);
            if (manifest is null || !IsInstalledGame(manifest))
            {
                continue;
            }

            var location = PathNormalizer.TryNormalize(manifest.InstallLocation);
            if (location is null || !seenLocations.Add(location))
            {
                continue;
            }

            var appName = string.IsNullOrWhiteSpace(manifest.AppName) ? Path.GetFileName(location) : manifest.AppName.Trim();
            var displayName = string.IsNullOrWhiteSpace(manifest.DisplayName) ? appName : manifest.DisplayName.Trim();
            games.Add(new DetectedGame($"epic:{appName}", displayName, GameSource.Epic, location, MatchKind.Folder));
        }

        logger.LogInformation("Epic: found {Count} games", games.Count);
        return Task.FromResult<IReadOnlyList<DetectedGame>>(games);
    }

    private string? FindManifestDirectory()
    {
        var registered = registry.GetString(RegistryRoot.LocalMachine, @"SOFTWARE\Epic Games\EpicGamesLauncher", "AppDataPath");
        string?[] candidates =
        [
            registered is null ? null : Path.Combine(registered, "Manifests"),
            Path.Combine(environment.ProgramDataDirectory, "Epic", "EpicGamesLauncher", "Data", "Manifests"),
        ];

        foreach (var candidate in candidates)
        {
            var normalized = PathNormalizer.TryNormalize(candidate);
            if (normalized is not null && fileSystem.DirectoryExists(normalized))
            {
                return normalized;
            }
        }

        return null;
    }

    private static bool IsInstalledGame(EpicManifest manifest)
    {
        if (manifest.IsIncompleteInstall)
        {
            return false;
        }

        if (manifest.AppCategories is { Count: > 0 } categories && !categories.Contains("games", StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        // Add-ons and DLC point at their parent game, which has its own manifest.
        return string.IsNullOrEmpty(manifest.MainGameAppName)
            || string.Equals(manifest.MainGameAppName, manifest.AppName, StringComparison.Ordinal);
    }

    private EpicManifest? ReadManifest(string path)
    {
        try
        {
            return JsonSerializer.Deserialize(fileSystem.ReadAllText(path), EpicJsonContext.Default.EpicManifest);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Skipping unreadable Epic manifest {Path}", path);
            return null;
        }
    }

    private List<string> EnumerateManifests(string directory)
    {
        try
        {
            return fileSystem.EnumerateFiles(directory, "*.item").ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not list Epic manifests in {Directory}", directory);
            return [];
        }
    }
}
