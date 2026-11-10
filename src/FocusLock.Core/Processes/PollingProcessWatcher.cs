using FocusLock.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace FocusLock.Core.Processes;

/// <summary>Detects new processes by comparing process lists on every tick. Paths are only resolved for new processes.</summary>
public sealed class PollingProcessWatcher(
    IProcessProvider provider,
    ITickTimer timer,
    ProcessWatcherOptions options,
    ILogger<PollingProcessWatcher> logger) : IProcessWatcher
{
    private readonly object _gate = new();
    private HashSet<int> _knownIds = [];
    private IDisposable? _subscription;
    private int _polling;

    public event EventHandler<ProcessInfo>? ProcessStarted;

    public void Start()
    {
        lock (_gate)
        {
            if (_subscription is not null)
            {
                return;
            }

            _knownIds = (TryList() ?? []).Select(process => process.Id).ToHashSet();
            _subscription = timer.Start(options.PollInterval, Poll);
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            _subscription?.Dispose();
            _subscription = null;
        }
    }

    public IReadOnlyList<ProcessInfo> Snapshot() => (TryList() ?? []).Select(Resolve).ToList();

    private void Poll()
    {
        if (Interlocked.Exchange(ref _polling, 1) == 1)
        {
            return; // the previous poll is still running
        }

        try
        {
            var current = TryList();
            if (current is null)
            {
                return;
            }

            var started = new List<ProcessInfo>();
            lock (_gate)
            {
                if (_subscription is null)
                {
                    return;
                }

                var currentIds = new HashSet<int>(current.Count);
                foreach (var process in current)
                {
                    currentIds.Add(process.Id);
                    if (!_knownIds.Contains(process.Id))
                    {
                        started.Add(process);
                    }
                }

                _knownIds = currentIds;
            }

            foreach (var process in started)
            {
                RaiseStarted(Resolve(process));
            }
        }
        finally
        {
            Volatile.Write(ref _polling, 0);
        }
    }

    private ProcessInfo Resolve(ProcessInfo process)
    {
        if (process.ExecutablePath is not null)
        {
            return process;
        }

        var path = provider.TryGetExecutablePath(process.Id);
        if (path is null)
        {
            logger.LogDebug("Could not read the path of {ProcessName} (pid {Pid}); it may have exited or be protected", process.Name, process.Id);
        }

        return process with { ExecutablePath = path };
    }

    private IReadOnlyList<ProcessInfo>? TryList()
    {
        try
        {
            return provider.ListProcesses();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Listing running programs failed; will retry on the next tick");
            return null;
        }
    }

    private void RaiseStarted(ProcessInfo process)
    {
        try
        {
            ProcessStarted?.Invoke(this, process);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Handling the start of {ProcessName} failed", process.Name);
        }
    }
}
