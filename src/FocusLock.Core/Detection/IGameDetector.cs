namespace FocusLock.Core.Detection;

/// <summary>One source of games. To support a new store, implement this and register it in DI.</summary>
public interface IGameDetector
{
    GameSource Source { get; }

    Task<IReadOnlyList<DetectedGame>> DetectAsync(CancellationToken cancellationToken);
}
