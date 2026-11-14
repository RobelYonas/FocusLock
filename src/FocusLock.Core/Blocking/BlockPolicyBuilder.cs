using FocusLock.Core.Abstractions;
using FocusLock.Core.Detection;
using FocusLock.Core.Settings;
using FocusLock.Core.Websites;
using Microsoft.Extensions.Logging;

namespace FocusLock.Core.Blocking;

/// <summary>Builds the policy for a session from the catalog's enabled entries, the user's allowlist and the blocked sites.</summary>
public sealed class BlockPolicyBuilder(IFileSystem fileSystem, ISitePresetCatalog sitePresets, ILogger<BlockPolicyBuilder> logger)
{
    private const int MaxDepth = 3;
    private const int MaxDirectoriesPerRule = 400;

    /// <summary>Scans for games again first, so games installed since the last scan are blocked too.</summary>
    public async Task<BlockPolicy> BuildFreshAsync(GameCatalog catalog, AppSettings settings, CancellationToken cancellationToken = default)
    {
        await catalog.RefreshAsync(cancellationToken).ConfigureAwait(false);
        return Build(catalog.Entries, settings);
    }

    public BlockPolicy Build(IReadOnlyList<CatalogEntry> entries, AppSettings settings)
    {
        var rules = new List<BlockRule>();
        foreach (var entry in entries)
        {
            if (!entry.IsEnabled)
            {
                continue;
            }

            var game = entry.Game;
            var executableNames = game.Kind == MatchKind.Folder ? CollectExecutableNames(game.Target) : null;
            rules.Add(new BlockRule(game.Name, game.Kind, game.Target, executableNames));
        }

        var allow = settings.Allowlist
            .Select(CustomListDetector.ToGame)
            .OfType<DetectedGame>()
            .Select(game => new BlockRule(game.Name, game.Kind, game.Target))
            .ToList();

        return new BlockPolicy(rules, allow)
        {
            BlockTaskManager = settings.CommitToTimer,
            BlockedDomains = ResolveDomains(settings),
        };
    }

    /// <summary>Domains of the selected presets plus the custom domains with their variants, lowercase, valid and deduped.</summary>
    private List<string> ResolveDomains(AppSettings settings)
    {
        var presetDomains = settings.BlockedSitePresetIds
            .Select(id => sitePresets.All.FirstOrDefault(preset => preset.Id == id))
            .OfType<SitePreset>()
            .SelectMany(preset => preset.Domains);

        var normalized = settings.CustomBlockedDomains
            .Select(input => DomainName.TryNormalize(input, out var domain) ? domain : null)
            .OfType<string>()
            .ToList();
        IReadOnlyList<string> customDomains = normalized.Count == 0 ? [] : DomainName.WithVariants(normalized);

        return presetDomains
            .Concat(customDomains)
            .Select(domain => domain.ToLowerInvariant())
            .Where(DomainName.IsValid)
            .Distinct()
            .ToList();
    }

    /// <summary>Names of executables found a few levels deep in a game folder. Bounded so huge game folders stay cheap.</summary>
    private List<string> CollectExecutableNames(string folder)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!fileSystem.DirectoryExists(folder))
        {
            return [];
        }

        var queue = new Queue<(string Directory, int Depth)>();
        queue.Enqueue((folder, 0));
        var visited = 0;
        while (queue.Count > 0 && visited < MaxDirectoriesPerRule)
        {
            var (directory, depth) = queue.Dequeue();
            visited++;
            try
            {
                foreach (var file in fileSystem.EnumerateFiles(directory, "*.exe"))
                {
                    names.Add(Path.GetFileName(file));
                }

                if (depth < MaxDepth)
                {
                    foreach (var subdirectory in fileSystem.EnumerateDirectories(directory))
                    {
                        queue.Enqueue((subdirectory, depth + 1));
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.LogDebug(ex, "Could not list {Directory}", directory);
            }
        }

        return names.ToList();
    }
}
