using FocusLock.Core.Sessions;

namespace FocusLock.Tests.Sessions;

public class FocusSessionEngineTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);
    private static readonly SessionPlan Pomodoro = new(TimeSpan.FromMinutes(25), Pomodoro: true, TimeSpan.FromMinutes(5), Cycles: 2);

    private readonly FocusSessionEngine _engine = new();

    [Fact]
    public void Advance_reports_remaining_time_and_progress()
    {
        _engine.Start(SessionPlan.Single(TimeSpan.FromMinutes(25)), Start);

        var snapshot = _engine.Advance(Start.AddMinutes(10));

        Assert.Equal(SessionPhase.Focus, snapshot.Phase);
        Assert.Equal(1, snapshot.Cycle);
        Assert.Equal(1, snapshot.TotalCycles);
        Assert.Equal(TimeSpan.FromMinutes(15), snapshot.RemainingInPhase);
        Assert.Equal(TimeSpan.FromMinutes(15), snapshot.RemainingTotal);
        Assert.Equal(0.4, snapshot.PhaseProgress, 3);
        Assert.Equal(TimeSpan.FromMinutes(10), snapshot.FocusedTime);
        Assert.False(snapshot.IsFinished);
        Assert.False(snapshot.PhaseChanged);
    }

    [Fact]
    public void Session_finishes_when_the_last_block_ends_and_stays_finished()
    {
        _engine.Start(SessionPlan.Single(TimeSpan.FromMinutes(25)), Start);

        var atEnd = _engine.Advance(Start.AddMinutes(25));
        Assert.True(atEnd.IsFinished);
        Assert.Equal(TimeSpan.Zero, atEnd.RemainingInPhase);
        Assert.Equal(TimeSpan.FromMinutes(25), atEnd.FocusedTime);
        Assert.Equal(1d, atEnd.PhaseProgress);

        var later = _engine.Advance(Start.AddMinutes(40));
        Assert.True(later.IsFinished);
        Assert.Equal(TimeSpan.FromMinutes(25), later.FocusedTime);
    }

    [Fact]
    public void Pomodoro_moves_through_focus_break_focus()
    {
        _engine.Start(Pomodoro, Start);

        var inBreak = _engine.Advance(Start.AddMinutes(26));
        Assert.Equal(SessionPhase.Break, inBreak.Phase);
        Assert.True(inBreak.PhaseChanged);
        Assert.Equal(1, inBreak.Cycle);
        Assert.Equal(2, inBreak.TotalCycles);
        Assert.Equal(TimeSpan.FromMinutes(4), inBreak.RemainingInPhase);
        Assert.Equal(TimeSpan.FromMinutes(29), inBreak.RemainingTotal);
        Assert.Equal(TimeSpan.FromMinutes(25), inBreak.FocusedTime);

        var stillInBreak = _engine.Advance(Start.AddMinutes(27));
        Assert.False(stillInBreak.PhaseChanged);

        var secondFocus = _engine.Advance(Start.AddMinutes(31));
        Assert.Equal(SessionPhase.Focus, secondFocus.Phase);
        Assert.True(secondFocus.PhaseChanged);
        Assert.Equal(2, secondFocus.Cycle);
        Assert.Equal(TimeSpan.FromMinutes(26), secondFocus.FocusedTime);

        var done = _engine.Advance(Start.AddMinutes(55));
        Assert.True(done.IsFinished);
        Assert.Equal(TimeSpan.FromMinutes(50), done.FocusedTime);
    }

    [Fact]
    public void A_large_jump_in_time_crosses_several_segments_at_once()
    {
        _engine.Start(Pomodoro, Start);

        var snapshot = _engine.Advance(Start.AddMinutes(31));

        Assert.Equal(SessionPhase.Focus, snapshot.Phase);
        Assert.Equal(2, snapshot.Cycle);
        Assert.True(snapshot.PhaseChanged);
    }

    [Fact]
    public void Stop_ends_the_session_with_the_focused_time_so_far()
    {
        _engine.Start(SessionPlan.Single(TimeSpan.FromMinutes(25)), Start);

        var snapshot = _engine.Stop(Start.AddMinutes(10));

        Assert.Equal(TimeSpan.FromMinutes(10), snapshot.FocusedTime);
        Assert.False(_engine.IsRunning);
        Assert.Null(_engine.Plan);
        Assert.Throws<InvalidOperationException>(() => _engine.Advance(Start.AddMinutes(11)));
    }

    [Fact]
    public void Starting_twice_is_rejected()
    {
        _engine.Start(SessionPlan.Single(TimeSpan.FromMinutes(25)), Start);

        Assert.Throws<InvalidOperationException>(() => _engine.Start(SessionPlan.Single(TimeSpan.FromMinutes(25)), Start));
    }

    [Fact]
    public void A_clock_that_goes_backwards_is_clamped()
    {
        _engine.Start(SessionPlan.Single(TimeSpan.FromMinutes(25)), Start);

        var snapshot = _engine.Advance(Start.AddMinutes(-1));

        Assert.Equal(TimeSpan.FromMinutes(25), snapshot.RemainingInPhase);
        Assert.Equal(0d, snapshot.PhaseProgress);
        Assert.Equal(TimeSpan.Zero, snapshot.FocusedTime);
    }
}
