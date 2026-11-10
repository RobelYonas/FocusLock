using FocusLock.Core.Abstractions;

namespace FocusLock.Core.Sessions;

/// <summary>Raised when a game was closed (or could not be closed) during a session, with the time left in the current phase.</summary>
public sealed record BlockedLaunchNotice(string GameName, TerminateResult Result, TimeSpan RemainingInPhase);
