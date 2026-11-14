using FocusLock.Core.Platform;

namespace FocusLock.Tests.Platform;

public class DohPolicyBackupTests
{
    private const string Chrome = @"SOFTWARE\Policies\Google\Chrome";

    [Fact]
    public void Capture_round_trips_through_json()
    {
        var current = new Dictionary<string, object> { [Chrome + "|DnsOverHttpsMode"] = "secure" };
        var backup = DohPolicyBackup.Capture((key, name) => current.GetValueOrDefault(key + "|" + name), key => key == @"SOFTWARE\Policies\Microsoft");

        var restored = DohPolicyBackup.FromJson(backup.ToJson());

        Assert.NotNull(restored);
        Assert.Equal("secure", restored.Values["chrome"].Text);
        Assert.False(restored.Values["edge"].Existed);
        Assert.Contains(@"SOFTWARE\Policies\Google", restored.CreatedKeys);
        Assert.DoesNotContain(@"SOFTWARE\Policies\Microsoft", restored.CreatedKeys);
    }

    [Fact]
    public void Plan_restores_valid_values_and_deletes_the_rest()
    {
        var plan = Plan("""
            {
              "values": {
                "chrome": { "existed": true, "text": "automatic" },
                "edge": { "existed": false },
                "firefoxEnabled": { "existed": true, "number": 1 },
                "firefoxLocked": { "existed": true, "number": 0 }
              }
            }
            """);

        Assert.Equal("automatic", plan["chrome"]);
        Assert.Null(plan["edge"]);
        Assert.Equal(1, plan["firefoxEnabled"]);
        Assert.Equal(0, plan["firefoxLocked"]);
    }

    [Fact]
    public void Plan_rejects_out_of_range_and_wrongly_typed_values()
    {
        var plan = Plan("""
            {
              "values": {
                "chrome": { "existed": true, "text": "custom-evil" },
                "edge": { "existed": true, "number": 1 },
                "firefoxEnabled": { "existed": true, "number": 7 },
                "firefoxLocked": { "existed": true, "text": "1" }
              }
            }
            """);

        Assert.All(plan.Values, Assert.Null);
    }

    [Fact]
    public void Plan_ignores_unknown_values_and_keys()
    {
        var backup = DohPolicyBackup.FromJson("""
            {
              "values": { "evil": { "existed": true, "text": "off" } },
              "createdKeys": [ "SOFTWARE\\Microsoft\\Windows", "SOFTWARE\\Policies\\Mozilla\\Firefox\\DNSOverHTTPS", "SOFTWARE\\Policies\\Mozilla" ]
            }
            """);

        var plan = DohPolicyBackup.PlanRestore(backup);

        Assert.Equal(DohPolicies.Values.Select(v => v.Id), plan.Values.Select(v => v.Policy.Id));
        Assert.Equal([@"SOFTWARE\Policies\Mozilla\Firefox\DNSOverHTTPS", @"SOFTWARE\Policies\Mozilla"], plan.KeysToDeleteIfEmpty);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{ "values": 5 }""")]
    [InlineData("""{ "values": { "chrome": { "existed": "yes" } } }""")]
    public void Unreadable_backup_means_delete_every_known_value_and_key(string json)
    {
        var plan = DohPolicyBackup.PlanRestore(DohPolicyBackup.FromJson(json));

        Assert.All(plan.Values, value => Assert.Null(value.Value));
        Assert.Equal(DohPolicies.Keys.Reverse(), plan.KeysToDeleteIfEmpty);
    }

    private static Dictionary<string, object?> Plan(string json) =>
        DohPolicyBackup.PlanRestore(DohPolicyBackup.FromJson(json)).Values.ToDictionary(v => v.Policy.Id, v => v.Value);
}
