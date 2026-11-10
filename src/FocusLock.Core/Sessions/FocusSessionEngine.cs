namespace FocusLock.Core.Sessions;

/// <summary>
/// Pure state machine for one session. Everything is derived from the timestamps passed in,
/// so ticks never drift and the engine is deterministic in tests.
/// </summary>
public sealed class FocusSessionEngine
{
    private IReadOnlyList<SessionSegment> _segments = [];
    private int _index;
    private DateTimeOffset _segmentStartedAt;

    public bool IsRunning { get; private set; }

    public SessionPlan? Plan { get; private set; }

    public DateTimeOffset StartedAt { get; private set; }

    public void Start(SessionPlan plan, DateTimeOffset now)
    {
        if (IsRunning)
        {
            throw new InvalidOperationException("A session is already running.");
        }

        _segments = plan.BuildSegments();
        _index = 0;
        _segmentStartedAt = now;
        StartedAt = now;
        Plan = plan;
        IsRunning = true;
    }

    /// <summary>
    /// Moves the session forward to <paramref name="now"/> and reports where it is.
    /// <see cref="EngineSnapshot.PhaseChanged"/> is true if a segment boundary was crossed since the previous call.
    /// </summary>
    public EngineSnapshot Advance(DateTimeOffset now)
    {
        if (!IsRunning)
        {
            throw new InvalidOperationException("No session is running.");
        }

        var phaseChanged = false;
        while (true)
        {
            var segment = _segments[_index];
            var elapsed = now - _segmentStartedAt;
            if (elapsed < segment.Length)
            {
                return Snapshot(segment, elapsed < TimeSpan.Zero ? TimeSpan.Zero : elapsed, phaseChanged, isFinished: false);
            }

            if (_index == _segments.Count - 1)
            {
                return Snapshot(segment, segment.Length, phaseChanged, isFinished: true);
            }

            _segmentStartedAt += segment.Length;
            _index++;
            phaseChanged = true;
        }
    }

    /// <summary>Ends the session at <paramref name="now"/>, whether it finished or was cut short, and returns the final snapshot.</summary>
    public EngineSnapshot Stop(DateTimeOffset now)
    {
        var snapshot = Advance(now);
        IsRunning = false;
        Plan = null;
        return snapshot;
    }

    private EngineSnapshot Snapshot(SessionSegment segment, TimeSpan elapsedInSegment, bool phaseChanged, bool isFinished)
    {
        var remainingInPhase = segment.Length - elapsedInSegment;
        if (remainingInPhase < TimeSpan.Zero)
        {
            remainingInPhase = TimeSpan.Zero;
        }

        var remainingTotal = remainingInPhase;
        for (var i = _index + 1; i < _segments.Count; i++)
        {
            remainingTotal += _segments[i].Length;
        }

        var focusedTime = segment.Phase == SessionPhase.Focus ? elapsedInSegment : TimeSpan.Zero;
        for (var i = 0; i < _index; i++)
        {
            if (_segments[i].Phase == SessionPhase.Focus)
            {
                focusedTime += _segments[i].Length;
            }
        }

        var progress = segment.Length > TimeSpan.Zero ? Math.Clamp(elapsedInSegment / segment.Length, 0d, 1d) : 1d;
        var totalCycles = _segments.Count(s => s.Phase == SessionPhase.Focus);
        return new EngineSnapshot(segment.Phase, segment.Cycle, totalCycles, remainingInPhase, remainingTotal, progress, focusedTime, isFinished, phaseChanged);
    }
}
