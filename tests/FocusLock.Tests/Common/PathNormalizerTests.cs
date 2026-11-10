using FocusLock.Core.Common;

namespace FocusLock.Tests.Common;

public class PathNormalizerTests
{
    [Theory]
    [InlineData(@"C:/Program Files (x86)/Steam/", @"C:\Program Files (x86)\Steam")]
    [InlineData(@"  D:\Games\Foo\  ", @"D:\Games\Foo")]
    [InlineData(@"c:\games\..\apps\x.exe", @"c:\apps\x.exe")]
    public void TryNormalize_converts_slashes_and_trims(string input, string expected)
    {
        Assert.Equal(expected, PathNormalizer.TryNormalize(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("C:\\bad\0path")]
    public void TryNormalize_returns_null_for_unusable_input(string? input)
    {
        Assert.Null(PathNormalizer.TryNormalize(input));
    }

    [Theory]
    [InlineData(@"C:\Games\Foo\bin\foo.exe", @"C:\Games\Foo", true)]
    [InlineData(@"c:\games\foo\FOO.EXE", @"C:\Games\Foo\", true)]
    [InlineData(@"C:\Games\Foo", @"C:\Games\Foo", true)]
    [InlineData(@"C:\Games\Foobar\x.exe", @"C:\Games\Foo", false)]
    [InlineData(@"D:\Games\Foo\x.exe", @"C:\Games\Foo", false)]
    [InlineData(@"C:\Games\Foo\x.exe", "", false)]
    public void IsUnder_respects_folder_boundaries(string path, string directory, bool expected)
    {
        Assert.Equal(expected, PathNormalizer.IsUnder(path, directory));
    }

    [Fact]
    public void PathEquals_ignores_case_and_trailing_separator()
    {
        Assert.True(PathNormalizer.PathEquals(@"C:\Apps\Game.exe", @"c:\apps\game.exe"));
        Assert.True(PathNormalizer.PathEquals(@"C:\Apps\", @"c:\apps"));
        Assert.False(PathNormalizer.PathEquals(@"C:\Apps\Game.exe", @"C:\Apps\Game2.exe"));
    }
}
