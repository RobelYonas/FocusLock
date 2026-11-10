namespace FocusLock.Core.Abstractions;

public interface IProcessProvider
{
    /// <summary>Lists running processes without resolving executable paths; cheap enough to call every second.</summary>
    IReadOnlyList<ProcessInfo> ListProcesses();

    /// <summary>Resolves the full executable path, or null when the process exited or access was denied.</summary>
    string? TryGetExecutablePath(int processId);
}
