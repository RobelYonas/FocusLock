using FocusLock.Core.Websites;

namespace FocusLock.Core.Platform;

/// <summary>The system calls the elevated helper needs. Internal seam so the helper's ordering and backup rules can be tested.</summary>
internal interface ISiteBlockSystem
{
    string ReadHosts();

    /// <summary>Replaces the hosts file atomically, keeping its encoding and ACL.</summary>
    void WriteHosts(string text);

    /// <summary>The saved policy backup, or null when there is none (or it could not be trusted and was removed).</summary>
    string? ReadBackup();

    /// <summary>Creates the backup file; never overwrites one.</summary>
    void WriteBackup(string json);

    void DeleteBackup();

    bool PolicyKeyExists(string subKey);

    /// <summary>A string for REG_SZ, an int for REG_DWORD, another object for other types, null when missing.</summary>
    object? ReadPolicyValue(string subKey, string name);

    /// <summary>Writes a string as REG_SZ or an int as REG_DWORD, creating the key when needed.</summary>
    void SetPolicyValue(string subKey, string name, object value);

    void DeletePolicyValue(string subKey, string name);

    void DeletePolicyKeyIfEmpty(string subKey);

    /// <summary>Best effort; never throws.</summary>
    void FlushDns();
}

/// <summary>
/// Runs in the elevated FocusLock process started with <c>--site-block apply &lt;domain&gt;... | clear</c>.
/// Writes no logs and shows nothing; the result is only the exit code (<see cref="SiteBlockExitCodes"/>).
/// </summary>
public sealed class SiteBlockHelper
{
    private readonly ISiteBlockSystem _system;
    private readonly Func<string, bool> _isValidDomain;

    internal SiteBlockHelper(ISiteBlockSystem system, Func<string, bool> isValidDomain)
    {
        _system = system;
        _isValidDomain = isValidDomain;
    }

    public static bool IsHelperInvocation(IReadOnlyList<string> args) => SiteBlockCommand.IsHelperInvocation(args);

    /// <summary>Entry point for the elevated process. Returns the exit code.</summary>
    public static int Run(IReadOnlyList<string> args) => new SiteBlockHelper(new WindowsSiteBlockSystem(), DomainName.IsValid).Execute(args);

    internal int Execute(IReadOnlyList<string> args)
    {
        SiteBlockCommand? command;
        try
        {
            command = SiteBlockCommand.TryParse(args, _isValidDomain);
        }
        catch (Exception)
        {
            return SiteBlockExitCodes.BadArguments;
        }

        if (command is null)
        {
            return SiteBlockExitCodes.BadArguments;
        }

        try
        {
            return command.IsApply ? Apply(command.Domains) : Clear();
        }
        catch (Exception)
        {
            return SiteBlockExitCodes.Unexpected;
        }
    }

    // Order: backup + policies, hosts, flush. A crash at any point leaves a backup that clear can restore from.
    private int Apply(IReadOnlyList<string> domains)
    {
        try
        {
            // A backup already present means the block is on: it holds the user's values, not FocusLock's own.
            if (_system.ReadBackup() is null)
            {
                _system.WriteBackup(DohPolicyBackup.Capture(_system.ReadPolicyValue, _system.PolicyKeyExists).ToJson());
            }

            foreach (var policy in DohPolicies.Values)
            {
                _system.SetPolicyValue(policy.SubKey, policy.Name, policy.BlockValue);
            }
        }
        catch (Exception)
        {
            return SiteBlockExitCodes.PolicyFailed;
        }

        if (!TryEditHosts(text => HostsFileEditor.Apply(text, domains)))
        {
            return SiteBlockExitCodes.HostsFailed;
        }

        _system.FlushDns();
        return SiteBlockExitCodes.Success;
    }

    // Order: restore policies, hosts, delete backup. The hosts section is always removed, even when the
    // policies fail, so the sites never stay blocked; the backup goes only after both worked.
    private int Clear()
    {
        string? backupJson = null;
        var policiesRestored = true;
        try
        {
            backupJson = _system.ReadBackup();
            if (backupJson is not null)
            {
                var plan = DohPolicyBackup.PlanRestore(DohPolicyBackup.FromJson(backupJson));
                foreach (var (policy, value) in plan.Values)
                {
                    if (value is null)
                    {
                        _system.DeletePolicyValue(policy.SubKey, policy.Name);
                    }
                    else
                    {
                        _system.SetPolicyValue(policy.SubKey, policy.Name, value);
                    }
                }

                foreach (var key in plan.KeysToDeleteIfEmpty)
                {
                    _system.DeletePolicyKeyIfEmpty(key);
                }
            }
        }
        catch (Exception)
        {
            policiesRestored = false;
        }

        var hostsCleared = TryEditHosts(HostsFileEditor.Remove);
        if (hostsCleared)
        {
            _system.FlushDns();
        }

        if (!policiesRestored)
        {
            return SiteBlockExitCodes.PolicyFailed;
        }

        if (!hostsCleared)
        {
            return SiteBlockExitCodes.HostsFailed;
        }

        if (backupJson is not null)
        {
            try
            {
                _system.DeleteBackup();
            }
            catch (Exception)
            {
                return SiteBlockExitCodes.PolicyFailed;
            }
        }

        return SiteBlockExitCodes.Success;
    }

    private bool TryEditHosts(Func<string, string> edit)
    {
        try
        {
            var text = _system.ReadHosts();
            var updated = edit(text);
            if (!string.Equals(updated, text, StringComparison.Ordinal))
            {
                _system.WriteHosts(updated);
            }

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
