using System.Globalization;

namespace DashDetective.Tabs.FileExplorer;

/// <summary>
/// Humanises a byte count as B / KB / MB / GB / TB, using the same power-of-two idiom as the
/// Dashboard's capacity formatting. KB and bytes render whole; MB and up keep one significant
/// decimal (dropping a trailing ".0"). Always InvariantCulture, matching the app's convention.
/// </summary>
public static class FileSizeFormatter {
    private const double Kb = 1024d;
    private const double Mb = Kb * 1024;
    private const double Gb = Mb * 1024;
    private const double Tb = Gb * 1024;

    public static string Format(long bytes) {
        if (bytes < 0)
            bytes = 0;

        var c = CultureInfo.InvariantCulture;
        if (bytes >= Tb) return string.Format(c, "{0:0.#} TB", bytes / Tb);
        if (bytes >= Gb) return string.Format(c, "{0:0.#} GB", bytes / Gb);
        if (bytes >= Mb) return string.Format(c, "{0:0.#} MB", bytes / Mb);
        if (bytes >= Kb) return string.Format(c, "{0:0} KB", bytes / Kb);
        return string.Format(c, "{0} B", bytes);
    }
}
