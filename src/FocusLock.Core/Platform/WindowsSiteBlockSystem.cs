using System.Diagnostics;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32;

namespace FocusLock.Core.Platform;

/// <summary>The real hosts file, policy registry keys, backup file and DNS cache, as used by the elevated helper.</summary>
internal sealed class WindowsSiteBlockSystem : ISiteBlockSystem
{
    private const int Attempts = 5;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(200);
    private static readonly SecurityIdentifier Administrators = new(WellKnownSidType.BuiltinAdministratorsSid, null);
    private static readonly SecurityIdentifier LocalSystem = new(WellKnownSidType.LocalSystemSid, null);
    private static readonly SecurityIdentifier Users = new(WellKnownSidType.BuiltinUsersSid, null);

    private readonly string _hostsPath = SiteBlockPaths.HostsFile(Environment.GetFolderPath(Environment.SpecialFolder.Windows));
    private readonly string _backupFolder = SiteBlockPaths.BackupFolder(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData));
    private Encoding _hostsEncoding = new UTF8Encoding(false);

    private string BackupPath => Path.Combine(_backupFolder, SiteBlockPaths.BackupFileName);

    public string ReadHosts()
    {
        var bytes = WithRetry(() => File.ReadAllBytes(_hostsPath));
        (_hostsEncoding, var preambleLength) = DetectEncoding(bytes);
        return _hostsEncoding.GetString(bytes, preambleLength, bytes.Length - preambleLength);
    }

    public void WriteHosts(string text)
    {
        var folder = Path.GetDirectoryName(_hostsPath)!;
        var tempPath = Path.Combine(folder, $"hosts.focuslock-{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                var preamble = _hostsEncoding.GetPreamble();
                stream.Write(preamble);
                stream.Write(_hostsEncoding.GetBytes(text));
            }

            // File.Replace keeps the original file's ACL, owner and attributes.
            WithRetry(() =>
            {
                if (File.Exists(_hostsPath))
                {
                    File.Replace(tempPath, _hostsPath, destinationBackupFileName: null, ignoreMetadataErrors: true);
                }
                else
                {
                    File.Move(tempPath, _hostsPath);
                }

                return 0;
            });
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    public string? ReadBackup()
    {
        EnsureBackupFolder();
        var path = BackupPath;
        if (!File.Exists(path) && !Directory.Exists(path))
        {
            return null;
        }

        // Only a plain file created by an administrator is trusted; anything else was planted and is removed.
        var attributes = File.GetAttributes(path);
        if (attributes.HasFlag(FileAttributes.ReparsePoint) || attributes.HasFlag(FileAttributes.Directory) || !IsAdminOwned(new FileInfo(path).GetAccessControl(AccessControlSections.Owner)))
        {
            DeleteEntry(path, attributes);
            return null;
        }

        return File.ReadAllText(path);
    }

    public void WriteBackup(string json)
    {
        EnsureBackupFolder();

        // CreateNew never writes through an existing file or link.
        using var stream = new FileStream(BackupPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);

        // ReadBackup trusts only admin-owned files; don't depend on the "default owner" group policy.
        var owner = new FileSecurity();
        owner.SetOwner(Administrators);
        stream.SetAccessControl(owner);
        stream.Write(new UTF8Encoding(false).GetBytes(json));
    }

    public void DeleteBackup() => File.Delete(BackupPath);

    public bool PolicyKeyExists(string subKey)
    {
        using var key = OpenMachine().OpenSubKey(subKey);
        return key is not null;
    }

    public object? ReadPolicyValue(string subKey, string name)
    {
        using var key = OpenMachine().OpenSubKey(subKey);
        return key?.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
    }

    public void SetPolicyValue(string subKey, string name, object value)
    {
        using var key = OpenMachine().CreateSubKey(subKey, writable: true);
        switch (value)
        {
            case string text:
                key.SetValue(name, text, RegistryValueKind.String);
                break;
            case int number:
                key.SetValue(name, number, RegistryValueKind.DWord);
                break;
            default:
                throw new ArgumentException("Only REG_SZ and REG_DWORD values are written.", nameof(value));
        }
    }

    public void DeletePolicyValue(string subKey, string name)
    {
        using var key = OpenMachine().OpenSubKey(subKey, writable: true);
        key?.DeleteValue(name, throwOnMissingValue: false);
    }

    public void DeletePolicyKeyIfEmpty(string subKey)
    {
        using var machine = OpenMachine();
        using (var key = machine.OpenSubKey(subKey))
        {
            if (key is null || key.ValueCount > 0 || key.SubKeyCount > 0)
            {
                return;
            }
        }

        machine.DeleteSubKey(subKey, throwOnMissingSubKey: false);
    }

    public void FlushDns()
    {
        try
        {
            if (NativeMethods.FlushDnsCache())
            {
                return;
            }
        }
        catch (Exception)
        {
            // Fall through to ipconfig.
        }

        try
        {
            using var process = Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "ipconfig.exe"), "/flushdns")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            process?.WaitForExit(10_000);
        }
        catch (Exception)
        {
            // Best effort: the cache entries expire on their own.
        }
    }

    internal static (Encoding Encoding, int PreambleLength) DetectEncoding(byte[] bytes)
    {
        if (bytes is [0xEF, 0xBB, 0xBF, ..])
        {
            return (new UTF8Encoding(true), 3);
        }

        if (bytes is [0xFF, 0xFE, ..])
        {
            return (new UnicodeEncoding(false, true), 2);
        }

        if (bytes is [0xFE, 0xFF, ..])
        {
            return (new UnicodeEncoding(true, true), 2);
        }

        try
        {
            _ = new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes);
            return (new UTF8Encoding(false), 0);
        }
        catch (DecoderFallbackException)
        {
            // An ANSI file: Latin-1 maps every byte to itself, so the lines FocusLock doesn't touch stay byte-for-byte.
            return (Encoding.Latin1, 0);
        }
    }

    private static RegistryKey OpenMachine() => RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);

    private static T WithRetry<T>(Func<T> action)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return action();
            }
            catch (IOException) when (attempt < Attempts)
            {
                // Usually an antivirus scanner holding the file for a moment.
                Thread.Sleep(RetryDelay);
            }
        }
    }

    private static bool IsAdminOwned(ObjectSecurity security) =>
        security.GetOwner(typeof(SecurityIdentifier)) is SecurityIdentifier owner && (owner == Administrators || owner == LocalSystem);

    private static void DeleteEntry(string path, FileAttributes attributes)
    {
        // Deleting a link removes only the link; a recursive delete never follows junctions inside.
        if (attributes.HasFlag(FileAttributes.Directory))
        {
            Directory.Delete(path, recursive: !attributes.HasFlag(FileAttributes.ReparsePoint));
        }
        else
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Makes %ProgramData%\FocusLock a real folder owned by Administrators that only Administrators and SYSTEM can change.
    /// A normal user can create that folder first, so a link or a user-owned folder is removed and created again.
    /// </summary>
    private void EnsureBackupFolder()
    {
        var path = _backupFolder;
        if (File.Exists(path) || Directory.Exists(path))
        {
            var attributes = File.GetAttributes(path);
            var trusted = attributes.HasFlag(FileAttributes.Directory)
                && !attributes.HasFlag(FileAttributes.ReparsePoint)
                && IsAdminOwned(new DirectoryInfo(path).GetAccessControl(AccessControlSections.Owner));
            if (!trusted)
            {
                DeleteEntry(path, attributes);
            }
        }

        var security = FolderSecurity();
        var folder = new DirectoryInfo(path);
        if (!folder.Exists)
        {
            folder.Create(security);
        }

        folder.Refresh();
        if (folder.Attributes.HasFlag(FileAttributes.ReparsePoint) || !IsAdminOwned(folder.GetAccessControl(AccessControlSections.Owner)))
        {
            throw new IOException("The FocusLock data folder could not be secured.");
        }

        // Admin-owned and not a link, so no normal user can swap it before this call.
        folder.SetAccessControl(security);
    }

    private static DirectorySecurity FolderSecurity()
    {
        const InheritanceFlags inherit = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
        var security = new DirectorySecurity();
        security.SetOwner(Administrators);
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.AddAccessRule(new FileSystemAccessRule(Administrators, FileSystemRights.FullControl, inherit, PropagationFlags.None, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(LocalSystem, FileSystemRights.FullControl, inherit, PropagationFlags.None, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(Users, FileSystemRights.ReadAndExecute, inherit, PropagationFlags.None, AccessControlType.Allow));
        return security;
    }
}

/// <summary>Where the hosts file and the policy backup live; shared by the helper and the backend.</summary>
public static class SiteBlockPaths
{
    public const string BackupFileName = "doh-backup.json";

    public static string HostsFile(string windowsDirectory) => Path.Combine(windowsDirectory, "System32", "drivers", "etc", "hosts");

    public static string BackupFolder(string programDataDirectory) => Path.Combine(programDataDirectory, "FocusLock");

    public static string BackupFile(string programDataDirectory) => Path.Combine(BackupFolder(programDataDirectory), BackupFileName);
}
