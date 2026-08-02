using DashDetective.Tabs.Toolkit;
using Xunit;

namespace DashDetective.Tests.Tabs.Toolkit;

/// <summary>Covers <see cref="ToolkitPins"/>: pins survive a round trip intact — including the commands
/// carrying spaces, slashes, percent signs and angle brackets that the catalog actually contains — and a
/// hand-edited or older settings value costs its bad entries and nothing more.</summary>
public class ToolkitPinsTests {
    [Fact]
    public void RoundTrip_KeepsTheCommandsAndTheirOrder() {
        string[] commands = ["%appdata%", "ipconfig /all", "ping <host>", @"%windir%\System32\drivers\etc"];

        Assert.Equal(commands, ToolkitPins.Decode(ToolkitPins.Encode(commands)));
    }

    [Fact]
    public void Encode_NothingPinned_IsEmpty() {
        Assert.Equal("", ToolkitPins.Encode([]));
    }

    [Fact]
    public void Decode_NothingStored_IsEmpty() {
        Assert.Empty(ToolkitPins.Decode(null));
        Assert.Empty(ToolkitPins.Decode(""));
    }

    [Fact]
    public void Encode_SkipsBlankEntriesRatherThanStoringSeparatorsForThem() {
        Assert.Equal(["a", "b"], ToolkitPins.Decode(ToolkitPins.Encode(["a", "", "   ", "b"])));
    }

    [Fact]
    public void Decode_DropsDuplicates() {
        Assert.Equal(["a", "b"], ToolkitPins.Decode(ToolkitPins.Encode(["a", "b", "a"])));
    }

    /// <summary>The separator is an ASCII control character precisely so no command can contain it;
    /// a value spelled with ordinary punctuation must survive whole.</summary>
    [Fact]
    public void RoundTrip_CommandWithPunctuation_IsNotSplit() {
        Assert.Equal(["tracert <host> -h 20 & more"],
                     ToolkitPins.Decode(ToolkitPins.Encode(["tracert <host> -h 20 & more"])));
    }

    [Fact]
    public void Decode_GarbageValue_IsTreatedAsASinglePin() {
        // A hand-edited file with no separators is one entry, not an error.
        Assert.Equal(["whatever"], ToolkitPins.Decode("whatever"));
    }
}
