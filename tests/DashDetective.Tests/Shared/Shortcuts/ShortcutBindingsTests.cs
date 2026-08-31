using Avalonia.Input;
using DashDetective.Shared.Shortcuts;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace DashDetective.Tests.Shared.Shortcuts;

/// <summary>
/// Covers <see cref="ShortcutBindings"/>: that an override replaces the default and is what resolves,
/// that scope precedence still holds over the rebound table, that a clash inside one scope is refused
/// while a cross-scope duplicate stays legal, and that resets put things back.
/// </summary>
public class ShortcutBindingsTests {
    private static bool Resolves(ShortcutBindings bindings, Key key, KeyModifiers modifiers,
                                 ShortcutScope scope, out ShortcutId id) =>
        bindings.TryResolve(key, modifiers, textInputFocused: false, scope, out id);

    [Fact]
    public void Fresh_MatchesTheCatalogDefaults() {
        var bindings = new ShortcutBindings();

        Assert.False(bindings.HasOverrides);
        Assert.Equal(ShortcutCatalog.All.Count, bindings.All.Count);
        Assert.True(Resolves(bindings, Key.E, KeyModifiers.Control, ShortcutScope.Global, out var id));
        Assert.Equal(ShortcutId.Export, id);
    }

    [Fact]
    public void Rebind_MakesTheNewGestureResolve() {
        var bindings = new ShortcutBindings();

        Assert.True(bindings.TryRebind(ShortcutId.Export, new KeyGesture(Key.G, KeyModifiers.Control), out _));

        Assert.True(Resolves(bindings, Key.G, KeyModifiers.Control, ShortcutScope.Global, out var id));
        Assert.Equal(ShortcutId.Export, id);
        Assert.True(bindings.IsCustom(ShortcutId.Export));
        Assert.True(bindings.HasOverrides);
    }

    /// <summary>A rebind replaces every default gesture, not just one of them. Refresh ships as F5 and
    /// Ctrl+R; after choosing Ctrl+G, neither of the old two may still fire it.</summary>
    [Fact]
    public void Rebind_ReplacesAllOfTheDefaultGestures() {
        var bindings = new ShortcutBindings();

        Assert.True(bindings.TryRebind(ShortcutId.Refresh, new KeyGesture(Key.G, KeyModifiers.Control), out _));

        Assert.False(Resolves(bindings, Key.F5, KeyModifiers.None, ShortcutScope.Global, out _));
        Assert.False(Resolves(bindings, Key.R, KeyModifiers.Control, ShortcutScope.Global, out _));
        Assert.True(Resolves(bindings, Key.G, KeyModifiers.Control, ShortcutScope.Global, out var id));
        Assert.Equal(ShortcutId.Refresh, id);
    }

    /// <summary>The Help copy has to describe the binding the user chose, not the one it replaced.</summary>
    [Fact]
    public void Rebind_RegeneratesTheDisplayKeys() {
        var bindings = new ShortcutBindings();
        bindings.TryRebind(ShortcutId.Export, new KeyGesture(Key.G, KeyModifiers.Control | KeyModifiers.Shift), out _);

        var export = bindings.All.Single(shortcut => shortcut.Id == ShortcutId.Export);
        Assert.Equal("Ctrl+Shift+G", export.Keys);
    }

    /// <summary>An untouched entry keeps the catalog's hand-written copy, which says things a formatter
    /// cannot — "Ctrl+1 … Ctrl+9" covers nine bindings in one row.</summary>
    [Fact]
    public void AnUntouchedShortcut_KeepsItsHandWrittenKeys() {
        var bindings = new ShortcutBindings();

        var tabs = bindings.All.Single(shortcut => shortcut.Id == ShortcutId.NavigateTab1);
        Assert.Equal("Ctrl+1 … Ctrl+9", tabs.Keys);
    }

    // ----- Conflicts -----

    [Fact]
    public void Rebind_RefusesAGestureAlreadyUsedInTheSameScope() {
        var bindings = new ShortcutBindings();

        // Ctrl+F is universal search, and both live in the Global scope.
        Assert.False(bindings.TryRebind(ShortcutId.Export, new KeyGesture(Key.F, KeyModifiers.Control),
                                        out var conflict));

        Assert.Equal(ShortcutId.FocusSearch, conflict);
        Assert.False(bindings.IsCustom(ShortcutId.Export));
        Assert.True(Resolves(bindings, Key.F, KeyModifiers.Control, ShortcutScope.Global, out var id));
        Assert.Equal(ShortcutId.FocusSearch, id);   // the holder kept it
    }

    /// <summary>Two tabs may share a gesture and already do — Alt+↑ sorts on Processes and climbs a
    /// folder on File Explorer — because only one tab is ever current.</summary>
    [Fact]
    public void Rebind_AllowsAGestureUsedInADifferentScope() {
        var bindings = new ShortcutBindings();

        Assert.True(bindings.TryRebind(ShortcutId.NextPage, new KeyGesture(Key.Delete), out _));

        // Network's new binding, and Processes' End Task, are the same keys in different scopes.
        Assert.True(Resolves(bindings, Key.Delete, KeyModifiers.None, ShortcutScope.Network, out var network));
        Assert.Equal(ShortcutId.NextPage, network);

        Assert.True(Resolves(bindings, Key.Delete, KeyModifiers.None, ShortcutScope.Processes, out var processes));
        Assert.Equal(ShortcutId.EndTask, processes);
    }

    /// <summary>Scope precedence has to survive rebinding: a tab's own binding still beats a global one.</summary>
    [Fact]
    public void ATabBinding_StillBeatsAGlobalOne_AfterARebind() {
        var bindings = new ShortcutBindings();
        bindings.TryRebind(ShortcutId.FocusFilter, new KeyGesture(Key.E, KeyModifiers.Control), out _);

        Assert.True(Resolves(bindings, Key.E, KeyModifiers.Control, ShortcutScope.Processes, out var onTab));
        Assert.Equal(ShortcutId.FocusFilter, onTab);

        // Elsewhere the global binding is untouched.
        Assert.True(Resolves(bindings, Key.E, KeyModifiers.Control, ShortcutScope.Global, out var global));
        Assert.Equal(ShortcutId.Export, global);
    }

    /// <summary>Re-binding a shortcut to what it already has is a no-op, not a clash with itself.</summary>
    [Fact]
    public void Rebind_ToItsOwnGesture_IsAccepted() =>
        Assert.True(new ShortcutBindings()
            .TryRebind(ShortcutId.Export, new KeyGesture(Key.E, KeyModifiers.Control), out _));

    /// <summary>The text-input guard is a property of the action, so it has to follow a rebind.</summary>
    [Fact]
    public void Rebind_KeepsTheTextInputGuard() {
        var bindings = new ShortcutBindings();
        bindings.TryRebind(ShortcutId.EndTask, new KeyGesture(Key.K, KeyModifiers.Control), out _);

        Assert.False(bindings.TryResolve(Key.K, KeyModifiers.Control, textInputFocused: true,
                                         ShortcutScope.Processes, out _));
        Assert.True(bindings.TryResolve(Key.K, KeyModifiers.Control, textInputFocused: false,
                                        ShortcutScope.Processes, out _));
    }

    // ----- Resetting -----

    [Fact]
    public void ResetToDefault_PutsTheShippedGestureBack() {
        var bindings = new ShortcutBindings();
        bindings.TryRebind(ShortcutId.Export, new KeyGesture(Key.G, KeyModifiers.Control), out _);

        bindings.ResetToDefault(ShortcutId.Export);

        Assert.False(bindings.IsCustom(ShortcutId.Export));
        Assert.False(Resolves(bindings, Key.G, KeyModifiers.Control, ShortcutScope.Global, out _));
        Assert.True(Resolves(bindings, Key.E, KeyModifiers.Control, ShortcutScope.Global, out _));
    }

    [Fact]
    public void ResetAll_ClearsEveryOverride() {
        var bindings = new ShortcutBindings();
        bindings.TryRebind(ShortcutId.Export, new KeyGesture(Key.G, KeyModifiers.Control), out _);
        bindings.TryRebind(ShortcutId.ToggleLive, new KeyGesture(Key.J, KeyModifiers.Control), out _);

        bindings.ResetAll();

        Assert.False(bindings.HasOverrides);
        Assert.Empty(bindings.Overrides);
    }

    // ----- Change notification -----

    [Fact]
    public void Changed_FiresOnRebindAndReset_ButNotOnANoOpReset() {
        var bindings = new ShortcutBindings();
        var changes = 0;
        bindings.Changed += () => changes++;

        bindings.TryRebind(ShortcutId.Export, new KeyGesture(Key.G, KeyModifiers.Control), out _);
        Assert.Equal(1, changes);

        bindings.ResetToDefault(ShortcutId.Export);
        Assert.Equal(2, changes);

        bindings.ResetToDefault(ShortcutId.Export);   // already default
        bindings.ResetAll();                          // nothing to clear
        Assert.Equal(2, changes);
    }

    /// <summary>A refused rebind changes nothing, so it must not announce a change either.</summary>
    [Fact]
    public void Changed_DoesNotFireForARefusedRebind() {
        var bindings = new ShortcutBindings();
        var changes = 0;
        bindings.Changed += () => changes++;

        bindings.TryRebind(ShortcutId.Export, new KeyGesture(Key.F, KeyModifiers.Control), out _);

        Assert.Equal(0, changes);
    }

    // ----- Loading persisted overrides -----

    [Fact]
    public void Load_AppliesAPersistedSet() {
        var bindings = new ShortcutBindings();

        bindings.Load(new Dictionary<ShortcutId, KeyGesture> {
            [ShortcutId.Export] = new(Key.G, KeyModifiers.Control),
        });

        Assert.True(Resolves(bindings, Key.G, KeyModifiers.Control, ShortcutScope.Global, out var id));
        Assert.Equal(ShortcutId.Export, id);
    }

    /// <summary>Loading is a replace, not a merge: the settings file is the whole truth about overrides.</summary>
    [Fact]
    public void Load_ReplacesWhateverWasAlreadyThere() {
        var bindings = new ShortcutBindings();
        bindings.TryRebind(ShortcutId.ToggleLive, new KeyGesture(Key.J, KeyModifiers.Control), out _);

        bindings.Load(new Dictionary<ShortcutId, KeyGesture>());

        Assert.False(bindings.HasOverrides);
        Assert.True(Resolves(bindings, Key.P, KeyModifiers.Control, ShortcutScope.Global, out _));
    }

    /// <summary>Help renders these, so a rebound entry has to reach the groups too.</summary>
    [Fact]
    public void HelpGroups_ShowTheReboundKeys() {
        var bindings = new ShortcutBindings();
        bindings.TryRebind(ShortcutId.Export, new KeyGesture(Key.G, KeyModifiers.Control), out _);

        var row = bindings.HelpGroups
            .SelectMany(group => group.Shortcuts)
            .Single(shortcut => shortcut.Id == ShortcutId.Export);

        Assert.Equal("Ctrl+G", row.Keys);
    }

    [Fact]
    public void DefaultKeys_ReportsTheShippedCopy() =>
        Assert.Equal("Ctrl+E", ShortcutBindings.DefaultKeys(ShortcutId.Export));
}
