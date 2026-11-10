using FocusLock.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace FocusLock.Core.Blocking;

/// <summary>While active, closes every newly started process that matches the policy and reports it.</summary>
public sealed class BlockingEngine(
    IProcessWatcher watcher,
    IProcessTerminator terminator,
    BlockRuleEvaluator evaluator,
    ILogger<BlockingEngine> logger)
{
    private readonly object _gate = new();
    private BlockPolicy? _policy;

    public event EventHandler<LaunchBlockedEventArgs>? LaunchBlocked;

    public bool IsActive
    {
        get
        {
            lock (_gate)
            {
                return _policy is not null;
            }
        }
    }

    /// <summary>Starts enforcing <paramref name="policy"/>. With <paramref name="closeAlreadyRunning"/>, matching processes that are already open are closed too.</summary>
    public void Activate(BlockPolicy policy, bool closeAlreadyRunning)
    {
        lock (_gate)
        {
            if (_policy is null)
            {
                watcher.ProcessStarted += OnProcessStarted;
                watcher.Start();
            }

            _policy = policy;
        }

        logger.LogInformation("Blocking is on: {RuleCount} rules, {AllowCount} exceptions", policy.Rules.Count, policy.Allow.Count);

        if (closeAlreadyRunning)
        {
            foreach (var process in SafeSnapshot())
            {
                Handle(process);
            }
        }
    }

    public void Deactivate()
    {
        lock (_gate)
        {
            if (_policy is null)
            {
                return;
            }

            _policy = null;
            watcher.ProcessStarted -= OnProcessStarted;
            watcher.Stop();
        }

        logger.LogInformation("Blocking is off");
    }

    private void OnProcessStarted(object? sender, ProcessInfo process) => Handle(process);

    private void Handle(ProcessInfo process)
    {
        BlockPolicy? policy;
        lock (_gate)
        {
            policy = _policy;
        }

        if (policy is null)
        {
            return;
        }

        var decision = evaluator.Evaluate(policy, process);
        if (!decision.ShouldBlock || decision.MatchedRule is null)
        {
            return;
        }

        var gameName = decision.MatchedRule.Name;
        var result = terminator.Terminate(process.Id);
        switch (result)
        {
            case TerminateResult.Terminated:
                logger.LogInformation("Closed {Game} ({ProcessName}, pid {Pid})", gameName, process.Name, process.Id);
                break;
            case TerminateResult.AlreadyExited:
                logger.LogDebug("{Game} ({ProcessName}, pid {Pid}) had already exited", gameName, process.Name, process.Id);
                break;
            case TerminateResult.AccessDenied:
                logger.LogWarning("Could not close {Game} ({ProcessName}, pid {Pid}): access denied", gameName, process.Name, process.Id);
                break;
            default:
                logger.LogWarning("Could not close {Game} ({ProcessName}, pid {Pid})", gameName, process.Name, process.Id);
                break;
        }

        LaunchBlocked?.Invoke(this, new LaunchBlockedEventArgs(gameName, process, result));
    }

    private IReadOnlyList<ProcessInfo> SafeSnapshot()
    {
        try
        {
            return watcher.Snapshot();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not list running programs for the initial sweep");
            return [];
        }
    }
}
