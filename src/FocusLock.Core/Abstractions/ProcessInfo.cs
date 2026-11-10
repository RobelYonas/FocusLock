namespace FocusLock.Core.Abstractions;

/// <summary>
/// A running process. <see cref="Name"/> is the executable file name (e.g. "steam.exe").
/// <see cref="ExecutablePath"/> is null when it could not be read (the process exited or access was denied).
/// </summary>
public sealed record ProcessInfo(int Id, string Name, string? ExecutablePath);
