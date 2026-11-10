namespace FocusLock.Core.Detection;

/// <summary>A game (or launcher) found by a detector. <see cref="Id"/> is stable across runs so user toggles can refer to it.</summary>
public sealed record DetectedGame(string Id, string Name, GameSource Source, string Target, MatchKind Kind);
