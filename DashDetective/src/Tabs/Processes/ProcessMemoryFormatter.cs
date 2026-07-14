using System.Globalization;

namespace DashDetective.Tabs.Processes;

/// <summary>
/// Humanises a process working-set size for the Memory column as whole megabytes ("412 MB",
/// "2048 MB") — the Memory column always reads in MB regardless of size, so values stay directly
/// comparable at a glance. Kept tab-local (File Explorer has its own <c>FileSizeFormatter</c>);
/// promote a shared byte formatter to <c>src/Shared</c> if a cross-tab refactor is signed off.
/// </summary>
public static class ProcessMemoryFormatter {
    private const double Mb = 1024d * 1024d;

    public static string Format(long bytes) {
        if (bytes <= 0)
            return "0 MB";

        return (bytes / Mb).ToString("F0", CultureInfo.InvariantCulture) + " MB";
    }
}
