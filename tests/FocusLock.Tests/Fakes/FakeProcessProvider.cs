using FocusLock.Core.Abstractions;

namespace FocusLock.Tests.Fakes;

public sealed class FakeProcessProvider : IProcessProvider
{
    public List<ProcessInfo> Processes { get; } = [];

    /// <summary>Paths returned by <see cref="TryGetExecutablePath"/>; processes not listed here resolve to null.</summary>
    public Dictionary<int, string> Paths { get; } = [];

    public bool ThrowOnList { get; set; }

    public int PathLookups { get; private set; }

    public FakeProcessProvider Add(int id, string name, string? path = null)
    {
        Processes.Add(new ProcessInfo(id, name, null));
        if (path is not null)
        {
            Paths[id] = path;
        }

        return this;
    }

    public void Remove(int id)
    {
        Processes.RemoveAll(process => process.Id == id);
        Paths.Remove(id);
    }

    public IReadOnlyList<ProcessInfo> ListProcesses()
    {
        if (ThrowOnList)
        {
            throw new InvalidOperationException("Listing failed.");
        }

        return Processes.ToList();
    }

    public string? TryGetExecutablePath(int processId)
    {
        PathLookups++;
        return Paths.TryGetValue(processId, out var path) ? path : null;
    }
}
