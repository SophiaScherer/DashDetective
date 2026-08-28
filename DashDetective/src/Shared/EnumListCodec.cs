using System;
using System.Collections.Generic;
using System.Text;

namespace DashDetective.Shared;

/// <summary>
/// Encodes a list of enum values as one flat string, so <c>AppSettings</c> can persist it without
/// knowing what the values mean — the same shape as <c>WidgetOrders</c> and <c>ToolkitPins</c>.
///
/// Values are stored by NAME, never by ordinal: a release that inserts a member must not silently
/// re-point a saved record at different ones.
/// </summary>
public static class EnumListCodec {
    // An ASCII unit separator. A control character, so it cannot occur in a member name, which is what
    // makes joining without escaping safe.
    private const char Separator = (char)0x1F;

    /// <summary>The values as one persistable string, repeats dropped.</summary>
    public static string Encode<T>(IEnumerable<T> values) where T : struct, Enum {
        var builder = new StringBuilder();
        var seen = new HashSet<T>();
        foreach (var value in values) {
            if (!seen.Add(value))
                continue;
            if (builder.Length > 0)
                builder.Append(Separator);
            builder.Append(value.ToString());
        }

        return builder.ToString();
    }

    /// <summary>The record read back. Total: a name no member answers to — a hand-edit, or a member
    /// since removed — is dropped rather than thrown on.</summary>
    public static List<T> Decode<T>(string? encoded) where T : struct, Enum {
        var values = new List<T>();
        if (string.IsNullOrEmpty(encoded))
            return values;

        var seen = new HashSet<T>();
        foreach (var field in encoded.Split(Separator))
            if (Enum.TryParse<T>(field, out var value) && Enum.IsDefined(value) && seen.Add(value))
                values.Add(value);

        return values;
    }
}
