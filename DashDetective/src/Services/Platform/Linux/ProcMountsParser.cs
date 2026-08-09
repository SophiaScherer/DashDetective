using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace DashDetective.Services.Platform.Linux;

/// <summary>One line of <c>/proc/mounts</c>, unescaped: the backing device (<c>/dev/sda2</c>,
/// <c>tmpfs</c>), where it is mounted, and its filesystem type.</summary>
internal readonly record struct MountEntry(string Device, string MountPoint, string FileSystem);

/// <summary>
/// Parses <c>/proc/mounts</c> — space-separated <c>device mountpoint fstype options dump pass</c>. Format
/// knowledge lives here rather than in the volume provider, matching <see cref="ProcStatParser"/> and
/// <see cref="ProcMeminfoParser"/>.
///
/// <b>The device and mount-point fields are octal-escaped.</b> The separator is a space, so the kernel
/// writes any space, tab, newline or backslash inside a path as <c>\040</c>, <c>\011</c>, <c>\012</c> and
/// <c>\134</c>. Reading them raw leaves a visible "\040" in the Partitions table for every mount point with
/// a space in it — which is most removable media.
///
/// Pure and side-effect-free, and never throws: a short or malformed line is skipped rather than failing
/// the read.
/// </summary>
internal static class ProcMountsParser {
    // device, mountpoint, fstype — the fields we read. Later columns (options, dump, pass) may be absent on
    // some kernels' mtab-alikes, so the check is a minimum rather than an exact count.
    private const int MinimumFields = 3;

    private const int OctalEscapeLength = 4;

    /// <summary>Parses every well-formed line, in file order.</summary>
    internal static IReadOnlyList<MountEntry> Parse(IReadOnlyList<string> lines) {
        var entries = new List<MountEntry>();

        foreach (var line in lines) {
            var fields = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length < MinimumFields)
                continue;

            entries.Add(new MountEntry(Unescape(fields[0]), Unescape(fields[1]), fields[2]));
        }

        return entries;
    }

    /// <summary>Expands the kernel's <c>\NNN</c> octal escapes. A backslash that does not start a
    /// well-formed escape is kept as written — a literal backslash in a path is legal.</summary>
    internal static string Unescape(string field) {
        if (!field.Contains('\\', StringComparison.Ordinal))
            return field;

        var text = new StringBuilder(field.Length);
        for (var i = 0; i < field.Length; i++) {
            if (field[i] == '\\' && TryReadOctal(field, i, out var value)) {
                text.Append(value);
                i += OctalEscapeLength - 1;
                continue;
            }

            text.Append(field[i]);
        }

        return text.ToString();
    }

    /// <summary>Reads the three octal digits after a backslash at <paramref name="start"/>.</summary>
    private static bool TryReadOctal(string field, int start, out char value) {
        value = '\0';
        if (start + OctalEscapeLength > field.Length)
            return false;

        var code = 0;
        for (var i = start + 1; i < start + OctalEscapeLength; i++) {
            if (field[i] < '0' || field[i] > '7')
                return false;

            code = (code * 8) + (field[i] - '0');
        }

        value = (char)code;
        return true;
    }

    /// <summary>Expands udev's <c>\xNN</c> hex escapes, the different convention the
    /// <c>/dev/disk/by-label</c> symlink names use for the same job.</summary>
    internal static string UnescapeUdev(string name) {
        if (!name.Contains("\\x", StringComparison.Ordinal))
            return name;

        var text = new StringBuilder(name.Length);
        for (var i = 0; i < name.Length; i++) {
            if (name[i] == '\\'
                && i + 4 <= name.Length
                && name[i + 1] == 'x'
                && int.TryParse(
                    name.AsSpan(i + 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code)) {
                text.Append((char)code);
                i += 3;
                continue;
            }

            text.Append(name[i]);
        }

        return text.ToString();
    }
}
