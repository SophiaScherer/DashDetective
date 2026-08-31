using Avalonia.Input;
using System;
using System.Collections.Generic;

namespace DashDetective.Shared.Shortcuts;

/// <summary>
/// The bindings actually in force: <see cref="ShortcutCatalog"/>'s defaults with the user's overrides
/// applied. The catalog stays the immutable default table; resolution and the Help grouping move here,
/// because both have to see what the user chose rather than what shipped.
///
/// A rebind replaces <b>all</b> of a shortcut's default gestures with the one that was captured. F5 and
/// Ctrl+R both refresh by default; rebinding Refresh to Ctrl+G leaves Ctrl+G alone as the binding, which
/// is what "this is my shortcut for it" means.
/// </summary>
public sealed class ShortcutBindings {
    private readonly Dictionary<ShortcutId, KeyGesture> _overrides = [];
    private IReadOnlyList<Shortcut>? _all;
    private IReadOnlyList<ShortcutGroup>? _helpGroups;

    /// <summary>Raised after any rebind or reset, so Help, search and the Settings list re-read.</summary>
    public event Action? Changed;

    /// <summary>Every shortcut with the user's choices applied, in catalog order.</summary>
    public IReadOnlyList<Shortcut> All => _all ??= BuildAll();

    /// <summary>The listed shortcuts grouped by scope, as Help renders them.</summary>
    public IReadOnlyList<ShortcutGroup> HelpGroups => _helpGroups ??= ShortcutCatalog.GroupForHelp(All);

    /// <summary>Whether this shortcut is on something other than its shipped binding.</summary>
    public bool IsCustom(ShortcutId id) => _overrides.ContainsKey(id);

    /// <summary>Whether anything at all has been rebound — drives the "Restore defaults" affordance.</summary>
    public bool HasOverrides => _overrides.Count > 0;

    /// <summary>Resolves a key press to the action it triggers, against the live bindings. Same rule as
    /// the catalog's: the active tab's binding wins, and a global one is used only when that tab claims
    /// nothing.</summary>
    public bool TryResolve(
        Key key, KeyModifiers modifiers, bool textInputFocused, ShortcutScope scope, out ShortcutId id) =>
        ShortcutCatalog.TryResolve(All, key, modifiers, textInputFocused, scope, out id);

    /// <summary>
    /// Binds <paramref name="gesture"/> to <paramref name="id"/>, unless something in the same scope
    /// already has it — in which case nothing changes and <paramref name="conflict"/> names the holder.
    ///
    /// Only the <b>same scope</b> counts as a clash. Two tabs may share a gesture and already do: Alt+↑
    /// sorts on Processes and climbs a folder on File Explorer, which is safe because only one tab is
    /// ever current.
    /// </summary>
    public bool TryRebind(ShortcutId id, KeyGesture gesture, out ShortcutId conflict) {
        conflict = default;

        var target = Find(id);
        if (target is null)
            return false;

        foreach (var shortcut in All) {
            if (shortcut.Id == id || shortcut.Scope != target.Scope)
                continue;

            foreach (var existing in shortcut.Gestures)
                if (existing.Key == gesture.Key && existing.KeyModifiers == gesture.KeyModifiers) {
                    conflict = shortcut.Id;
                    return false;
                }
        }

        _overrides[id] = gesture;
        Invalidate();
        return true;
    }

    /// <summary>Puts one shortcut back on its shipped binding.</summary>
    public void ResetToDefault(ShortcutId id) {
        if (_overrides.Remove(id))
            Invalidate();
    }

    /// <summary>Puts every shortcut back on its shipped binding.</summary>
    public void ResetAll() {
        if (_overrides.Count == 0)
            return;

        _overrides.Clear();
        Invalidate();
    }

    /// <summary>The current overrides, for persisting. Empty when everything is on its default.</summary>
    public IReadOnlyDictionary<ShortcutId, KeyGesture> Overrides => _overrides;

    /// <summary>Applies a persisted set, replacing whatever is loaded. Raises <see cref="Changed"/> once
    /// rather than per entry, so the shell rebuilds its lists a single time on startup.</summary>
    public void Load(IReadOnlyDictionary<ShortcutId, KeyGesture> overrides) {
        _overrides.Clear();
        foreach (var (id, gesture) in overrides)
            _overrides[id] = gesture;

        Invalidate();
    }

    /// <summary>The default binding's display string, for showing what a reset would go back to.</summary>
    public static string DefaultKeys(ShortcutId id) {
        foreach (var shortcut in ShortcutCatalog.All)
            if (shortcut.Id == id)
                return shortcut.Keys.Length > 0 ? shortcut.Keys : Describe(shortcut.Gestures);

        return "";
    }

    private Shortcut? Find(ShortcutId id) {
        foreach (var shortcut in All)
            if (shortcut.Id == id)
                return shortcut;

        return null;
    }

    /// <summary>Rebuilds the effective table. An overridden entry carries the captured gesture alone and
    /// a generated display string; an untouched one is the catalog's own record, hand-written copy and
    /// all.</summary>
    private IReadOnlyList<Shortcut> BuildAll() {
        var all = new List<Shortcut>(ShortcutCatalog.All.Count);
        foreach (var shortcut in ShortcutCatalog.All)
            all.Add(_overrides.TryGetValue(shortcut.Id, out var gesture)
                ? shortcut with { Gestures = [gesture], Keys = GestureFormatter.Describe(gesture) }
                : shortcut);

        return all;
    }

    private static string Describe(IReadOnlyList<KeyGesture> gestures) =>
        gestures.Count == 0 ? "" : GestureFormatter.Describe(gestures[0]);

    private void Invalidate() {
        _all = null;
        _helpGroups = null;
        Changed?.Invoke();
    }
}
