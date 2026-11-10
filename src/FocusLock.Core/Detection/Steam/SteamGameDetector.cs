using FocusLock.Core.Abstractions;
using FocusLock.Core.Common;
using Microsoft.Extensions.Logging;

namespace FocusLock.Core.Detection.Steam;

/// <summary>
/// Finds installed Steam games: every library listed in libraryfolders.vdf, one entry per app manifest,
/// plus any folder under steamapps\common without a manifest, so that everything under common is covered.
/// </summary>
public sealed class SteamGameDetector(
    IRegistry registry,
    IFileSystem fileSystem,
    IAppEnvironment environment,
    ILogger<SteamGameDetector> logger) : IGameDetector
{
    private const string RedistributablesAppId = "228980";

    public GameSource Source => GameSource.Steam;

    public Task<IReadOnlyList<DetectedGame>> DetectAsync(CancellationToken cancellationToken)
    {
        var games = new List<DetectedGame>();
        var steamRoot = FindSteamRoot();
        if (steamRoot is null)
        {
            logger.LogInformation("Steam was not found; skipping Steam games");
            return Task.FromResult<IReadOnlyList<DetectedGame>>(games);
        }

        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var library in FindLibraries(steamRoot))
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var game in ScanLibrary(library))
            {
                if (seenIds.Add(game.Id))
                {
                    games.Add(game);
                }
            }
        }

        logger.LogInformation("Steam: found {Count} games", games.Count);
        return Task.FromResult<IReadOnlyList<DetectedGame>>(games);
    }

    private string? FindSteamRoot()
    {
        // The machine-wide entry keeps the folder's real casing; the per-user one is lower-case with forward slashes.
        string?[] candidates =
        [
            registry.GetString(RegistryRoot.LocalMachine, @"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath"),
            registry.GetString(RegistryRoot.CurrentUser, @"Software\Valve\Steam", "SteamPath"),
            Path.Combine(environment.ProgramFilesX86Directory, "Steam"),
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

    private List<string> FindLibraries(string steamRoot)
    {
        var libraries = new List<string> { steamRoot };
        var vdfPath = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
        if (!fileSystem.FileExists(vdfPath))
        {
            logger.LogDebug("No libraryfolders.vdf at {Path}; using the Steam folder only", vdfPath);
            return libraries;
        }

        KeyValuesNode root;
        try
        {
            root = KeyValuesParser.Parse(fileSystem.ReadAllText(vdfPath));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not read {Path}; using the Steam folder only", vdfPath);
            return libraries;
        }

        var folders = root.Child("libraryfolders") ?? root;
        foreach (var entry in folders.Children)
        {
            // Current format: "0" { "path" "D:\\SteamLibrary" ... }. Older format: "1" "D:\\SteamLibrary".
            var path = entry.Value is { Length: > 0 } ? entry.Value : entry.ValueOf("path");
            var normalized = PathNormalizer.TryNormalize(path);
            if (normalized is null)
            {
                continue;
            }

            if (!fileSystem.DirectoryExists(normalized))
            {
                logger.LogInformation("Steam library {Path} is not available right now; skipping it", normalized);
                continue;
            }

            if (!libraries.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            {
                libraries.Add(normalized);
            }
        }

        return libraries;
    }

    private List<DetectedGame> ScanLibrary(string library)
    {
        var games = new List<DetectedGame>();
        var steamApps = Path.Combine(library, "steamapps");
        var common = Path.Combine(steamApps, "common");
        var coveredFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var manifest in SafeEnumerateFiles(steamApps, "appmanifest_*.acf"))
        {
            var game = ReadManifest(manifest, common, coveredFolders);
            if (game is not null)
            {
                games.Add(game);
            }
        }

        foreach (var folder in SafeEnumerateDirectories(common))
        {
            var folderName = Path.GetFileName(folder);
            var normalized = PathNormalizer.TryNormalize(folder);
            if (normalized is null || coveredFolders.Contains(folderName))
            {
                continue;
            }

            games.Add(new DetectedGame(
                $"steam:folder:{normalized.ToLowerInvariant()}",
                folderName,
                GameSource.Steam,
                normalized,
                MatchKind.Folder));
        }

        return games;
    }

    private DetectedGame? ReadManifest(string manifestPath, string commonDirectory, HashSet<string> coveredFolders)
    {
        KeyValuesNode root;
        try
        {
            root = KeyValuesParser.Parse(fileSystem.ReadAllText(manifestPath));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not read Steam manifest {Path}", manifestPath);
            return null;
        }

        var state = root.Child("AppState") ?? root;
        var installDir = state.ValueOf("installdir");
        if (string.IsNullOrWhiteSpace(installDir))
        {
            return null;
        }

        coveredFolders.Add(installDir);

        var appId = state.ValueOf("appid") ?? AppIdFromFileName(manifestPath);
        if (appId == RedistributablesAppId)
        {
            return null;
        }

        var target = PathNormalizer.TryNormalize(Path.Combine(commonDirectory, installDir));
        if (target is null)
        {
            return null;
        }

        var name = state.ValueOf("name");
        if (string.IsNullOrWhiteSpace(name))
        {
            name = installDir;
        }

        return new DetectedGame($"steam:{appId}", name.Trim(), GameSource.Steam, target, MatchKind.Folder);
    }

    private static string AppIdFromFileName(string manifestPath)
    {
        const string prefix = "appmanifest_";
        var fileName = Path.GetFileNameWithoutExtension(manifestPath);
        return fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? fileName[prefix.Length..] : fileName;
    }

    private List<string> SafeEnumerateFiles(string directory, string pattern)
    {
        if (!fileSystem.DirectoryExists(directory))
        {
            return [];
        }

        try
        {
            return fileSystem.EnumerateFiles(directory, pattern).ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not list files in {Directory}", directory);
            return [];
        }
    }

    private List<string> SafeEnumerateDirectories(string directory)
    {
        if (!fileSystem.DirectoryExists(directory))
        {
            return [];
        }

        try
        {
            return fileSystem.EnumerateDirectories(directory).ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not list folders in {Directory}", directory);
            return [];
        }
    }
}
