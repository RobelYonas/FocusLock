using FocusLock.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace FocusLock.Core.Websites;

/// <summary>One finished backend call: whether it was a clear (else an apply) and its result.</summary>
internal sealed record SiteBlockCall(bool IsClear, SiteBlockResult Result);

/// <summary>
/// Turns the website block on and off for sessions. Calls never block the caller and never overlap:
/// a quick on/off/on ends in the last requested state.
/// </summary>
/// <remarks>
/// Requests only record the wanted state and bump a version. A single worker task makes the backend calls,
/// always for the latest wanted state, so requests made while a slow call (a UAC prompt) is open are coalesced.
/// </remarks>
public sealed class WebsiteBlocker(ISiteBlockBackend backend, ILogger<WebsiteBlocker> logger)
{
    private readonly object _lock = new();

    // Wanted state: the domains to block, or null for no block.
    private IReadOnlyList<string>? _wanted;
    private int _version;
    private int _handledVersion;

    // What the hosts file holds, as far as this run knows. Nothing is applied when FocusLock starts.
    private IReadOnlyList<string>? _current;
    private bool _currentKnown = true;
    private Task? _worker;

    /// <summary>Raised after each backend call with its result. Handlers run on a background thread.</summary>
    public event EventHandler<SiteBlockResult>? StatusChanged;

    /// <summary>Like <see cref="StatusChanged"/>, but also says whether the call was a clear.</summary>
    internal event EventHandler<SiteBlockCall>? CallCompleted;

    /// <summary>Requests that exactly <paramref name="domains"/> be blocked. An empty list means nothing to block.</summary>
    public void Activate(IReadOnlyList<string> domains) => Request(domains.Count == 0 ? null : domains.ToList());

    /// <summary>Requests that the block be removed.</summary>
    public void Deactivate() => Request(null);

    /// <summary>Completes when every requested change has finished, so quitting can wait for the clean-up.</summary>
    public Task WhenIdleAsync()
    {
        lock (_lock)
        {
            return _worker ?? Task.CompletedTask;
        }
    }

    /// <summary>App startup with no session running: removes a block left behind by a crash.</summary>
    public Task RecoverAsync(CancellationToken cancellationToken = default)
    {
        bool present;
        try
        {
            present = backend.IsBlockPresent();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not check for a website block left behind");
            return Task.CompletedTask;
        }

        if (!present)
        {
            return Task.CompletedTask;
        }

        logger.LogInformation("Removing a website block left behind by an earlier run");
        lock (_lock)
        {
            _currentKnown = false;
        }

        Request(null);
        return WhenIdleAsync().WaitAsync(cancellationToken);
    }

    private void Request(IReadOnlyList<string>? domains)
    {
        lock (_lock)
        {
            _wanted = domains;
            _version++;
            _worker ??= Task.Run(RunAsync);
        }
    }

    private async Task RunAsync()
    {
        while (true)
        {
            IReadOnlyList<string>? target;
            lock (_lock)
            {
                // Done when every request was handled (a failed one is not retried) or nothing would change.
                if (_handledVersion == _version || (_currentKnown && SameState(_wanted, _current)))
                {
                    _handledVersion = _version;
                    _worker = null;
                    return;
                }

                target = _wanted;
                _handledVersion = _version;
            }

            var result = await CallBackendAsync(target).ConfigureAwait(false);
            lock (_lock)
            {
                switch (result)
                {
                    case SiteBlockResult.Applied:
                        (_current, _currentKnown) = (target, true);
                        break;
                    case SiteBlockResult.Cleared:
                        (_current, _currentKnown) = (null, true);
                        break;
                    case SiteBlockResult.Failed:
                        _currentKnown = false;
                        break;

                    // UserCancelled: nothing ran, so the hosts file is as it was.
                }
            }

            Raise(new SiteBlockCall(target is null, result));
        }
    }

    private async Task<SiteBlockResult> CallBackendAsync(IReadOnlyList<string>? target)
    {
        var action = target is null ? "clear" : "apply";
        try
        {
            var result = target is null
                ? await backend.ClearAsync(CancellationToken.None).ConfigureAwait(false)
                : await backend.ApplyAsync(target, CancellationToken.None).ConfigureAwait(false);
            logger.LogInformation("Website block {Action}: {Result}", action, result);
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Website block {Action} failed", action);
            return SiteBlockResult.Failed;
        }
    }

    private void Raise(SiteBlockCall call)
    {
        // A throwing handler must not stop the worker, or WhenIdleAsync would never complete.
        try
        {
            StatusChanged?.Invoke(this, call.Result);
            CallCompleted?.Invoke(this, call);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "A website block status handler failed");
        }
    }

    private static bool SameState(IReadOnlyList<string>? a, IReadOnlyList<string>? b) =>
        a is null || b is null ? a is null && b is null : a.SequenceEqual(b, StringComparer.Ordinal);
}
