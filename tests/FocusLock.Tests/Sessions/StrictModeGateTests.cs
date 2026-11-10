using FocusLock.Core.Sessions;

namespace FocusLock.Tests.Sessions;

public class StrictModeGateTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    private readonly StrictModeGate _gate = new();

    [Fact]
    public void A_closed_gate_is_never_satisfied()
    {
        Assert.False(_gate.IsOpen);
        Assert.False(_gate.IsSatisfied(Now.AddMinutes(5), StrictModeGate.Sentence));
        Assert.Equal(StrictModeGate.WaitTime, _gate.RemainingWait(Now));
    }

    [Fact]
    public void The_wait_counts_down_and_then_satisfies_the_gate()
    {
        _gate.Open(Now);

        Assert.Equal(TimeSpan.FromSeconds(60), _gate.RemainingWait(Now));
        Assert.Equal(TimeSpan.FromSeconds(15), _gate.RemainingWait(Now.AddSeconds(45)));
        Assert.False(_gate.IsSatisfied(Now.AddSeconds(45), null));

        Assert.Equal(TimeSpan.Zero, _gate.RemainingWait(Now.AddSeconds(60)));
        Assert.True(_gate.IsSatisfied(Now.AddSeconds(60), null));
        Assert.Equal(TimeSpan.Zero, _gate.RemainingWait(Now.AddMinutes(10)));
    }

    [Theory]
    [InlineData("I choose to end this session early.")]
    [InlineData("i choose to end this session early")]
    [InlineData("  I CHOOSE TO END THIS SESSION EARLY.  ")]
    public void Typing_the_sentence_satisfies_the_gate_immediately(string typed)
    {
        _gate.Open(Now);

        Assert.True(_gate.IsSatisfied(Now.AddSeconds(1), typed));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("I choose to end this session")]
    [InlineData("I choose to end this session early, please")]
    public void Anything_else_does_not(string? typed)
    {
        _gate.Open(Now);

        Assert.False(_gate.IsSatisfied(Now.AddSeconds(1), typed));
    }

    [Fact]
    public void Reopening_an_open_gate_does_not_restart_the_clock()
    {
        _gate.Open(Now);
        _gate.Open(Now.AddSeconds(50));

        Assert.True(_gate.IsSatisfied(Now.AddSeconds(60), null));
    }

    [Fact]
    public void Closing_and_opening_again_restarts_the_wait()
    {
        _gate.Open(Now);
        _gate.Close();
        Assert.False(_gate.IsOpen);

        _gate.Open(Now.AddSeconds(55));

        Assert.False(_gate.IsSatisfied(Now.AddSeconds(60), null));
        Assert.True(_gate.IsSatisfied(Now.AddSeconds(115), null));
    }
}
