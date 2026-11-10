namespace FocusLock.Core.Abstractions;

/// <summary>Reports newly started processes. The polling implementation can later be swapped for WMI process-start events.</summary>
public interface IProcessWatcher
{
    event EventHandler<ProcessInfo>? ProcessStarted;

    /// <summary>All running processes, with executable paths resolved where possible.</summary>
    IReadOnlyList<ProcessInfo> Snapshot();

    void Start();

    void Stop();
}
