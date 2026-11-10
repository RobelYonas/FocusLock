using FocusLock.Core.Settings;
using Microsoft.Extensions.Logging;

namespace FocusLock.Core.Detection;

/// <summary>Runs every detector and applies the user's per-game toggles. The UI reads <see cref="Entries"/>.</summary>
public sealed class GameCatalog(
    IEnumerable<IGameDetector> detectors,
    SettingsService settings,
    ILogger<GameCatalog> logger)
{
    private readonly IReadOnlyList<IGameDetector> _detectors = detectors.ToList();

    public IReadOnlyList<CatalogEntry> Entries { get; private set; } = [];

    public event EventHandler? Changed;

    /// <summary>Number of games (launchers not included) that will be blocked during a session.</summary>
    public int BlockedGameCount => Entries.Count(entry => entry.IsEnabled && entry.Game.Source != GameSource.Launcher);

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var games = new List<DetectedGame>();
        foreach (var detector in _detectors)
        {
            try
            {
                games.AddRange(await detector.DetectAsync(cancellationToken).ConfigureAwait(false));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "The {Source} detector failed; its games are skipped", detector.Source);
            }
        }

        Entries = games.Select(game => new CatalogEntry(game, IsEnabled(game))).ToList();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Turns one detected game on or off. Launchers are controlled together through <see cref="SetLaunchersEnabled"/>.</summary>
    public void SetEnabled(string gameId, bool enabled)
    {
        settings.Update(current =>
        {
            current.DisabledGameIds.RemoveAll(id => string.Equals(id, gameId, StringComparison.OrdinalIgnoreCase));
            if (!enabled)
            {
                current.DisabledGameIds.Add(gameId);
            }
        });

        Entries = Entries
            .Select(entry => entry.Game.Source != GameSource.Launcher && entry.Game.Id == gameId ? entry with { IsEnabled = enabled } : entry)
            .ToList();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void SetLaunchersEnabled(bool enabled)
    {
        settings.Update(current => current.BlockLaunchers = enabled);
        Entries = Entries
            .Select(entry => entry.Game.Source == GameSource.Launcher ? entry with { IsEnabled = enabled } : entry)
            .ToList();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private bool IsEnabled(DetectedGame game) =>
        game.Source == GameSource.Launcher
            ? settings.Current.BlockLaunchers
            : !settings.Current.DisabledGameIds.Contains(game.Id, StringComparer.OrdinalIgnoreCase);
}
