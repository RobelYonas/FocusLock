using System.Text.RegularExpressions;
using FocusLock.Core.Abstractions;
using FocusLock.Core.Common;

namespace FocusLock.Core.Detection;

/// <summary>An app the person can pick for the blocklist: an open program (its .exe) or an installed one (its folder).</summary>
public sealed record AppChoice(string Name, string Path, bool IsFolder, bool IsRunning)
{
    public string Detail => IsRunning ? $"Open now · {Path}" : IsFolder ? $"Installed · {Path}" : Path;
}

/// <summary>Lists apps for the "Add app" picker, so nobody has to hunt for .exe files.</summary>
public sealed class AppChoiceFinder(IProcessProvider processes, IRegistry registry, IAppEnvironment environment)
{
    // A folder rule closes everything under it, so these shared install roots must never become one.
    private static readonly string[] SharedRoots = ["Program Files", "Program Files (x86)", "ProgramData", "Users", "Windows"];

    private static readonly Regex VersionedFolder = new(@"\d+\.\d+", RegexOptions.CultureInvariant);

    /// <summary>Open apps with a window first, then installed programs, each sorted by name.</summary>
    public IReadOnlyList<AppChoice> Find(IReadOnlySet<int> windowedProcessIds)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var installed = new List<AppChoice>();
        foreach (var (root, uninstallKey) in PublisherGameDetector.UninstallKeys)
        {
            foreach (var keyName in registry.GetSubKeyNames(root, uninstallKey))
            {
                var key = $@"{uninstallKey}\{keyName}";
                var name = registry.GetString(root, key, "DisplayName")?.Trim();
                // Some installers store the folder in quotes; a relative path would resolve against FocusLock's own working folder.
                var location = registry.GetString(root, key, "InstallLocation")?.Trim().Trim('"');
                var folder = location is not null && Path.IsPathFullyQualified(location) ? PathNormalizer.TryNormalize(location) : null;
                if (!string.IsNullOrEmpty(name) && folder is not null && IsSafeFolder(folder) && seen.Add(folder))
                {
                    installed.Add(new AppChoice(name, folder, IsFolder: true, IsRunning: false));
                }
            }
        }

        var running = new List<AppChoice>();
        var openFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var process in processes.ListProcesses())
        {
            if (!windowedProcessIds.Contains(process.Id) || process.Id == environment.ProcessId)
            {
                continue;
            }

            var path = PathNormalizer.TryNormalize(processes.TryGetExecutablePath(process.Id));
            if (path is null || IsFocusLockOrWindows(path))
            {
                continue;
            }

            // "Google Chrome" reads better than "chrome", so borrow the closest installed program's name.
            var owner = installed
                .Where(app => PathNormalizer.IsUnder(path, app.Path))
                .MaxBy(app => app.Path.Length);

            // An exe in a versioned folder (Discord\app-1.0.9\Discord.exe) moves on every auto-update, so block the install folder instead.
            // shortcut: Store apps (WindowsApps\Name_1.2.3_x64) have no install folder to fall back on; they keep the exact exe path.
            var choice = owner is not null && VersionedFolder.IsMatch(Path.GetRelativePath(owner.Path, Path.GetDirectoryName(path)!))
                ? owner with { IsRunning = true }
                : new AppChoice(owner?.Name ?? Path.GetFileNameWithoutExtension(path), path, IsFolder: false, IsRunning: true);
            // The folder moves from "installed" to "open"; a second process from the same folder finds it already moved.
            if (choice.IsFolder ? openFolders.Add(choice.Path) : seen.Add(path))
            {
                running.Add(choice);
            }
        }

        return [.. Sorted(running), .. Sorted(installed.Where(app => !openFolders.Contains(app.Path)))];
    }

    private static IEnumerable<AppChoice> Sorted(IEnumerable<AppChoice> apps) =>
        apps.OrderBy(app => app.Name, StringComparer.CurrentCultureIgnoreCase);

    private bool IsSafeFolder(string folder)
    {
        var parent = Path.GetDirectoryName(folder);
        var isTooBroad = parent is null
            || (Path.GetDirectoryName(parent) is null && SharedRoots.Contains(Path.GetFileName(folder), StringComparer.OrdinalIgnoreCase));
        return !isTooBroad && !IsFocusLockOrWindows(folder) && !PathNormalizer.IsUnder(environment.ExecutablePath, folder);
    }

    private bool IsFocusLockOrWindows(string path) =>
        PathNormalizer.PathEquals(path, environment.ExecutablePath) || PathNormalizer.IsUnder(path, environment.WindowsDirectory);
}
