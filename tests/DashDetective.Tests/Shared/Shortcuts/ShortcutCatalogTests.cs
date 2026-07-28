using Avalonia.Input;
using DashDetective.Shared.Shortcuts;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace DashDetective.Tests.Shared.Shortcuts;

/// <summary>Covers <see cref="ShortcutCatalog"/>: the table stays complete and unambiguous (every
/// action bound exactly once, no gesture claimed twice) and <see cref="ShortcutCatalog.TryResolve"/>
/// honours the text-input guard, so a new binding can't silently shadow an existing one or start
/// stealing keys from a search box.</summary>
public class ShortcutCatalogTests {
    [Fact]
    public void All_CoversEveryShortcutIdExactlyOnce() {
        var ids = ShortcutCatalog.All.Select(s => s.Id).ToList();

        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.Equal(Enum.GetValues<ShortcutId>().OrderBy(id => id), ids.OrderBy(id => id));
    }

    [Fact]
    public void All_BindsNoGestureTwice() {
        var gestures = ShortcutCatalog.All
            .SelectMany(s => s.Gestures)
            .Select(g => (g.Key, g.KeyModifiers))
            .ToList();

        Assert.Equal(gestures.Count, gestures.Distinct().Count());
    }

    [Fact]
    public void All_BindsAtLeastOneGesturePerEntry() {
        foreach (var shortcut in ShortcutCatalog.All)
            Assert.NotEmpty(shortcut.Gestures);
    }

    [Fact]
    public void All_EntriesShownInHelpCarryTrimmedCopy() {
        foreach (var shortcut in ShortcutCatalog.All.Where(s => s.ShowInHelp)) {
            Assert.False(string.IsNullOrWhiteSpace(shortcut.Keys));
            Assert.False(string.IsNullOrWhiteSpace(shortcut.Description));
            Assert.Equal(shortcut.Keys.Trim(), shortcut.Keys);
            Assert.Equal(shortcut.Description.Trim(), shortcut.Description);
        }
    }

    [Theory]
    [InlineData(Key.D1, ShortcutId.NavigateTab1)]
    [InlineData(Key.NumPad1, ShortcutId.NavigateTab1)]
    [InlineData(Key.D8, ShortcutId.NavigateTab8)]
    [InlineData(Key.B, ShortcutId.ToggleNavCollapse)]
    [InlineData(Key.OemComma, ShortcutId.OpenSettings)]
    public void TryResolve_MapsControlGesturesToTheirAction(Key key, ShortcutId expected) {
        Assert.True(ShortcutCatalog.TryResolve(key, KeyModifiers.Control, textInputFocused: false, out var id));
        Assert.Equal(expected, id);
    }

    [Fact]
    public void TryResolve_DistinguishesShiftedGestures() {
        Assert.True(ShortcutCatalog.TryResolve(Key.Tab, KeyModifiers.Control, false, out var next));
        Assert.Equal(ShortcutId.NextTab, next);

        Assert.True(ShortcutCatalog.TryResolve(
            Key.Tab, KeyModifiers.Control | KeyModifiers.Shift, false, out var previous));
        Assert.Equal(ShortcutId.PreviousTab, previous);
    }

    [Fact]
    public void TryResolve_RequiresAnExactModifierMatch() {
        // A bare digit types into whatever has focus; only Ctrl+digit navigates.
        Assert.False(ShortcutCatalog.TryResolve(Key.D1, KeyModifiers.None, false, out _));
        Assert.False(ShortcutCatalog.TryResolve(Key.D1, KeyModifiers.Control | KeyModifiers.Alt, false, out _));
    }

    [Fact]
    public void TryResolve_ReturnsFalseForUnboundKeys() {
        Assert.False(ShortcutCatalog.TryResolve(Key.Q, KeyModifiers.Control, false, out _));
    }

    [Fact]
    public void TryResolve_StillFiresControlGesturesWhileTypingInATextBox() {
        Assert.True(ShortcutCatalog.TryResolve(Key.D1, KeyModifiers.Control, textInputFocused: true, out var id));
        Assert.Equal(ShortcutId.NavigateTab1, id);
    }

    [Fact]
    public void TryResolve_SuppressesTextUnsafeGesturesWhileTypingInATextBox() {
        foreach (var shortcut in ShortcutCatalog.All.Where(s => !s.AllowInTextInput))
            foreach (var gesture in shortcut.Gestures)
                Assert.False(ShortcutCatalog.TryResolve(
                    gesture.Key, gesture.KeyModifiers, textInputFocused: true, out _));
    }

    [Fact]
    public void TryResolve_ResolvesEveryBoundGesture() {
        var resolved = new List<ShortcutId>();

        foreach (var shortcut in ShortcutCatalog.All)
            foreach (var gesture in shortcut.Gestures) {
                Assert.True(ShortcutCatalog.TryResolve(gesture.Key, gesture.KeyModifiers, false, out var id));
                Assert.Equal(shortcut.Id, id);
                resolved.Add(id);
            }

        Assert.Equal(ShortcutCatalog.All.Count, resolved.Distinct().Count());
    }
}
