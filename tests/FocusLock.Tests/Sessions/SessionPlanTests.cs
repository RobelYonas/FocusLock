using FocusLock.Core.Sessions;

namespace FocusLock.Tests.Sessions;

public class SessionPlanTests
{
    [Fact]
    public void Single_plan_is_one_focus_block()
    {
        var segments = SessionPlan.Single(TimeSpan.FromMinutes(50)).BuildSegments();

        var segment = Assert.Single(segments);
        Assert.Equal(SessionPhase.Focus, segment.Phase);
        Assert.Equal(TimeSpan.FromMinutes(50), segment.Length);
        Assert.Equal(1, segment.Cycle);
    }

    [Fact]
    public void Pomodoro_alternates_focus_and_break_without_a_trailing_break()
    {
        var plan = new SessionPlan(TimeSpan.FromMinutes(25), Pomodoro: true, TimeSpan.FromMinutes(5), Cycles: 3);

        var segments = plan.BuildSegments();

        Assert.Equal(
            [SessionPhase.Focus, SessionPhase.Break, SessionPhase.Focus, SessionPhase.Break, SessionPhase.Focus],
            segments.Select(segment => segment.Phase));
        Assert.Equal([1, 1, 2, 2, 3], segments.Select(segment => segment.Cycle));
        Assert.Equal(TimeSpan.FromMinutes(85), plan.TotalLength);
    }

    [Fact]
    public void Pomodoro_with_a_single_cycle_is_just_one_focus_block()
    {
        var plan = new SessionPlan(TimeSpan.FromMinutes(25), Pomodoro: true, TimeSpan.FromMinutes(5), Cycles: 1);

        Assert.Single(plan.BuildSegments());
    }

    [Fact]
    public void Zero_break_length_skips_breaks()
    {
        var plan = new SessionPlan(TimeSpan.FromMinutes(25), Pomodoro: true, TimeSpan.Zero, Cycles: 2);

        Assert.All(plan.BuildSegments(), segment => Assert.Equal(SessionPhase.Focus, segment.Phase));
        Assert.Equal(2, plan.BuildSegments().Count);
    }

    [Fact]
    public void Non_pomodoro_plan_ignores_cycles()
    {
        var plan = new SessionPlan(TimeSpan.FromMinutes(25), Pomodoro: false, TimeSpan.FromMinutes(5), Cycles: 4);

        Assert.Single(plan.BuildSegments());
    }

    [Fact]
    public void Zero_focus_length_is_rejected()
    {
        Assert.Throws<InvalidOperationException>(() => SessionPlan.Single(TimeSpan.Zero).BuildSegments());
    }
}
