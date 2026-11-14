using System.ComponentModel;
using System.Text;
using FocusLock.Core.Abstractions;
using FocusLock.Core.Platform;
using FocusLock.Tests.Fakes;

namespace FocusLock.Tests.Platform;

public class ElevatedSiteBlockBackendTests
{
    private readonly FakeFileSystem _files = new();
    private readonly FakeAppEnvironment _environment = new();
    private readonly ListLogger<ElevatedSiteBlockBackend> _logger = new();

    [Fact]
    public void Declined_uac_prompt_is_user_cancelled()
    {
        Assert.Equal(SiteBlockResult.UserCancelled, ElevatedSiteBlockBackend.MapStartFailure(new Win32Exception(1223)));
        Assert.Equal(SiteBlockResult.Failed, ElevatedSiteBlockBackend.MapStartFailure(new Win32Exception(2)));
        Assert.Equal(SiteBlockResult.Failed, ElevatedSiteBlockBackend.MapStartFailure(new InvalidOperationException()));
    }

    [Fact]
    public void Block_is_present_while_the_policy_backup_exists()
    {
        _files.AddFile(@"C:\ProgramData\FocusLock\doh-backup.json", "{}");

        Assert.True(Backend().IsBlockPresent());
    }

    [Fact]
    public void Block_is_absent_when_nothing_is_there()
    {
        Assert.False(Backend().IsBlockPresent());
    }

    [Fact]
    public void Block_check_logs_and_returns_false_when_hosts_is_unreadable()
    {
        const string hosts = @"C:\Windows\System32\drivers\etc\hosts";
        _files.AddFile(hosts, "# FocusLock begin");
        _files.UnreadablePaths.Add(hosts);

        Assert.False(Backend().IsBlockPresent());
        Assert.NotEmpty(_logger.Entries);
    }

    // Calls HostsFileEditor.ContainsBlock (Task B).
    [Fact]
    public void Block_is_present_when_hosts_has_a_focuslock_section()
    {
        _files.AddFile(@"C:\Windows\System32\drivers\etc\hosts", "127.0.0.1 localhost\r\n# FocusLock begin\r\n0.0.0.0 youtube.com\r\n# FocusLock end\r\n");

        Assert.True(Backend().IsBlockPresent());
    }

    [Theory]
    [InlineData(new byte[] { 0xEF, 0xBB, 0xBF, (byte)'a' }, 3)]
    [InlineData(new byte[] { 0xFF, 0xFE, (byte)'a', 0 }, 2)]
    [InlineData(new byte[] { (byte)'a', (byte)'b' }, 0)]
    public void Detects_hosts_encoding_from_bom(byte[] bytes, int preambleLength)
    {
        var (encoding, length) = WindowsSiteBlockSystem.DetectEncoding(bytes);

        Assert.Equal(preambleLength, length);
        Assert.Equal("a", encoding.GetString(bytes, length, bytes.Length - length)[..1]);
    }

    [Fact]
    public void Ansi_hosts_file_round_trips_byte_for_byte()
    {
        byte[] bytes = [(byte)'#', 0xE9, 0xFC, (byte)'\r', (byte)'\n'];

        var (encoding, length) = WindowsSiteBlockSystem.DetectEncoding(bytes);

        Assert.Equal(bytes, encoding.GetBytes(encoding.GetString(bytes, length, bytes.Length)));
        Assert.Equal(Encoding.Latin1.WebName, encoding.WebName);
    }

    private ElevatedSiteBlockBackend Backend() => new(_logger, _environment, _files);
}
