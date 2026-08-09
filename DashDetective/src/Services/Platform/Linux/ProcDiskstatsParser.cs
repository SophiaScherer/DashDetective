using System;
using System.Collections.Generic;
using System.Globalization;

namespace DashDetective.Services.Platform.Linux;

/// <summary>One <c>/proc/diskstats</c> row's monotonic counters. Sectors are 512 bytes; the millisecond
/// fields are cumulative time, and <c>InFlight</c> alone is an instantaneous depth rather than a
/// total.</summary>
internal readonly record struct DiskStatsCounters(
    string Name, ulong ReadsCompleted, ulong SectorsRead, ulong MillisecondsReading,
    ulong WritesCompleted, ulong SectorsWritten, ulong MillisecondsWriting,
    ulong InFlight, ulong IoMilliseconds);

/// <summary>
/// Parses <c>/proc/diskstats</c>, keyed by the packed <c>major:minor</c> its first two columns carry — the
/// same identity <see cref="SysBlockFacts"/> derives, which is what lets an independently-ticking sampler
/// agree with the drive cards about which disk is which.
///
/// <b>Parsed by index with a length check.</b> The row grew from 14 fields to 18 in 4.18 (discards) and 20
/// in 5.5 (flushes), so only the first 14 may be assumed present.
///
/// Pure and side-effect-free, and never throws: a short or malformed row is skipped.
/// </summary>
internal static class ProcDiskstatsParser {
    // 1-based columns: 1 major, 2 minor, 3 name, 4 reads completed, 5 reads merged, 6 sectors read,
    // 7 ms reading, 8 writes completed, 9 writes merged, 10 sectors written, 11 ms writing,
    // 12 I/Os in flight, 13 ms doing I/O, 14 weighted ms doing I/O.
    private const int Major = 0;
    private const int Minor = 1;
    private const int Name = 2;
    private const int ReadsCompleted = 3;
    private const int SectorsRead = 5;
    private const int MillisecondsReading = 6;
    private const int WritesCompleted = 7;
    private const int SectorsWritten = 9;
    private const int MillisecondsWriting = 10;
    private const int InFlight = 11;
    private const int IoMilliseconds = 12;

    /// <summary>The oldest layout every kernel still writes; anything shorter is not a stats row.</summary>
    private const int MinimumFields = 14;

    /// <summary>A sector is 512 bytes here regardless of the drive's physical sector size, matching
    /// <c>/sys/block/*/size</c>.</summary>
    internal const int SectorBytes = 512;

    /// <summary>Parses every well-formed row, keyed by packed disk number. A repeated device number keeps
    /// the last row, which is what the kernel means when it rewrites one.</summary>
    internal static IReadOnlyDictionary<int, DiskStatsCounters> Parse(IReadOnlyList<string> lines) {
        var counters = new Dictionary<int, DiskStatsCounters>();

        foreach (var line in lines) {
            var fields = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length < MinimumFields)
                continue;

            if (!TryParseInt(fields[Major], out var major) || !TryParseInt(fields[Minor], out var minor))
                continue;

            counters[SysBlockFacts.Pack(major, minor)] = new DiskStatsCounters(
                fields[Name],
                Value(fields, ReadsCompleted),
                Value(fields, SectorsRead),
                Value(fields, MillisecondsReading),
                Value(fields, WritesCompleted),
                Value(fields, SectorsWritten),
                Value(fields, MillisecondsWriting),
                Value(fields, InFlight),
                Value(fields, IoMilliseconds));
        }

        return counters;
    }

    private static ulong Value(string[] fields, int index) =>
        ulong.TryParse(fields[index], NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;

    private static bool TryParseInt(string field, out int value) =>
        int.TryParse(field, NumberStyles.None, CultureInfo.InvariantCulture, out value);
}
