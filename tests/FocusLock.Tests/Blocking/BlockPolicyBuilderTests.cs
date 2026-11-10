using FocusLock.Core.Blocking;
using FocusLock.Core.Detection;
using FocusLock.Core.Settings;
using FocusLock.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace FocusLock.Tests.Blocking;

public class BlockPolicyBuilderTests
{
    private readonly FakeFileSystem _fileSystem = new();

    [Fact]
    public void Includes_only_enabled_entries_and_collects_executable_names_for_folders()
    {
        _fileSystem.AddFile(@"C:\Games\Foo\foo.exe");
        _fileSystem.AddFile(@"C:\Games\Foo\readme.txt");
        _fileSystem.AddFile(@"C:\Games\Foo\bin\Helper.exe");
        _fileSystem.AddFile(@"C:\Games\Foo\a\b\c\deep.exe");
        _fileSystem.AddFile(@"C:\Games\Foo\a\b\c\d\too-deep.exe");

        var entries = new List<CatalogEntry>
        {
            new(new DetectedGame("steam:1", "Foo", GameSource.Steam, @"C:\Games\Foo", MatchKind.Folder), true),
            new(new DetectedGame("steam:2", "Bar", GameSource.Steam, @"C:\Games\Bar", MatchKind.Folder), false),
            new(new DetectedGame("launcher:steam.exe", "Steam", GameSource.Launcher, "steam.exe", MatchKind.ProcessName), true),
            new(new DetectedGame("custom:x", "x", GameSource.Custom, @"C:\Apps\x.exe", MatchKind.Executable), true),
        };

        var policy = Build(entries, new AppSettings());

        Assert.Equal(3, policy.Rules.Count);
        var foo = Assert.Single(policy.Rules, rule => rule.Name == "Foo");
        Assert.Equal(MatchKind.Folder, foo.Kind);
        Assert.NotNull(foo.ExecutableNames);
        Assert.Equal(["deep.exe", "foo.exe", "Helper.exe"], foo.ExecutableNames.OrderBy(name => name, StringComparer.OrdinalIgnoreCase));
        Assert.DoesNotContain(policy.Rules, rule => rule.Name == "Bar");
        Assert.Null(Assert.Single(policy.Rules, rule => rule.Name == "Steam").ExecutableNames);
        Assert.Null(Assert.Single(policy.Rules, rule => rule.Name == "x").ExecutableNames);
    }

    [Fact]
    public void Missing_folder_yields_empty_executable_names()
    {
        var entries = new List<CatalogEntry>
        {
            new(new DetectedGame("steam:1", "Gone", GameSource.Steam, @"C:\Games\Gone", MatchKind.Folder), true),
        };

        var policy = Build(entries, new AppSettings());

        Assert.Empty(Assert.Single(policy.Rules).ExecutableNames!);
    }

    [Fact]
    public void Allowlist_entries_become_allow_rules()
    {
        var settings = new AppSettings
        {
            Allowlist =
            [
                new CustomEntry { Path = @"C:\Tools\notepad.exe" },
                new CustomEntry { Path = @"C:\Games\Foo\Tools", IsFolder = true },
                new CustomEntry { Path = string.Empty },
            ],
        };

        var policy = Build([], settings);

        Assert.Empty(policy.Rules);
        Assert.Equal(2, policy.Allow.Count);
        Assert.Contains(policy.Allow, rule => rule.Kind == MatchKind.Executable && rule.Target == @"C:\Tools\notepad.exe");
        Assert.Contains(policy.Allow, rule => rule.Kind == MatchKind.Folder && rule.Target == @"C:\Games\Foo\Tools");
    }

    private BlockPolicy Build(IReadOnlyList<CatalogEntry> entries, AppSettings settings) =>
        new BlockPolicyBuilder(_fileSystem, NullLogger<BlockPolicyBuilder>.Instance).Build(entries, settings);
}
