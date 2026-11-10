namespace FocusLock.Core.Abstractions;

/// <summary>Source of the current time. Injected so time-based logic can be driven by a fake clock in tests.</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
