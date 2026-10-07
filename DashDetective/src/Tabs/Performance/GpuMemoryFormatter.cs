using System.Globalization;

namespace DashDetective.Tabs.Performance;

/// <summary>
/// Formats the GPU "VRAM in use" tile as "used / total", following the Memory rail caption ("19.5 / 32 GB":
/// used to one decimal, total whole from 10 GB up so a small total never reads below its usage). An adapter whose total is under 1 GiB reads in MB for both, so a small
/// carve-out is "0 / 460 MB" rather than "0.0 / 0 GB". An unknown usage is "— / 12 GB", never a fake 0; an
/// unknown total is a bare "—". Always InvariantCulture.
/// </summary>
internal static class GpuMemoryFormatter {
    private const double Gib = 1L << 30;
    private const double Mib = 1L << 20;

    internal static string Format(ulong? usedBytes, ulong? totalBytes) {
        if (totalBytes is not > 0)
            return "—";

        var total = (double)totalBytes.Value;
        if (total < Gib) {
            var usedMb = usedBytes is { } mb ? (mb / Mib).ToString("F0", CultureInfo.InvariantCulture) : "—";
            return $"{usedMb} / {(total / Mib).ToString("F0", CultureInfo.InvariantCulture)} MB";
        }

        var usedGb = usedBytes is { } gb ? (gb / Gib).ToString("F1", CultureInfo.InvariantCulture) : "—";
        var totalGb = total / Gib;
        return $"{usedGb} / {totalGb.ToString(totalGb >= 10 ? "F0" : "F1", CultureInfo.InvariantCulture)} GB";
    }
}
