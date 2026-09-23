using Avalonia.Controls;
using DashDetective.Shell.Help;
using Xunit;

namespace DashDetective.Tests.Shell.Help;

/// <summary>Covers <see cref="HelpOverlay.IsInsideCard"/>: which presses on the scrim close Help. A press
/// on the card, including an empty spot that reports the card itself, must leave it open. Constructing the
/// controls needs no render pass.</summary>
public class HelpOverlayTests {
    [Fact]
    public void IsInsideCard_TheCardItself_IsInside() {
        var (_, card, _) = Build();
        Assert.True(HelpOverlay.IsInsideCard(card, card));
    }

    [Fact]
    public void IsInsideCard_ContentInTheCard_IsInside() {
        var (_, card, content) = Build();
        Assert.True(HelpOverlay.IsInsideCard(card, content));
    }

    [Fact]
    public void IsInsideCard_TheScrim_IsOutside() {
        var (scrim, card, _) = Build();
        Assert.False(HelpOverlay.IsInsideCard(card, scrim));
    }

    [Fact]
    public void IsInsideCard_UnrelatedControl_IsOutside() {
        var (_, card, _) = Build();
        Assert.False(HelpOverlay.IsInsideCard(card, new Button()));
    }

    [Fact]
    public void IsInsideCard_NoVisualSource_IsOutside() {
        var (_, card, _) = Build();
        Assert.False(HelpOverlay.IsInsideCard(card, null));
        Assert.False(HelpOverlay.IsInsideCard(card, new object()));
    }

    /// <summary>The overlay's shape: a scrim holding the card, with content nested a level down.</summary>
    private static (Border Scrim, Border Card, TextBlock Content) Build() {
        var content = new TextBlock();
        var card = new Border { Child = new StackPanel { Children = { content } } };
        var scrim = new Border { Child = card };
        return (scrim, card, content);
    }
}
