using DashDetective.Shared.Controls;
using Xunit;

namespace DashDetective.Tests.Shared.Controls;

/// <summary>Covers <see cref="CollapsedWidgets"/>: the round-trip, and a total decode of anything
/// malformed — a hand-edited settings file costs its bad entries and nothing more.</summary>
public class CollapsedWidgetsTests {
    [Fact]
    public void RoundTrips() {
        string[] ids = ["settings.alerts", "settings.keyboard"];

        Assert.Equal(ids, CollapsedWidgets.Decode(CollapsedWidgets.Encode(ids)));
    }

    [Fact]
    public void Encode_KeepsTheOrderItWasGiven() {
        Assert.Equal(
            ["b", "a"],
            CollapsedWidgets.Decode(CollapsedWidgets.Encode(["b", "a"])));
    }

    [Fact]
    public void Encode_SkipsBlanksAndDuplicates() {
        var encoded = CollapsedWidgets.Encode(["a", "", "  ", "a", "b"]);

        Assert.Equal(["a", "b"], CollapsedWidgets.Decode(encoded));
    }

    [Fact]
    public void Encode_OfNothing_IsEmpty() {
        Assert.Equal("", CollapsedWidgets.Encode([]));
    }

    [Fact]
    public void Decode_OfNullOrEmpty_IsEmpty() {
        Assert.Empty(CollapsedWidgets.Decode(null));
        Assert.Empty(CollapsedWidgets.Decode(""));
    }

    /// <summary>The separator is a control character, so a hand-edit can only ever leave empty fields
    /// behind — those are dropped rather than folding a widget with no name.</summary>
    [Fact]
    public void Decode_DropsEmptyFields() {
        Assert.Equal(["a", "b"], CollapsedWidgets.Decode($"a{(char)0x1F}{(char)0x1F}b"));
    }
}
