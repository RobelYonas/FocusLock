using FocusLock.Core.Platform;

namespace FocusLock.Tests.Fakes;

/// <summary>In-memory hosts file, policy registry and backup file for the elevated helper. Records every change in order.</summary>
internal sealed class FakeSiteBlockSystem : ISiteBlockSystem
{
    public string Hosts { get; set; } = "127.0.0.1 localhost\r\n";

    public string? Backup { get; set; }

    /// <summary>Policy values keyed by "subKey|name".</summary>
    public Dictionary<string, object> Values { get; } = new(StringComparer.OrdinalIgnoreCase);

    public HashSet<string> Keys { get; } = new(StringComparer.OrdinalIgnoreCase) { @"SOFTWARE\Policies\Microsoft" };

    public List<string> Log { get; } = [];

    public bool FailPolicies { get; set; }

    public bool FailHostsWrite { get; set; }

    public string ReadHosts() => Hosts;

    public void WriteHosts(string text)
    {
        if (FailHostsWrite)
        {
            throw new IOException("The file is in use.");
        }

        Log.Add("hosts");
        Hosts = text;
    }

    public string? ReadBackup() => Backup;

    public void WriteBackup(string json)
    {
        if (Backup is not null)
        {
            throw new IOException("Backup already exists.");
        }

        Log.Add("backup");
        Backup = json;
    }

    public void DeleteBackup()
    {
        Log.Add("delete-backup");
        Backup = null;
    }

    public bool PolicyKeyExists(string subKey) => Keys.Contains(subKey);

    public object? ReadPolicyValue(string subKey, string name) => Values.GetValueOrDefault(subKey + "|" + name);

    public void SetPolicyValue(string subKey, string name, object value)
    {
        if (FailPolicies)
        {
            throw new UnauthorizedAccessException("Access denied.");
        }

        Log.Add($"set {name}={value}");
        var path = subKey;
        while (path.Contains('\\'))
        {
            Keys.Add(path);
            path = path[..path.LastIndexOf('\\')];
        }

        Values[subKey + "|" + name] = value;
    }

    public void DeletePolicyValue(string subKey, string name)
    {
        if (FailPolicies)
        {
            throw new UnauthorizedAccessException("Access denied.");
        }

        Log.Add($"delete {name}");
        Values.Remove(subKey + "|" + name);
    }

    public void DeletePolicyKeyIfEmpty(string subKey)
    {
        var hasContent = Values.Keys.Any(key => key.StartsWith(subKey + "|", StringComparison.OrdinalIgnoreCase))
            || Keys.Any(key => key.StartsWith(subKey + "\\", StringComparison.OrdinalIgnoreCase));
        if (!hasContent && Keys.Remove(subKey))
        {
            Log.Add("delete-key " + subKey);
        }
    }

    public void FlushDns() => Log.Add("flush");
}
