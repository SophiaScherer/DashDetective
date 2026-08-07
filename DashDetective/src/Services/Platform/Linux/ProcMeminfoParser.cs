using System;
using System.Collections.Generic;
using System.Globalization;

namespace DashDetective.Services.Platform.Linux;

/// <summary>
/// Parses <c>/proc/meminfo</c>'s <c>Key: value kB</c> lines into a byte-valued lookup. Shared format
/// knowledge rather than sampler logic, so it lives beside <see cref="ProcStatParser"/> and serves both
/// the memory sampler and the system-performance provider.
///
/// The file's <c>kB</c> unit is <b>kibibytes</b> despite the label, so every suffixed value is scaled by
/// 1024 — the classic off-by-1024. Values with no unit (<c>HugePages_Total</c>) are counts, not sizes, and
/// are kept verbatim.
/// </summary>
internal static class ProcMeminfoParser {
    private const ulong BytesPerKibibyte = 1024;

    /// <summary>
    /// Parses every well-formed line, keyed by field name (ordinal, as the kernel writes it). Malformed
    /// and unrecognised lines are skipped rather than failing the parse, since the file gains fields
    /// across kernel versions. A duplicate key keeps the first occurrence.
    /// </summary>
    internal static IReadOnlyDictionary<string, ulong> Parse(IReadOnlyList<string> lines) {
        var values = new Dictionary<string, ulong>(StringComparer.Ordinal);

        foreach (var line in lines) {
            var colon = line.IndexOf(':');
            if (colon <= 0)
                continue;

            var key = line.AsSpan(0, colon).Trim();
            var rest = line.AsSpan(colon + 1).Trim();
            if (key.IsEmpty || rest.IsEmpty)
                continue;

            // Split the number from its optional unit; anything else on the line makes it unparseable.
            var space = rest.IndexOf(' ');
            var number = space < 0 ? rest : rest[..space];
            ReadOnlySpan<char> unit = space < 0 ? default : rest[(space + 1)..].Trim();

            if (!ulong.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out var value))
                continue;

            if (!unit.IsEmpty) {
                if (!unit.Equals("kB", StringComparison.OrdinalIgnoreCase))
                    continue;

                // Saturate rather than wrap: a nonsensical value must not read as a tiny one.
                value = value <= ulong.MaxValue / BytesPerKibibyte
                    ? value * BytesPerKibibyte
                    : ulong.MaxValue;
            }

            _ = values.TryAdd(key.ToString(), value);
        }

        return values;
    }

    /// <summary>The field's value, or 0 when it is absent — the "not reported" contract every caller
    /// treats as a missing reading.</summary>
    internal static ulong Value(IReadOnlyDictionary<string, ulong> fields, string key) =>
        fields.TryGetValue(key, out var value) ? value : 0;
}
