namespace FocusLock.Core.Blocking;

public sealed record BlockDecision(bool ShouldBlock, BlockRule? MatchedRule)
{
    public static BlockDecision Allow { get; } = new(false, null);

    public static BlockDecision Block(BlockRule rule) => new(true, rule);
}
