namespace FocusLock.Core.Detection;

/// <summary>A detected game together with the user's on/off choice for it.</summary>
public sealed record CatalogEntry(DetectedGame Game, bool IsEnabled);
