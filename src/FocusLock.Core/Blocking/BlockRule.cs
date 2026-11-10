using FocusLock.Core.Detection;

namespace FocusLock.Core.Blocking;

/// <summary>
/// One thing to block or allow: a folder, an executable path, or a process name.
/// <see cref="ExecutableNames"/> lists executables known to live in a folder rule, used when a process path cannot be read.
/// Plain data so it can cross a process boundary later.
/// </summary>
public sealed record BlockRule(string Name, MatchKind Kind, string Target, IReadOnlyList<string>? ExecutableNames = null);
