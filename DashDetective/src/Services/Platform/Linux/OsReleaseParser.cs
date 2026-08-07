using System;
using System.Collections.Generic;

namespace DashDetective.Services.Platform.Linux;

/// <summary>
/// Parses <c>/etc/os-release</c>'s <c>KEY=value</c> lines into a string lookup. Shared format knowledge
/// rather than provider logic, so it lives beside <see cref="ProcMeminfoParser"/> and
/// <see cref="ProcStatParser"/>.
///
/// The file is a shell fragment, so values are usually quoted
/// (<c>PRETTY_NAME="Ubuntu 24.04.1 LTS"</c>) and sometimes not (<c>VERSION_ID=24.04</c>). A matched pair
/// of surrounding quotes is stripped; anything else is kept verbatim.
/// </summary>
internal static class OsReleaseParser {
    /// <summary>
    /// Parses every well-formed line, keyed by field name (ordinal, as the distro writes it). Comments and
    /// malformed lines are skipped rather than failing the parse, since the file is free to carry keys this
    /// app does not know. A duplicate key keeps the first occurrence.
    /// </summary>
    internal static IReadOnlyDictionary<string, string> Parse(IReadOnlyList<string> lines) {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var line in lines) {
            var trimmed = line.AsSpan().Trim();
            if (trimmed.IsEmpty || trimmed[0] == '#')
                continue;

            var equals = trimmed.IndexOf('=');
            if (equals <= 0)
                continue;

            var key = trimmed[..equals].Trim();
            if (key.IsEmpty)
                continue;

            _ = values.TryAdd(key.ToString(), Unquote(trimmed[(equals + 1)..].Trim()));
        }

        return values;
    }

    /// <summary>The field's value, or "" when it is absent — the "not reported" contract every caller
    /// treats as a missing reading.</summary>
    internal static string Value(IReadOnlyDictionary<string, string> fields, string key) =>
        fields.TryGetValue(key, out var value) ? value : "";

    /// <summary>Strips one matched pair of surrounding single or double quotes. An unbalanced quote is
    /// left alone rather than half-stripped.</summary>
    private static string Unquote(ReadOnlySpan<char> value) {
        if (value.Length >= 2 && (value[0] == '"' || value[0] == '\'') && value[^1] == value[0])
            return value[1..^1].ToString();

        return value.ToString();
    }
}
