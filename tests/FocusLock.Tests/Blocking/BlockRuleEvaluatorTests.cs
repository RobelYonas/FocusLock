using FocusLock.Core.Abstractions;
using FocusLock.Core.Blocking;
using FocusLock.Core.Detection;
using FocusLock.Tests.Fakes;

namespace FocusLock.Tests.Blocking;

public class BlockRuleEvaluatorTests
{
    private static readonly BlockRule FooFolder = new("Foo", MatchKind.Folder, @"C:\Games\Foo", ["foo.exe", "FooLauncher.exe"]);
    private static readonly BlockRule GameExe = new("Game", MatchKind.Executable, @"D:\Apps\game.exe");
    private static readonly BlockRule SteamName = new("Steam", MatchKind.ProcessName, "steam.exe");

    private static readonly BlockPolicy Policy = new(
        [FooFolder, GameExe, SteamName],
        [new BlockRule("Foo tools", MatchKind.Folder, @"C:\Games\Foo\Tools")]);

    private readonly BlockRuleEvaluator _evaluator = new(new FakeAppEnvironment { ProcessId = 4242, WindowsDirectory = @"C:\Windows" });

    [Theory]
    [InlineData(@"C:\Games\Foo\bin\x64\foo.exe")]
    [InlineData(@"c:\games\foo\FOO.EXE")]
    public void Blocks_executables_inside_a_folder_rule(string path)
    {
        var decision = _evaluator.Evaluate(Policy, new ProcessInfo(1, "foo.exe", path));

        Assert.True(decision.ShouldBlock);
        Assert.Equal("Foo", decision.MatchedRule?.Name);
    }

    [Fact]
    public void Does_not_block_a_sibling_folder_with_the_same_prefix()
    {
        var decision = _evaluator.Evaluate(Policy, new ProcessInfo(1, "x.exe", @"C:\Games\Foobar\x.exe"));

        Assert.False(decision.ShouldBlock);
    }

    [Fact]
    public void Blocks_an_exact_executable_regardless_of_case()
    {
        Assert.True(_evaluator.Evaluate(Policy, new ProcessInfo(1, "GAME.EXE", @"d:\apps\GAME.EXE")).ShouldBlock);
        Assert.False(_evaluator.Evaluate(Policy, new ProcessInfo(1, "game2.exe", @"D:\Apps\game2.exe")).ShouldBlock);
    }

    [Fact]
    public void Blocks_by_process_name_wherever_it_runs_from()
    {
        Assert.True(_evaluator.Evaluate(Policy, new ProcessInfo(1, "Steam.exe", @"E:\Somewhere\Steam.exe")).ShouldBlock);
        Assert.True(_evaluator.Evaluate(Policy, new ProcessInfo(1, "steam.exe", null)).ShouldBlock);
    }

    [Fact]
    public void Allowlist_always_wins()
    {
        var decision = _evaluator.Evaluate(Policy, new ProcessInfo(1, "editor.exe", @"C:\Games\Foo\Tools\editor.exe"));

        Assert.False(decision.ShouldBlock);
        Assert.Null(decision.MatchedRule);
    }

    [Fact]
    public void Never_blocks_focuslock_itself()
    {
        var decision = _evaluator.Evaluate(Policy, new ProcessInfo(4242, "foo.exe", @"C:\Games\Foo\foo.exe"));

        Assert.False(decision.ShouldBlock);
    }

    [Fact]
    public void Never_blocks_anything_in_the_windows_folder()
    {
        var decision = _evaluator.Evaluate(Policy, new ProcessInfo(1, "steam.exe", @"C:\Windows\System32\steam.exe"));

        Assert.False(decision.ShouldBlock);
    }

    [Fact]
    public void Falls_back_to_known_executable_names_when_the_path_is_unreadable()
    {
        Assert.True(_evaluator.Evaluate(Policy, new ProcessInfo(1, "foolauncher.exe", null)).ShouldBlock);
        Assert.True(_evaluator.Evaluate(Policy, new ProcessInfo(2, "game.exe", null)).ShouldBlock);
        Assert.False(_evaluator.Evaluate(Policy, new ProcessInfo(3, "other.exe", null)).ShouldBlock);
    }

    [Fact]
    public void Folder_rule_without_known_names_does_not_match_unreadable_paths()
    {
        var policy = new BlockPolicy([new BlockRule("Bar", MatchKind.Folder, @"C:\Games\Bar")], []);

        Assert.False(_evaluator.Evaluate(policy, new ProcessInfo(1, "bar.exe", null)).ShouldBlock);
    }
}
