using FocusLock.Core.Abstractions;
using FocusLock.Core.Websites;

namespace FocusLock.Tests.Fakes;

/// <summary>Records apply/clear calls. Set <see cref="Hold"/> to keep calls pending, like an open UAC prompt.</summary>
public sealed class FakeSiteBlockBackend : ISiteBlockBackend
{
    private readonly object _lock = new();
    private readonly List<string> _calls = [];
    private readonly SemaphoreSlim _started = new(0);

    /// <summary>"apply:a.com,b.com" or "clear", in call order.</summary>
    public IReadOnlyList<string> Calls
    {
        get
        {
            lock (_lock)
            {
                return _calls.ToList();
            }
        }
    }

    public SiteBlockResult ApplyResult { get; set; } = SiteBlockResult.Applied;

    public SiteBlockResult ClearResult { get; set; } = SiteBlockResult.Cleared;

    public bool ThrowOnCall { get; set; }

    public bool BlockPresent { get; set; }

    public bool ThrowOnIsBlockPresent { get; set; }

    /// <summary>When set, every call waits for this before returning.</summary>
    public TaskCompletionSource? Hold { get; set; }

    /// <summary>Waits until a call has started (one per started call).</summary>
    public async Task WaitForCallAsync()
    {
        Assert.True(await _started.WaitAsync(TimeSpan.FromSeconds(10)), "No backend call started.");
    }

    public Task<SiteBlockResult> ApplyAsync(IReadOnlyList<string> domains, CancellationToken cancellationToken) =>
        CallAsync("apply:" + string.Join(',', domains), ApplyResult);

    public Task<SiteBlockResult> ClearAsync(CancellationToken cancellationToken) => CallAsync("clear", ClearResult);

    public bool IsBlockPresent() => ThrowOnIsBlockPresent ? throw new IOException("Hosts file locked.") : BlockPresent;

    private async Task<SiteBlockResult> CallAsync(string call, SiteBlockResult result)
    {
        lock (_lock)
        {
            _calls.Add(call);
        }

        _started.Release();
        if (Hold is { } hold)
        {
            await hold.Task;
        }

        return ThrowOnCall ? throw new InvalidOperationException("Helper crashed.") : result;
    }
}

public sealed class FakeSitePresetCatalog(params SitePreset[] presets) : ISitePresetCatalog
{
    public IReadOnlyList<SitePreset> All { get; } = presets;
}
