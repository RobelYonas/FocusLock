using FocusLock.Core.Abstractions;
using FocusLock.Core.Websites;
using FocusLock.Tests.Fakes;
using Microsoft.Extensions.Logging;

namespace FocusLock.Tests.Websites;

public class WebsiteBlockerTests
{
    private static readonly string[] Domains = ["youtube.com", "www.youtube.com"];

    private readonly FakeSiteBlockBackend _backend = new();
    private readonly ListLogger<WebsiteBlocker> _logger = new();
    private readonly WebsiteBlocker _blocker;
    private readonly List<SiteBlockResult> _results = [];

    public WebsiteBlockerTests()
    {
        _blocker = new WebsiteBlocker(_backend, _logger);
        _blocker.StatusChanged += (_, result) => _results.Add(result);
    }

    [Fact]
    public async Task Activate_applies_the_domains_and_reports_the_result()
    {
        _blocker.Activate(Domains);
        await _blocker.WhenIdleAsync();

        Assert.Equal(["apply:youtube.com,www.youtube.com"], _backend.Calls);
        Assert.Equal([SiteBlockResult.Applied], _results);
    }

    [Fact]
    public async Task Deactivate_clears_what_was_applied()
    {
        _blocker.Activate(Domains);
        await _blocker.WhenIdleAsync();

        _blocker.Deactivate();
        await _blocker.WhenIdleAsync();

        Assert.Equal(["apply:youtube.com,www.youtube.com", "clear"], _backend.Calls);
        Assert.Equal([SiteBlockResult.Applied, SiteBlockResult.Cleared], _results);
    }

    [Fact]
    public async Task Nothing_is_called_when_nothing_was_applied_or_nothing_would_change()
    {
        _blocker.Deactivate();
        _blocker.Activate([]);
        await _blocker.WhenIdleAsync();
        Assert.Empty(_backend.Calls);

        _blocker.Activate(Domains);
        await _blocker.WhenIdleAsync();
        _blocker.Activate(Domains);
        await _blocker.WhenIdleAsync();

        Assert.Single(_backend.Calls);
    }

    [Fact]
    public async Task Activating_with_no_domains_clears_the_block()
    {
        _blocker.Activate(Domains);
        await _blocker.WhenIdleAsync();

        _blocker.Activate([]);
        await _blocker.WhenIdleAsync();

        Assert.Equal("clear", _backend.Calls[^1]);
    }

    [Fact]
    public async Task Quick_flips_while_a_call_is_pending_end_in_the_last_state_with_one_more_call()
    {
        _backend.Hold = new TaskCompletionSource();
        _blocker.Activate(Domains);
        await _backend.WaitForCallAsync();

        _blocker.Deactivate();
        _blocker.Activate(Domains);
        _blocker.Deactivate();
        var idle = _blocker.WhenIdleAsync();
        Assert.False(idle.IsCompleted);

        _backend.Hold.SetResult();
        await idle;

        Assert.Equal(["apply:youtube.com,www.youtube.com", "clear"], _backend.Calls);
    }

    [Fact]
    public async Task Flipping_back_to_the_pending_state_needs_no_further_call()
    {
        _backend.Hold = new TaskCompletionSource();
        _blocker.Activate(Domains);
        await _backend.WaitForCallAsync();

        _blocker.Deactivate();
        _blocker.Activate(Domains);
        _backend.Hold.SetResult();
        await _blocker.WhenIdleAsync();

        Assert.Single(_backend.Calls);
    }

    [Fact]
    public async Task A_throwing_backend_is_logged_and_reported_as_failed()
    {
        _backend.ThrowOnCall = true;

        _blocker.Activate(Domains);
        await _blocker.WhenIdleAsync();

        Assert.Equal([SiteBlockResult.Failed], _results);
        Assert.True(_logger.Has(LogLevel.Error, "apply"));
    }

    [Fact]
    public async Task After_a_failed_apply_the_state_is_unknown_so_deactivate_still_clears()
    {
        _backend.ApplyResult = SiteBlockResult.Failed;
        _blocker.Activate(Domains);
        await _blocker.WhenIdleAsync();

        _blocker.Deactivate();
        await _blocker.WhenIdleAsync();

        Assert.Equal("clear", _backend.Calls[^1]);
    }

    [Fact]
    public async Task After_a_declined_apply_nothing_changed_so_deactivate_needs_no_call()
    {
        _backend.ApplyResult = SiteBlockResult.UserCancelled;
        _blocker.Activate(Domains);
        await _blocker.WhenIdleAsync();

        _blocker.Deactivate();
        await _blocker.WhenIdleAsync();

        Assert.Single(_backend.Calls);
        Assert.Equal([SiteBlockResult.UserCancelled], _results);
    }

    [Fact]
    public async Task A_throwing_status_handler_does_not_stop_the_worker()
    {
        _blocker.StatusChanged += (_, _) => throw new InvalidOperationException("Handler bug.");

        _blocker.Activate(Domains);
        await _blocker.WhenIdleAsync();
        _blocker.Deactivate();
        await _blocker.WhenIdleAsync();

        Assert.Equal(2, _backend.Calls.Count);
    }

    [Fact]
    public async Task Recovery_clears_a_block_left_behind()
    {
        _backend.BlockPresent = true;

        await _blocker.RecoverAsync();

        Assert.Equal(["clear"], _backend.Calls);
    }

    [Fact]
    public async Task Recovery_does_nothing_when_no_block_is_present()
    {
        await _blocker.RecoverAsync();

        Assert.Empty(_backend.Calls);
    }

    [Fact]
    public async Task Recovery_is_skipped_when_the_check_fails()
    {
        _backend.ThrowOnIsBlockPresent = true;

        await _blocker.RecoverAsync();

        Assert.Empty(_backend.Calls);
        Assert.True(_logger.Has(LogLevel.Warning, "block left behind"));
    }
}
