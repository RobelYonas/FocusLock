using FocusLock.Core.Platform;
using FocusLock.Tests.Fakes;

namespace FocusLock.Tests.Platform;

public class SiteBlockHelperTests
{
    private const string ChromeMode = @"SOFTWARE\Policies\Google\Chrome|DnsOverHttpsMode";

    private readonly FakeSiteBlockSystem _system = new();

    [Fact]
    public void Bad_arguments_change_nothing()
    {
        var exitCode = Run("--site-block", "apply", "youtube.com", "1.2.3.4");

        Assert.Equal(SiteBlockExitCodes.BadArguments, exitCode);
        Assert.Empty(_system.Log);
        Assert.Null(_system.Backup);
    }

    [Fact]
    public void Apply_saves_backup_then_sets_policies_before_touching_hosts()
    {
        SetChromeMode("automatic");

        Run("--site-block", "apply", "youtube.com");

        Assert.Equal("backup", _system.Log[0]);
        Assert.Equal(
            ["set DnsOverHttpsMode=off", "set DnsOverHttpsMode=off", "set Enabled=0", "set Locked=1"],
            _system.Log.Skip(1).Take(4));
        Assert.Equal("automatic", DohPolicyBackup.FromJson(_system.Backup!)!.Values["chrome"].Text);
        Assert.Equal("off", _system.Values[ChromeMode]);
    }

    [Fact]
    public void Apply_does_not_overwrite_an_existing_backup()
    {
        SetChromeMode("secure");
        Run("--site-block", "apply", "youtube.com");
        var first = _system.Backup;

        Run("--site-block", "apply", "reddit.com");

        Assert.Equal(first, _system.Backup);
        Assert.Equal("secure", DohPolicyBackup.FromJson(_system.Backup!)!.Values["chrome"].Text);
        Assert.Single(_system.Log, entry => entry == "backup");
    }

    [Fact]
    public void Apply_stops_before_hosts_when_policies_fail()
    {
        _system.FailPolicies = true;

        Assert.Equal(SiteBlockExitCodes.PolicyFailed, Run("--site-block", "apply", "youtube.com"));
        Assert.DoesNotContain("hosts", _system.Log);
    }

    [Fact]
    public void Clear_restores_policies_and_deletes_created_keys()
    {
        SetChromeMode("secure");
        Run("--site-block", "apply", "youtube.com");
        _system.Log.Clear();

        var exitCode = Run("--site-block", "clear");

        Assert.Equal(SiteBlockExitCodes.Success, exitCode);
        Assert.Equal("secure", _system.Values[ChromeMode]);
        Assert.Single(_system.Values);
        Assert.DoesNotContain(@"SOFTWARE\Policies\Mozilla", _system.Keys);
        Assert.Contains(@"SOFTWARE\Policies\Google\Chrome", _system.Keys);
        Assert.Contains(@"SOFTWARE\Policies\Microsoft", _system.Keys);
        Assert.Null(_system.Backup);
        Assert.Equal(["delete-backup", "flush"], _system.Log.TakeLast(2));
    }

    [Fact]
    public void Clear_keeps_backup_when_hosts_cannot_be_written()
    {
        _system.Backup = new DohPolicyBackup().ToJson();
        _system.Hosts = "127.0.0.1 localhost\r\n# FocusLock begin\r\n0.0.0.0 youtube.com\r\n# FocusLock end\r\n";
        _system.FailHostsWrite = true;

        Assert.Equal(SiteBlockExitCodes.HostsFailed, Run("--site-block", "clear"));
        Assert.NotNull(_system.Backup);
    }

    [Fact]
    public void Clear_with_nothing_to_clear_succeeds()
    {
        Assert.Equal(SiteBlockExitCodes.Success, Run("--site-block", "clear"));
        Assert.DoesNotContain("hosts", _system.Log);
    }

    private void SetChromeMode(string mode)
    {
        _system.SetPolicyValue(@"SOFTWARE\Policies\Google\Chrome", "DnsOverHttpsMode", mode);
        _system.Log.Clear();
    }

    private int Run(params string[] args) => new SiteBlockHelper(_system, SiteBlockCommandTests.SimpleIsValid).Execute(args);
}
