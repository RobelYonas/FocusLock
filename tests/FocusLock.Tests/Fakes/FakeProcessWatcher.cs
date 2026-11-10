using FocusLock.Core.Abstractions;

namespace FocusLock.Tests.Fakes;

public sealed class FakeProcessWatcher : IProcessWatcher
{
    public event EventHandler<ProcessInfo>? ProcessStarted;

    public List<ProcessInfo> Running { get; } = [];

    public bool IsStarted { get; private set; }

    public int StartCount { get; private set; }

    public int StopCount { get; private set; }

    public IReadOnlyList<ProcessInfo> Snapshot() => Running.ToList();

    public void Start()
    {
        IsStarted = true;
        StartCount++;
    }

    public void Stop()
    {
        IsStarted = false;
        StopCount++;
    }

    public void RaiseStarted(ProcessInfo process) => ProcessStarted?.Invoke(this, process);

    public void RaiseStarted(int id, string name, string? path) => RaiseStarted(new ProcessInfo(id, name, path));
}
