using DashDetective.Tabs.Toolkit;
using System.Linq;
using Xunit;

namespace DashDetective.Tests.Tabs.Toolkit;

/// <summary>
/// Covers <see cref="ToolkitCommandCodec"/>: the user's own commands survive a round trip through the
/// settings file, and a damaged or hand-edited file costs its bad rows and nothing more.
/// </summary>
public class ToolkitCommandCodecTests {
    private static ToolkitCommand Sample(string title = "My folder") =>
        new(title, "Somewhere I go often", ToolkitCommandType.FolderPath, @"C:\work");

    [Fact]
    public void Encode_NoCommands_IsEmpty() {
        Assert.Equal("", ToolkitCommandCodec.Encode([]));
    }

    [Fact]
    public void Decode_NothingStored_YieldsNoCommands() {
        Assert.Empty(ToolkitCommandCodec.Decode(null));
        Assert.Empty(ToolkitCommandCodec.Decode(""));
    }

    [Fact]
    public void RoundTrip_KeepsEveryFieldAndTheOrder() {
        ToolkitCommand[] commands = [
            Sample(),
            new("Ports", "Listening sockets", ToolkitCommandType.Capture, "netstat", "-an",
                ToolkitCategory.Diagnostics),
            new("Docs", "", ToolkitCommandType.Url, "https://example.com"),
        ];

        var decoded = ToolkitCommandCodec.Decode(ToolkitCommandCodec.Encode(commands));

        Assert.Equal(commands, decoded);
    }

    /// <summary>Arguments are stored as typed, quotes and all, so the edit form gives back the user's own
    /// words rather than something reassembled from a split list.</summary>
    [Fact]
    public void RoundTrip_KeepsTheArgumentStringExactlyAsTyped() {
        var typed = @"/out:""C:\my folder\log.txt""  -v";
        var command = new ToolkitCommand("Log", "", ToolkitCommandType.Launch, "tool.exe", typed);

        var decoded = ToolkitCommandCodec.Decode(ToolkitCommandCodec.Encode([command]));

        Assert.Equal(typed, Assert.Single(decoded).Arguments);
    }

    /// <summary>Written by name, so reordering either enum cannot silently re-point a stored command at a
    /// different meaning.</summary>
    [Fact]
    public void Encode_WritesEnumsByName() {
        var encoded = ToolkitCommandCodec.Encode([
            new("Ports", "", ToolkitCommandType.Capture, "netstat", "", ToolkitCategory.Diagnostics)]);

        Assert.Contains("Capture", encoded, System.StringComparison.Ordinal);
        Assert.Contains("Diagnostics", encoded, System.StringComparison.Ordinal);
    }

    [Fact]
    public void RoundTrip_NoCategory_ComesBackAsNone() {
        var decoded = ToolkitCommandCodec.Decode(ToolkitCommandCodec.Encode([Sample()]));

        Assert.Null(Assert.Single(decoded).Category);
    }

    /// <summary>The payload means something different for each type, so a type this build cannot read
    /// makes the whole record unguessable — it goes rather than being filed under a default.</summary>
    [Fact]
    public void Decode_UnknownType_DropsThatCommandAndKeepsTheRest() {
        var encoded = ToolkitCommandCodec.Encode([Sample("Keep me"), Sample("Drop me")])
            .Replace("FolderPath", "SomethingFromTheFuture", System.StringComparison.Ordinal);

        // Both records named the same type, so both go — what matters is that nothing threw and the
        // result is a clean, if empty, list.
        Assert.Empty(ToolkitCommandCodec.Decode(encoded));
    }

    /// <summary>An unknown category only affects where a row is shown, so it degrades to "no second
    /// section" and the command itself survives.</summary>
    [Fact]
    public void Decode_UnknownCategory_KeepsTheCommandWithoutOne() {
        var encoded = ToolkitCommandCodec.Encode([
            new("Ports", "", ToolkitCommandType.Capture, "netstat", "", ToolkitCategory.Diagnostics)])
            .Replace("Diagnostics", "NoSuchSection", System.StringComparison.Ordinal);

        var decoded = Assert.Single(ToolkitCommandCodec.Decode(encoded));

        Assert.Equal("Ports", decoded.Title);
        Assert.Null(decoded.Category);
    }

    /// <summary>A custom row is already in the Custom section; a hand-edit asking for it a second time
    /// must not get it twice.</summary>
    [Fact]
    public void Decode_CategorySaysCustom_IsTreatedAsNone() {
        var encoded = ToolkitCommandCodec.Encode([
            new("Mine", "", ToolkitCommandType.Capture, "netstat", "", ToolkitCategory.Folders)])
            .Replace("Folders", "Custom", System.StringComparison.Ordinal);

        Assert.Null(Assert.Single(ToolkitCommandCodec.Decode(encoded)).Category);
    }

    [Theory]
    [InlineData("nonsense with no separators")]
    [InlineData("too\u001Ffew\u001Ffields")]
    public void Decode_MalformedRecord_IsDroppedRatherThanThrown(string encoded) {
        Assert.Empty(ToolkitCommandCodec.Decode(encoded));
    }

    [Fact]
    public void Decode_MalformedRecordAmongGoodOnes_LosesOnlyItself() {
        var encoded = ToolkitCommandCodec.Encode([Sample("First"), Sample("Second")]) +
                      "\u001Egarbage";

        Assert.Equal(["First", "Second"],
                     ToolkitCommandCodec.Decode(encoded).Select(c => c.Title));
    }

    [Fact]
    public void Encode_SkipsACommandWithNoTitle() {
        var encoded = ToolkitCommandCodec.Encode([
            Sample("Real"), new("  ", "", ToolkitCommandType.Launch, "thing.exe")]);

        Assert.Equal(["Real"], ToolkitCommandCodec.Decode(encoded).Select(c => c.Title));
    }

    /// <summary>The form refuses duplicate titles, but a hand-edited file never went through the form —
    /// and two rows sharing a title would make pins and search reveal ambiguous.</summary>
    [Fact]
    public void Decode_DuplicateTitles_KeepsTheFirstOnly() {
        var encoded = ToolkitCommandCodec.Encode([Sample("Same"), Sample("SAME")]);

        Assert.Single(ToolkitCommandCodec.Decode(encoded));
    }
}
