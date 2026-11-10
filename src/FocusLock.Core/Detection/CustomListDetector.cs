using FocusLock.Core.Common;
using FocusLock.Core.Settings;

namespace FocusLock.Core.Detection;

/// <summary>Turns the user's own blocklist entries (an .exe or a folder) into detected games.</summary>
public sealed class CustomListDetector(SettingsService settings) : IGameDetector
{
    public GameSource Source => GameSource.Custom;

    public Task<IReadOnlyList<DetectedGame>> DetectAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<DetectedGame> games = settings.Current.CustomBlocklist
            .Select(ToGame)
            .OfType<DetectedGame>()
            .ToList();

        return Task.FromResult(games);
    }

    public static DetectedGame? ToGame(CustomEntry entry)
    {
        var normalized = PathNormalizer.TryNormalize(entry.Path);
        if (normalized is null)
        {
            return null;
        }

        var name = entry.IsFolder ? Path.GetFileName(normalized) : Path.GetFileNameWithoutExtension(normalized);
        if (string.IsNullOrEmpty(name))
        {
            name = normalized;
        }

        return new DetectedGame(
            $"custom:{normalized.ToLowerInvariant()}",
            name,
            GameSource.Custom,
            normalized,
            entry.IsFolder ? MatchKind.Folder : MatchKind.Executable);
    }
}
