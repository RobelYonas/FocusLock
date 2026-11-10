using FocusLock.Core.Abstractions;

namespace FocusLock.Core.Platform;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
