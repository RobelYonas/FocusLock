namespace FocusLock.Core.Processes;

public sealed record ProcessWatcherOptions(TimeSpan PollInterval)
{
    public static ProcessWatcherOptions Default { get; } = new(TimeSpan.FromSeconds(1));
}
