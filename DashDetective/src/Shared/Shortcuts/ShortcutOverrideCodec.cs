using Avalonia.Input;
using System;
using System.Collections.Generic;
using System.Text;

namespace DashDetective.Shared.Shortcuts;

/// <summary>
/// Encodes the user's rebound shortcuts as one flat string, so <c>AppSettings</c> — and the settings
/// file — stay free of any knowledge of what a shortcut is. The same shape as <c>WidgetOrders</c> and
/// <c>EnumListCodec</c>, and for the same reason: the record's value equality (which the round-trip
/// relies on) compares a collection by reference.
///
/// Ids, keys and modifiers are stored by NAME, never by ordinal: a release that inserts an enum member
/// must not silently re-point someone's keyboard at a different action. Anything unrecognised is skipped
/// rather than fatal, so a binding for an action that no longer exists is dropped and that shortcut
/// simply stays on its default.
/// </summary>
public static class ShortcutOverrideCodec {
    // ASCII record and unit separators. Control characters, so neither can occur in an enum member name,
    // which is what makes joining without escaping safe.
    private const char EntrySeparator = (char)0x1E;
    private const char FieldSeparator = (char)0x1F;

    /// <summary>The overrides as one persistable string. Empty when nothing is rebound.</summary>
    public static string Encode(IReadOnlyDictionary<ShortcutId, KeyGesture> overrides) {
        var builder = new StringBuilder();
        foreach (var (id, gesture) in overrides) {
            if (builder.Length > 0)
                builder.Append(EntrySeparator);

            builder.Append(id).Append(FieldSeparator)
                   .Append(gesture.Key).Append(FieldSeparator)
                   .Append(gesture.KeyModifiers);
        }

        return builder.ToString();
    }

    /// <summary>Reads back what <see cref="Encode"/> wrote. Never throws: a malformed or hand-edited
    /// string yields whatever entries were still readable, and an empty map at worst.</summary>
    public static IReadOnlyDictionary<ShortcutId, KeyGesture> Decode(string? encoded) {
        var overrides = new Dictionary<ShortcutId, KeyGesture>();
        if (string.IsNullOrEmpty(encoded))
            return overrides;

        foreach (var entry in encoded.Split(EntrySeparator, StringSplitOptions.RemoveEmptyEntries)) {
            var fields = entry.Split(FieldSeparator);
            if (fields.Length != 3)
                continue;

            // A hand-edited file, or one written by a version that knew a different set of actions.
            if (!Enum.TryParse<ShortcutId>(fields[0], out var id) ||
                !Enum.TryParse<Key>(fields[1], out var key) ||
                !Enum.TryParse<KeyModifiers>(fields[2], out var modifiers))
                continue;

            // A modifier-only binding would fire the moment Ctrl was touched. The capture control refuses
            // to produce one; this refuses to load one.
            if (key == Key.None || GestureFormatter.IsModifierKey(key))
                continue;

            overrides[id] = new KeyGesture(key, modifiers);
        }

        return overrides;
    }
}
