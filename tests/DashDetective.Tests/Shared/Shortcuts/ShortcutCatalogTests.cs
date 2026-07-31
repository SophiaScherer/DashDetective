using Avalonia.Input;
using DashDetective.Shared.Shortcuts;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace DashDetective.Tests.Shared.Shortcuts;

/// <summary>Covers <see cref="ShortcutCatalog"/>: the table stays complete and unambiguous (every
/// action bound, and neither a gesture nor an action claimed twice within one scope) and
/// <see cref="ShortcutCatalog.TryResolve"/> picks the active tab's binding and honours the text-input
/// guard, so a new binding can't silently shadow an existing one or start stealing keys from a search
/// box.</summary>
public class ShortcutCatalogTests {
    /// <summary>Resolves as if no tab claims its own bindings — the common case for global shortcuts.</summary>
    private static bool Resolve(Key key, KeyModifiers modifiers, bool textInputFocused, out ShortcutId id) =>
        ShortcutCatalog.TryResolve(key, modifiers, textInputFocused, ShortcutScope.Global, out id);

    [Fact]
    public void All_CoversEveryShortcutId() {
        var ids = ShortcutCatalog.All.Select(s => s.Id).Distinct().OrderBy(id => id);

        Assert.Equal(Enum.GetValues<ShortcutId>().OrderBy(id => id), ids);
    }

    [Fact]
    public void All_BindsNoActionTwiceWithinAScope() {
        // Across scopes an action may repeat — "/" focuses the filter box on both Processes and
        // Toolkit, and the shell offers a resolved id to whichever page is current. Within one scope
        // a second binding for the same action would be unreachable.
        foreach (var scope in Enum.GetValues<ShortcutScope>()) {
            var ids = ShortcutCatalog.All.Where(s => s.Scope == scope).Select(s => s.Id).ToList();

            Assert.Equal(ids.Count, ids.Distinct().Count());
        }
    }

    [Fact]
    public void All_BindsNoGestureTwiceWithinAScope() {
        // Across scopes a gesture may repeat (Alt+up sorts on Processes, climbs a folder on File
        // Explorer) because only one tab is ever current. Within one scope it must be unambiguous.
        foreach (var scope in Enum.GetValues<ShortcutScope>()) {
            var gestures = ShortcutCatalog.All
                .Where(s => s.Scope == scope)
                .SelectMany(s => s.Gestures)
                .Select(g => (g.Key, g.KeyModifiers))
                .ToList();

            Assert.Equal(gestures.Count, gestures.Distinct().Count());
        }
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

    [Fact]
    public void HelpGroups_ListEveryShortcutMarkedForHelpExactlyOnce() {
        var listed = ShortcutCatalog.HelpGroups.SelectMany(g => g.Shortcuts).ToList();
        var expected = ShortcutCatalog.All.Where(s => s.ShowInHelp).ToList();

        Assert.Equal(expected.Count, listed.Count);
        Assert.Equal(expected.Select(s => s.Id).OrderBy(id => id), listed.Select(s => s.Id).OrderBy(id => id));
    }

    [Fact]
    public void HelpGroups_CarryATitleAndAtLeastOneRow() {
        Assert.NotEmpty(ShortcutCatalog.HelpGroups);

        foreach (var group in ShortcutCatalog.HelpGroups) {
            Assert.False(string.IsNullOrWhiteSpace(group.Title));
            Assert.NotEmpty(group.Shortcuts);
        }
    }

    [Fact]
    public void HelpGroups_LeadWithTheGeneralShortcuts() {
        Assert.Equal("General", ShortcutCatalog.HelpGroups[0].Title);
    }

    [Fact]
    public void HelpGroups_KeepEachScopeToOneGroup() {
        var titles = ShortcutCatalog.HelpGroups.Select(g => g.Title).ToList();

        Assert.Equal(titles.Count, titles.Distinct().Count());
    }

    [Theory]
    [InlineData(Key.D1, ShortcutId.NavigateTab1)]
    [InlineData(Key.NumPad1, ShortcutId.NavigateTab1)]
    [InlineData(Key.D8, ShortcutId.NavigateTab8)]
    [InlineData(Key.D9, ShortcutId.NavigateTab9)]
    [InlineData(Key.B, ShortcutId.ToggleNavCollapse)]
    [InlineData(Key.OemComma, ShortcutId.OpenSettings)]
    public void TryResolve_MapsControlGesturesToTheirAction(Key key, ShortcutId expected) {
        Assert.True(Resolve(key, KeyModifiers.Control, textInputFocused: false, out var id));
        Assert.Equal(expected, id);
    }

    [Fact]
    public void TryResolve_MapsEveryAlternativeGestureToTheSameAction() {
        // Refresh and Help each carry a function key and a Ctrl chord; both must land on one action.
        Assert.True(Resolve(Key.F5, KeyModifiers.None, false, out var functionRefresh));
        Assert.True(Resolve(Key.R, KeyModifiers.Control, false, out var chordRefresh));
        Assert.Equal(ShortcutId.Refresh, functionRefresh);
        Assert.Equal(ShortcutId.Refresh, chordRefresh);

        Assert.True(Resolve(Key.F1, KeyModifiers.None, false, out var functionHelp));
        Assert.True(Resolve(Key.OemQuestion, KeyModifiers.Control, false, out var chordHelp));
        Assert.Equal(ShortcutId.ShowHelp, functionHelp);
        Assert.Equal(ShortcutId.ShowHelp, chordHelp);
    }

    [Fact]
    public void TryResolve_PrefersTheActiveTabsBindingOverAGlobalOne() {
        // Alt+up is bound in two scopes; each tab must get its own action.
        Assert.True(ShortcutCatalog.TryResolve(
            Key.Up, KeyModifiers.Alt, false, ShortcutScope.Processes, out var onProcesses));
        Assert.Equal(ShortcutId.SortAscending, onProcesses);

        Assert.True(ShortcutCatalog.TryResolve(
            Key.Up, KeyModifiers.Alt, false, ShortcutScope.FileExplorer, out var onFiles));
        Assert.Equal(ShortcutId.NavigateUp, onFiles);
    }

    [Fact]
    public void TryResolve_ReservesControlFForUniversalSearchOnEveryTab() {
        // Ctrl+F belongs to the toolbar search box alone — no tab may reclaim it for its own field.
        foreach (var scope in Enum.GetValues<ShortcutScope>()) {
            Assert.True(ShortcutCatalog.TryResolve(
                Key.F, KeyModifiers.Control, textInputFocused: false, scope, out var id));
            Assert.Equal(ShortcutId.FocusSearch, id);
        }
    }

    [Fact]
    public void TryResolve_MapsSlashToWhicheverFieldTheTabOwns() {
        Assert.True(ShortcutCatalog.TryResolve(
            Key.OemQuestion, KeyModifiers.None, false, ShortcutScope.Processes, out var onProcesses));
        Assert.Equal(ShortcutId.FocusFilter, onProcesses);

        Assert.True(ShortcutCatalog.TryResolve(
            Key.OemQuestion, KeyModifiers.None, false, ShortcutScope.FileExplorer, out var onFiles));
        Assert.Equal(ShortcutId.FocusAddressBar, onFiles);

        // A tab with no field of its own leaves "/" alone.
        Assert.False(ShortcutCatalog.TryResolve(
            Key.OemQuestion, KeyModifiers.None, false, ShortcutScope.Network, out _));
        Assert.False(Resolve(Key.OemQuestion, KeyModifiers.None, false, out _));
    }

    [Fact]
    public void TryResolve_LetsSlashBeTypedIntoTheFieldItJustFocused() {
        // The bug this guards: a text-safe "/" is consumed by the shell before reaching the focused box,
        // so the character can never be typed into the filter or the path bar.
        Assert.False(ShortcutCatalog.TryResolve(
            Key.OemQuestion, KeyModifiers.None, textInputFocused: true, ShortcutScope.Processes, out _));
        Assert.False(ShortcutCatalog.TryResolve(
            Key.OemQuestion, KeyModifiers.None, textInputFocused: true, ShortcutScope.FileExplorer, out _));
    }

    [Fact]
    public void TryResolve_OffersTheResultArrowsOnlyWhileTheSearchDropdownIsOpen() {
        // Search scope is reported by the shell only while the dropdown is up; everywhere else the bare
        // arrows must keep scrolling the page.
        Assert.True(ShortcutCatalog.TryResolve(
            Key.Down, KeyModifiers.None, textInputFocused: true, ShortcutScope.Search, out var next));
        Assert.Equal(ShortcutId.SelectNextResult, next);

        Assert.True(ShortcutCatalog.TryResolve(
            Key.Up, KeyModifiers.None, textInputFocused: true, ShortcutScope.Search, out var previous));
        Assert.Equal(ShortcutId.SelectPreviousResult, previous);

        Assert.False(Resolve(Key.Down, KeyModifiers.None, false, out _));
        Assert.False(ShortcutCatalog.TryResolve(
            Key.Up, KeyModifiers.None, false, ShortcutScope.Processes, out _));
    }

    [Fact]
    public void TryResolve_LeavesBareTabToTheFocusManager() {
        // Accepting a ghosted completion is owned by the field showing one, not by this table: only the
        // focused control knows whether there is a suggestion to accept. Everywhere else Tab must keep
        // moving focus, which it cannot do if the shell claims it.
        Assert.False(Resolve(Key.Tab, KeyModifiers.None, textInputFocused: true, out _));
        Assert.False(Resolve(Key.Tab, KeyModifiers.None, textInputFocused: false, out _));
    }

    [Fact]
    public void TryResolve_FallsBackToGlobalWhenTheTabClaimsNothing() {
        Assert.True(ShortcutCatalog.TryResolve(
            Key.D1, KeyModifiers.Control, false, ShortcutScope.FileExplorer, out var id));
        Assert.Equal(ShortcutId.NavigateTab1, id);
    }

    [Fact]
    public void TryResolve_IgnoresAnotherTabsBindings() {
        // Delete only ends a task on Processes; elsewhere it must stay unbound.
        Assert.False(ShortcutCatalog.TryResolve(
            Key.Delete, KeyModifiers.None, false, ShortcutScope.FileExplorer, out _));
        Assert.True(ShortcutCatalog.TryResolve(
            Key.Delete, KeyModifiers.None, false, ShortcutScope.Processes, out _));
    }

    [Fact]
    public void TryResolve_KeepsDismissalKeysLiveWhileTypingInATextBox() {
        // Esc, F1 and F5 don't type a character, so they stay available inside a search box.
        Assert.True(Resolve(Key.Escape, KeyModifiers.None, textInputFocused: true, out var esc));
        Assert.Equal(ShortcutId.Escape, esc);
        Assert.True(Resolve(Key.F5, KeyModifiers.None, textInputFocused: true, out _));
        Assert.True(Resolve(Key.F1, KeyModifiers.None, textInputFocused: true, out _));
    }

    [Fact]
    public void TryResolve_DistinguishesShiftedGestures() {
        Assert.True(Resolve(Key.Tab, KeyModifiers.Control, false, out var next));
        Assert.Equal(ShortcutId.NextTab, next);

        Assert.True(Resolve(Key.Tab, KeyModifiers.Control | KeyModifiers.Shift, false, out var previous));
        Assert.Equal(ShortcutId.PreviousTab, previous);
    }

    [Fact]
    public void TryResolve_RequiresAnExactModifierMatch() {
        // A bare digit types into whatever has focus; only Ctrl+digit navigates.
        Assert.False(Resolve(Key.D1, KeyModifiers.None, false, out _));
        Assert.False(Resolve(Key.D1, KeyModifiers.Control | KeyModifiers.Alt, false, out _));
    }

    [Fact]
    public void TryResolve_ReturnsFalseForUnboundKeys() {
        Assert.False(Resolve(Key.Q, KeyModifiers.Control, false, out _));
    }

    [Fact]
    public void TryResolve_StillFiresControlGesturesWhileTypingInATextBox() {
        Assert.True(Resolve(Key.D1, KeyModifiers.Control, textInputFocused: true, out var id));
        Assert.Equal(ShortcutId.NavigateTab1, id);
    }

    [Fact]
    public void TryResolve_SuppressesTextUnsafeGesturesWhileTypingInATextBox() {
        foreach (var shortcut in ShortcutCatalog.All.Where(s => !s.AllowInTextInput))
            foreach (var gesture in shortcut.Gestures)
                Assert.False(ShortcutCatalog.TryResolve(
                    gesture.Key, gesture.KeyModifiers, textInputFocused: true, shortcut.Scope, out _));
    }

    [Fact]
    public void TryResolve_ResolvesEveryBoundGestureUnderItsOwnScope() {
        var resolved = new List<ShortcutId>();

        foreach (var shortcut in ShortcutCatalog.All)
            foreach (var gesture in shortcut.Gestures) {
                Assert.True(ShortcutCatalog.TryResolve(
                    gesture.Key, gesture.KeyModifiers, false, shortcut.Scope, out var id));
                Assert.Equal(shortcut.Id, id);
                resolved.Add(id);
            }

        // Distinct, not All.Count: an action may be bound in more than one scope (see
        // All_BindsNoActionTwiceWithinAScope). Every one of them must still be reachable.
        Assert.Equal(
            ShortcutCatalog.All.Select(s => s.Id).Distinct().Count(),
            resolved.Distinct().Count());
    }
}
