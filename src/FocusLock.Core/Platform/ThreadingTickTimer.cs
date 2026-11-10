using FocusLock.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace FocusLock.Core.Platform;

/// <summary>Thread-pool timer. Callback errors are logged instead of taking the process down.</summary>
public sealed class ThreadingTickTimer(ILogger<ThreadingTickTimer> logger) : ITickTimer
{
    public IDisposable Start(TimeSpan interval, Action onTick)
    {
        return new Timer(
            _ =>
            {
                try
                {
                    onTick();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "A timer callback failed");
                }
            },
            null,
            interval,
            interval);
    }
}
