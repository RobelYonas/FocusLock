namespace FocusLock.Core.Blocking;

/// <summary>Everything the blocking engine needs for one session. Allow rules always win over block rules.</summary>
public sealed record BlockPolicy(IReadOnlyList<BlockRule> Rules, IReadOnlyList<BlockRule> Allow)
{
    public static BlockPolicy Empty { get; } = new([], []);
}
