using Avalonia.Controls;
using Avalonia.Input;
using DashDetective.Shell.Help;
using Xunit;

namespace DashDetective.Tests.Shell.Help;

/// <summary>Covers <see cref="HelpOverlay.IsInsideCard"/> (which presses on the scrim close Help: a press on
/// the card, including an empty spot that reports the card itself, must leave it open) and
/// <see cref="HelpOverlay.RestoreMethod"/> (how focus is handed back on close). Constructing the controls
/// needs no render pass.</summary>
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

    [Fact]
    public void RestoreMethod_RingedButton_KeepsItsRing() =>
        Assert.Equal(NavigationMethod.Tab, HelpOverlay.RestoreMethod(new Button(), ring: true));

    [Fact]
    public void RestoreMethod_NoRing_StaysUnringed() =>
        Assert.Equal(NavigationMethod.Pointer, HelpOverlay.RestoreMethod(new Button(), ring: false));

    /// <summary>The bug this pins: a text box focused the keyboard way selects all its text, so pressing F1
    /// mid-typing and closing Help left the next key to wipe the half-typed filter.</summary>
    [Fact]
    public void RestoreMethod_RingedTextBox_IsNotSelectedAll() =>
        Assert.Equal(NavigationMethod.Pointer, HelpOverlay.RestoreMethod(new TextBox(), ring: true));
}
