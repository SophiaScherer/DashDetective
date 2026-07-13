using System.Globalization;

namespace DashDetective.Tabs.Processes;

/// <summary>
/// Humanises a process working-set size for the Memory column: whole megabytes below 1 GB
/// ("412 MB"), one decimal gigabyte above ("1.8 GB"), matching Task Manager's Processes view. Kept
/// tab-local (File Explorer has its own <c>FileSizeFormatter</c>); promote a shared byte formatter to
/// <c>src/Shared</c> if a cross-tab refactor is signed off.
/// </summary>
public static class ProcessMemoryFormatter {
    private const double Mb = 1024d * 1024d;
    private const double Gb = Mb * 1024d;

    public static string Format(long bytes) {
        if (bytes <= 0)
            return "0 MB";

        if (bytes >= Gb)
            return (bytes / Gb).ToString("F1", CultureInfo.InvariantCulture) + " GB";

        return (bytes / Mb).ToString("F0", CultureInfo.InvariantCulture) + " MB";
    }
}
