using FocusLock.Core.Detection.Steam;

namespace FocusLock.Tests.Detection;

public class KeyValuesParserTests
{
    [Fact]
    public void Parses_nested_blocks_and_unescapes_backslashes()
    {
        var root = KeyValuesParser.Parse("""
            "libraryfolders"
            {
                "0"
                {
                    "path"		"C:\\Program Files (x86)\\Steam"
                    "apps"
                    {
                        "620"		"12755935432"
                    }
                }
            }
            """);

        var first = root.Child("libraryfolders")?.Child("0");
        Assert.NotNull(first);
        Assert.Equal(@"C:\Program Files (x86)\Steam", first.ValueOf("path"));
        Assert.Equal("12755935432", first.Child("apps")?.ValueOf("620"));
    }

    [Fact]
    public void Unescapes_quotes_and_tabs()
    {
        var root = KeyValuesParser.Parse("\"key\" \"a \\\"quoted\\\" value\\twith tab\"");

        Assert.Equal("a \"quoted\" value\twith tab", root.ValueOf("key"));
    }

    [Fact]
    public void Ignores_comments_and_accepts_bare_tokens()
    {
        var root = KeyValuesParser.Parse("// a comment\nkey value // trailing\n\"other\" \"x\"");

        Assert.Equal("value", root.ValueOf("key"));
        Assert.Equal("x", root.ValueOf("other"));
    }

    [Fact]
    public void Tolerates_truncated_input()
    {
        var root = KeyValuesParser.Parse("\"AppState\" { \"appid\" \"620\" \"name\"");

        Assert.Equal("620", root.Child("AppState")?.ValueOf("appid"));
    }

    [Fact]
    public void Lookups_are_case_insensitive()
    {
        var root = KeyValuesParser.Parse("\"AppState\" { \"InstallDir\" \"Portal 2\" }");

        Assert.Equal("Portal 2", root.Child("appstate")?.ValueOf("installdir"));
    }

    [Fact]
    public void Empty_input_yields_empty_root()
    {
        var root = KeyValuesParser.Parse(string.Empty);

        Assert.Empty(root.Children);
    }
}
