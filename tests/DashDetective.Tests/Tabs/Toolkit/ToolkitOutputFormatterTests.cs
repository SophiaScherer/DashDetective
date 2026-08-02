using DashDetective.Tabs.Toolkit;
using System;
using System.Linq;
using Xunit;

namespace DashDetective.Tests.Tabs.Toolkit;

/// <summary>Covers <see cref="ToolkitOutputFormatter"/>: the two streams merge in a readable order,
/// Windows line endings and console sign-off padding are normalised away, both caps announce
/// themselves rather than trimming silently, and every outcome with no output to show still says
/// something.</summary>
public class ToolkitOutputFormatterTests {
    [Fact]
    public void Combine_NoOutputOnEitherStream_IsEmpty() {
        Assert.Equal("", ToolkitOutputFormatter.Combine(null, null));
        Assert.Equal("", ToolkitOutputFormatter.Combine("", ""));
        Assert.Equal("", ToolkitOutputFormatter.Combine("  \r\n \t ", null));
    }

    [Fact]
    public void Combine_OnlyOneStream_DoesNotPadWithABlankLine() {
        Assert.Equal("output", ToolkitOutputFormatter.Combine("output", ""));
        Assert.Equal("trouble", ToolkitOutputFormatter.Combine("", "trouble"));
    }

    /// <summary>A command writing to both is almost always reporting its trouble after its output.</summary>
    [Fact]
    public void Combine_BothStreams_PutsErrorAfterOutput() {
        Assert.Equal("output\ntrouble", ToolkitOutputFormatter.Combine("output", "trouble"));
    }

    [Fact]
    public void Combine_NormalisesWindowsAndBareCarriageReturns() {
        Assert.Equal("a\nb\nc", ToolkitOutputFormatter.Combine("a\r\nb\rc", null));
    }

    /// <summary>Nearly every in-box console tool signs off with blank lines, which would otherwise pad
    /// every stanza in the log panel.</summary>
    [Fact]
    public void Combine_DropsLeadingAndTrailingBlankLines() {
        Assert.Equal("body", ToolkitOutputFormatter.Combine("\r\n\r\nbody\r\n\r\n", null));
    }

    [Fact]
    public void Combine_KeepsBlankLinesInsideTheBody() {
        Assert.Equal("first\n\nsecond", ToolkitOutputFormatter.Combine("first\r\n\r\nsecond", null));
    }

    [Fact]
    public void Cap_ShortText_IsLeftExactlyAsItIs() {
        Assert.Equal("a\nb", ToolkitOutputFormatter.Cap("a\nb"));
        Assert.Equal("", ToolkitOutputFormatter.Cap(""));
    }

    [Fact]
    public void Cap_TooManyLines_KeepsTheCapAndSaysItTrimmed() {
        var text = string.Join('\n', Enumerable.Range(0, ToolkitOutputFormatter.MaxLines + 50));

        var capped = ToolkitOutputFormatter.Cap(text);
        var lines = capped.Split('\n');

        Assert.Equal(ToolkitOutputFormatter.MaxLines + 1, lines.Length); // + the marker
        Assert.Equal(ToolkitOutputFormatter.TruncationMarker, lines[^1]);
        Assert.Equal("0", lines[0]);
    }

    [Fact]
    public void Cap_TooManyCharactersOnOneLine_AlsoTrimsAndSaysSo() {
        var text = new string('x', ToolkitOutputFormatter.MaxCharacters + 500);

        var capped = ToolkitOutputFormatter.Cap(text);

        Assert.EndsWith(ToolkitOutputFormatter.TruncationMarker, capped, StringComparison.Ordinal);
        Assert.True(capped.Length < text.Length);
    }

    [Fact]
    public void Cap_TextExactlyAtTheLineCap_IsNotMarkedTruncated() {
        var text = string.Join('\n', Enumerable.Range(0, ToolkitOutputFormatter.MaxLines));

        Assert.Equal(text, ToolkitOutputFormatter.Cap(text));
    }

    [Fact]
    public void Combine_AppliesTheCapsToMergedStreamOutput() {
        var flood = string.Join("\r\n", Enumerable.Range(0, ToolkitOutputFormatter.MaxLines + 10));

        var combined = ToolkitOutputFormatter.Combine(flood, null);

        Assert.EndsWith(ToolkitOutputFormatter.TruncationMarker, combined, StringComparison.Ordinal);
    }

    [Fact]
    public void BlockedUrl_NamesTheTargetSoABadEntryIsObvious() {
        var message = ToolkitOutputFormatter.BlockedUrl("http://example.com");

        Assert.Contains("http://example.com", message, StringComparison.Ordinal);
        Assert.Contains("https://", message, StringComparison.Ordinal);
    }

    [Fact]
    public void TimedOut_NamesTheLimitAndKeepsWhatWasPrinted() {
        var message = ToolkitOutputFormatter.TimedOut(TimeSpan.FromSeconds(20), "partial");

        Assert.StartsWith("partial\n", message, StringComparison.Ordinal);
        Assert.Contains("20s", message, StringComparison.Ordinal);
    }

    [Fact]
    public void TimedOut_NoOutputAtAll_IsJustTheNote() {
        Assert.DoesNotContain('\n', ToolkitOutputFormatter.TimedOut(TimeSpan.FromSeconds(5), ""));
    }

    [Fact]
    public void ExitedWith_NamesTheCodeAndKeepsWhatWasPrinted() {
        var message = ToolkitOutputFormatter.ExitedWith(2, "some output");

        Assert.StartsWith("some output\n", message, StringComparison.Ordinal);
        Assert.Contains("code 2", message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExitedWith_NoOutputAtAll_IsJustTheNote() {
        Assert.Equal("Exited with code 5.", ToolkitOutputFormatter.ExitedWith(5, ""));
    }

    [Fact]
    public void Failed_CarriesTheReasonTheOsGave() {
        Assert.Contains(
            "The system cannot find the file specified",
            ToolkitOutputFormatter.Failed("The system cannot find the file specified"),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Failed_NoReasonGiven_StillSaysSomething() {
        Assert.False(string.IsNullOrWhiteSpace(ToolkitOutputFormatter.Failed("   ")));
    }
}
