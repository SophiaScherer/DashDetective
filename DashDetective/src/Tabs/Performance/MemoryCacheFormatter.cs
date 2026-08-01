using System.Globalization;

namespace DashDetective.Tabs.Performance;

/// <summary>
/// Formats the Memory "Cached" stat tile. One decimal of binary GB, matching the In use / Available tiles
/// beside it so the strip reads as one unit. Always InvariantCulture, matching the app's convention. A
/// missing reading (the shared <c>SystemPerformanceProvider</c> reports no cache figure when the counter is
/// unavailable) yields the neutral "—".
/// </summary>
internal static class MemoryCacheFormatter {
    private const double BytesPerGb = 1L << 30;

    /// <summary>Formats <paramref name="cachedBytes"/> as GB, or "—" when there is no reading.</summary>
    public static string Format(ulong? cachedBytes) {
        if (cachedBytes is not > 0)
            return "—";

        return string.Format(CultureInfo.InvariantCulture, "{0:0.0} GB", cachedBytes.Value / BytesPerGb);
    }
}
