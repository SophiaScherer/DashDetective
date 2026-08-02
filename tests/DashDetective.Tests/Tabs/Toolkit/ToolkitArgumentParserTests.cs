using DashDetective.Tabs.Toolkit;
using Xunit;

namespace DashDetective.Tests.Tabs.Toolkit;

/// <summary>
/// Covers <see cref="ToolkitArgumentParser"/>: one typed string becomes the separate arguments the OS is
/// given. The last test is the one that matters — shell metacharacters are not special here, because what
/// comes out goes into an <c>ArgumentList</c> and never near a shell.
/// </summary>
public class ToolkitArgumentParserTests {
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Split_NothingTyped_YieldsNoArguments(string? typed) {
        Assert.Empty(ToolkitArgumentParser.Split(typed));
    }

    [Fact]
    public void Split_SeparatesOnWhitespace() {
        Assert.Equal(["-h", "20"], ToolkitArgumentParser.Split("-h 20"));
    }

    [Fact]
    public void Split_IgnoresRunsOfSpacesAndTabs() {
        Assert.Equal(["a", "b", "c"], ToolkitArgumentParser.Split("  a \t  b\tc  "));
    }

    /// <summary>The reason quotes are honoured at all: a path with a space is one argument, not two.</summary>
    [Fact]
    public void Split_QuotesGroupAPathWithSpaces() {
        Assert.Equal([@"C:\Program Files\thing", "/quiet"],
                     ToolkitArgumentParser.Split(@"""C:\Program Files\thing"" /quiet"));
    }

    [Fact]
    public void Split_QuotesMayOpenMidArgument() {
        Assert.Equal([@"/out:C:\my folder\log.txt"],
                     ToolkitArgumentParser.Split(@"/out:""C:\my folder\log.txt"""));
    }

    /// <summary>An explicitly empty argument is a thing programs are passed; a gap between arguments is
    /// not.</summary>
    [Fact]
    public void Split_KeepsAnExplicitlyEmptyArgument() {
        Assert.Equal(["a", "", "b"], ToolkitArgumentParser.Split(@"a """" b"));
    }

    /// <summary>Mid-typing is not an error state: the rest of the line becomes one argument rather than
    /// the whole string being refused.</summary>
    [Fact]
    public void Split_UnclosedQuote_TakesTheRestAsOneArgument() {
        Assert.Equal(["-p", "still typing this"],
                     ToolkitArgumentParser.Split(@"-p ""still typing this"));
    }

    /// <summary>Nothing here is a shell, so nothing here is a metacharacter: "&amp;" is just text on its
    /// way to becoming one element of an ArgumentList.</summary>
    [Theory]
    [InlineData("-an & calc", new[] { "-an", "&", "calc" })]
    [InlineData("a|b", new[] { "a|b" })]
    [InlineData("> out.txt", new[] { ">", "out.txt" })]
    [InlineData("$(whoami)", new[] { "$(whoami)" })]
    [InlineData("`whoami`", new[] { "`whoami`" })]
    public void Split_TreatsShellMetacharactersAsOrdinaryText(string typed, string[] expected) {
        Assert.Equal(expected, ToolkitArgumentParser.Split(typed));
    }
}
