using FocusLock.Core.Abstractions;

namespace FocusLock.Tests.Fakes;

public sealed class FakeClock : IClock
{
    public FakeClock(DateTimeOffset? start = null)
    {
        UtcNow = start ?? new DateTimeOffset(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);
    }

    public DateTimeOffset UtcNow { get; set; }

    public void Advance(TimeSpan amount) => UtcNow += amount;
}
