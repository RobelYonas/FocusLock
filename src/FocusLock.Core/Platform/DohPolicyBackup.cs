using System.Text.Json;
using System.Text.Json.Serialization;

namespace FocusLock.Core.Platform;

/// <summary>One browser policy value FocusLock overrides to turn DNS-over-HTTPS off. The value is a string (REG_SZ) or an int (REG_DWORD).</summary>
internal sealed record DohPolicyValue(string Id, string SubKey, string Name, object BlockValue, Func<object?, bool> IsAllowed);

/// <summary>The fixed list of policy values and keys FocusLock may ever touch. Nothing read from the backup can widen it.</summary>
internal static class DohPolicies
{
    private const string Chrome = @"SOFTWARE\Policies\Google\Chrome";
    private const string Edge = @"SOFTWARE\Policies\Microsoft\Edge";
    private const string FirefoxDoh = @"SOFTWARE\Policies\Mozilla\Firefox\DNSOverHTTPS";

    private static readonly string[] Modes = ["off", "automatic", "secure"];

    public static IReadOnlyList<DohPolicyValue> Values { get; } =
    [
        new("chrome", Chrome, "DnsOverHttpsMode", "off", IsMode),
        new("edge", Edge, "DnsOverHttpsMode", "off", IsMode),
        new("firefoxEnabled", FirefoxDoh, "Enabled", 0, IsFlag),
        new("firefoxLocked", FirefoxDoh, "Locked", 1, IsFlag),
    ];

    /// <summary>Every key FocusLock may create (and later delete when empty), parents before children.</summary>
    public static IReadOnlyList<string> Keys { get; } =
    [
        @"SOFTWARE\Policies\Google",
        Chrome,
        @"SOFTWARE\Policies\Microsoft",
        Edge,
        @"SOFTWARE\Policies\Mozilla",
        @"SOFTWARE\Policies\Mozilla\Firefox",
        FirefoxDoh,
    ];

    private static bool IsMode(object? value) => value is string text && Modes.Contains(text, StringComparer.Ordinal);

    private static bool IsFlag(object? value) => value is int number and (0 or 1);
}

/// <summary>The browser policies as they were before FocusLock changed them, saved to %ProgramData%\FocusLock\doh-backup.json.</summary>
internal sealed class DohPolicyBackup
{
    public int Version { get; set; } = 1;

    public Dictionary<string, DohSavedValue> Values { get; set; } = [];

    public List<string> CreatedKeys { get; set; } = [];

    /// <summary>Snapshot of the current values and of which known keys are missing (and so will be created by FocusLock).</summary>
    public static DohPolicyBackup Capture(Func<string, string, object?> readValue, Func<string, bool> keyExists) => new()
    {
        Values = DohPolicies.Values.ToDictionary(
            policy => policy.Id,
            policy => readValue(policy.SubKey, policy.Name) switch
            {
                null => new DohSavedValue(),
                string text => new DohSavedValue { Existed = true, Text = text },
                int number => new DohSavedValue { Existed = true, Number = number },

                // Another registry type: remembered as present but not restorable, so clear deletes it.
                _ => new DohSavedValue { Existed = true },
            }),
        CreatedKeys = DohPolicies.Keys.Where(key => !keyExists(key)).ToList(),
    };

    public string ToJson() => JsonSerializer.Serialize(this, DohBackupJsonContext.Default.DohPolicyBackup);

    /// <summary>Null when the text is not a backup FocusLock understands.</summary>
    public static DohPolicyBackup? FromJson(string json)
    {
        try
        {
            return JsonSerializer.Deserialize(json, DohBackupJsonContext.Default.DohPolicyBackup);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// What clear writes back. Only the known values are considered: a saved value of the expected type and range is restored,
    /// anything else (missing, out of range, wrong type, unreadable backup) means the value is deleted.
    /// Keys are only the known ones the backup says FocusLock created (all known ones for an unreadable backup),
    /// children first; they are deleted only when empty.
    /// </summary>
    public static DohRestorePlan PlanRestore(DohPolicyBackup? backup)
    {
        var values = DohPolicies.Values
            .Select(policy =>
            {
                object? saved = null;
                if (backup?.Values?.TryGetValue(policy.Id, out var entry) == true && entry is { Existed: true })
                {
                    saved = (object?)entry.Text ?? entry.Number;
                }

                return (policy, policy.IsAllowed(saved) ? saved : null);
            })
            .ToList();

        // An unreadable backup can't say which keys FocusLock created: all known keys are candidates (deleted only when empty).
        var created = backup is null ? DohPolicies.Keys : backup.CreatedKeys ?? [];
        var keys = DohPolicies.Keys.Where(key => created.Contains(key, StringComparer.OrdinalIgnoreCase)).Reverse().ToList();
        return new DohRestorePlan(values, keys);
    }
}

internal sealed class DohSavedValue
{
    public bool Existed { get; set; }

    public string? Text { get; set; }

    public int? Number { get; set; }
}

/// <summary>Each known value with what to write back (null = delete it), and the keys to delete when empty.</summary>
internal sealed record DohRestorePlan(IReadOnlyList<(DohPolicyValue Policy, object? Value)> Values, IReadOnlyList<string> KeysToDeleteIfEmpty);

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(DohPolicyBackup))]
internal sealed partial class DohBackupJsonContext : JsonSerializerContext
{
}
