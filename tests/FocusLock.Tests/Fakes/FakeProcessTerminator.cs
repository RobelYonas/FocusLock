using FocusLock.Core.Abstractions;

namespace FocusLock.Tests.Fakes;

public sealed class FakeProcessTerminator : IProcessTerminator
{
    public List<int> Terminated { get; } = [];

    /// <summary>Result to return for a given process id; anything else returns <see cref="TerminateResult.Terminated"/>.</summary>
    public Dictionary<int, TerminateResult> Results { get; } = [];

    public TerminateResult Terminate(int processId)
    {
        Terminated.Add(processId);
        return Results.TryGetValue(processId, out var result) ? result : TerminateResult.Terminated;
    }
}
