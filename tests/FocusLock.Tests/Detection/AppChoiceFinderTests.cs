using FocusLock.Core.Abstractions;
using FocusLock.Core.Detection;
using FocusLock.Tests.Fakes;

namespace FocusLock.Tests.Detection;

public class AppChoiceFinderTests
{
    private const string Uninstall64 = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

    private readonly FakeRegistry _registry = new();
    private readonly FakeProcessProvider _processes = new();
    private readonly FakeAppEnvironment _environment = new();

    [Fact]
    public void Lists_open_apps_first_then_installed_programs_by_name()
    {
        AddProgram("Zoo", "Zoo Tycoon", @"D:\Games\Zoo Tycoon");
        AddProgram("Chrome", "Google Chrome", @"C:\Program Files\Google\Chrome\Application");
        _processes.Add(1, "chrome.exe", @"C:\Program Files\Google\Chrome\Application\chrome.exe");
        _processes.Add(2, "Discord.exe", @"C:\Users\test\AppData\Roaming\Discord\Discord.exe");

        var choices = Find(1, 2);

        Assert.Equal(["Discord", "Google Chrome", "Google Chrome", "Zoo Tycoon"], choices.Select(c => c.Name));
        Assert.True(choices[1].IsRunning);
        Assert.False(choices[1].IsFolder);
        Assert.Equal(@"C:\Program Files\Google\Chrome\Application\chrome.exe", choices[1].Path);
        Assert.True(choices[3].IsFolder);
        Assert.Equal(@"D:\Games\Zoo Tycoon", choices[3].Path);
    }

    [Fact]
    public void Offers_the_install_folder_for_an_open_app_in_a_versioned_folder()
    {
        // Discord's exe lives in app-1.0.9; after an auto-update that exact path no longer exists.
        AddProgram("Discord", "Discord", @"C:\Users\test\AppData\Local\Discord");
        _processes.Add(1, "Discord.exe", @"C:\Users\test\AppData\Local\Discord\app-1.0.9\Discord.exe");
        _processes.Add(2, "Discord.exe", @"C:\Users\test\AppData\Local\Discord\app-1.0.9\Discord.exe");

        var choice = Assert.Single(Find(1, 2));

        Assert.Equal(new AppChoice("Discord", @"C:\Users\test\AppData\Local\Discord", IsFolder: true, IsRunning: true), choice);
    }

    [Fact]
    public void Names_an_open_app_after_the_closest_installed_program()
    {
        AddProgram("Steam", "Steam", @"C:\Steam");
        AddProgram("Steam App 1", "Portal", @"C:\Steam\steamapps\common\Portal");
        _processes.Add(1, "portal.exe", @"C:\Steam\steamapps\common\Portal\portal.exe");

        var open = Find(1)[0];

        Assert.Equal("Portal", open.Name);
        Assert.Equal(@"C:\Steam\steamapps\common\Portal\portal.exe", open.Path);
    }

    [Fact]
    public void Skips_background_processes_FocusLock_Windows_and_duplicates()
    {
        _processes.Add(1, "service.exe", @"C:\Program Files\Vendor\service.exe");
        _processes.Add(_environment.ProcessId, "FocusLock.exe", _environment.ExecutablePath);
        _processes.Add(3, "notepad.exe", @"C:\Windows\System32\notepad.exe");
        _processes.Add(4, "game.exe", @"D:\Game\game.exe");
        _processes.Add(5, "game.exe", @"D:\Game\game.exe");
        _processes.Add(6, "gone.exe");

        var choice = Assert.Single(Find(_environment.ProcessId, 3, 4, 5, 6));

        Assert.Equal(@"D:\Game\game.exe", choice.Path);
    }

    [Fact]
    public void Never_offers_a_folder_that_would_close_everything_under_it()
    {
        AddProgram("Root", "Bad Root", @"C:\");
        AddProgram("PF", "Bad Program Files", @"C:\Program Files");
        AddProgram("PF86", "Bad Program Files x86", @"C:\Program Files (x86)\");
        AddProgram("Users", "Bad Users", @"C:\Users");
        AddProgram("Win", "Bad Windows", @"C:\Windows\System32");
        AddProgram("Self", "Bad FocusLock", @"C:\Apps");
        AddProgram("Empty", "No Folder", "");
        AddProgram("Relative", "Bad Relative", @"Games\Thing");
        AddProgram("Ok", "Fine Game", @"C:\Games");

        var choice = Assert.Single(Find());

        Assert.Equal("Fine Game", choice.Name);
    }

    [Fact]
    public void Reads_install_folders_stored_in_quotes()
    {
        AddProgram("Scanner", "Angry IP Scanner", "\"C:\\Program Files\\Angry IP Scanner\"");

        Assert.Equal(@"C:\Program Files\Angry IP Scanner", Assert.Single(Find()).Path);
    }

    private IReadOnlyList<AppChoice> Find(params int[] windowed) =>
        new AppChoiceFinder(_processes, _registry, _environment).Find(windowed.ToHashSet());

    private void AddProgram(string keyName, string name, string location) =>
        _registry
            .Set(RegistryRoot.LocalMachine, $@"{Uninstall64}\{keyName}", "DisplayName", name)
            .Set(RegistryRoot.LocalMachine, $@"{Uninstall64}\{keyName}", "InstallLocation", location);
}
