namespace FocusLock.Core.Sessions;

/// <summary>What the user asked for: a single focus block, or Pomodoro cycles of focus and short breaks.</summary>
public sealed record SessionPlan(TimeSpan FocusLength, bool Pomodoro, TimeSpan BreakLength, int Cycles)
{
    public static SessionPlan Single(TimeSpan focusLength) => new(focusLength, Pomodoro: false, TimeSpan.Zero, Cycles: 1);

    /// <summary>Focus, break, focus, ... ending on the last focus block (no trailing break).</summary>
    public IReadOnlyList<SessionSegment> BuildSegments()
    {
        if (FocusLength <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("The focus length must be longer than zero.");
        }

        var cycles = Pomodoro ? Math.Max(1, Cycles) : 1;
        var segments = new List<SessionSegment>();
        for (var cycle = 1; cycle <= cycles; cycle++)
        {
            segments.Add(new SessionSegment(SessionPhase.Focus, FocusLength, cycle));
            if (cycle < cycles && BreakLength > TimeSpan.Zero)
            {
                segments.Add(new SessionSegment(SessionPhase.Break, BreakLength, cycle));
            }
        }

        return segments;
    }

    public TimeSpan TotalLength => BuildSegments().Aggregate(TimeSpan.Zero, (total, segment) => total + segment.Length);
}
