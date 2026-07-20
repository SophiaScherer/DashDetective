using Avalonia.Media;
using DashDetective.Shared;
using DashDetective.Shared.Charts;
using System;
using System.Collections.ObjectModel;
using System.Globalization;

namespace DashDetective.Tabs.Storage;

/// <summary>
/// The Storage tab: a read-only drives/health view per the design comp — three drive summary cards over
/// a Partitions table and a Disk Activity chart. Page-scrolls as a whole like the Dashboard/Network (not
/// <see cref="ISelfScrollingPage"/>).
///
/// This is the initial UI pass: surfaces are seeded with <b>static mock data</b>. Live samplers/providers
/// (<c>StorageUsageSampler</c>, <c>DiskInfoProvider</c> in <c>src/Services/SystemMetrics</c>) and the
/// <see cref="IRefreshablePage"/> / <see cref="ILiveSamplingPage"/> seams are a later technical pass.
/// </summary>
public partial class StorageViewModel : ViewModelBase {
    // Fixed semantic brushes (theme/accent-independent, matching the design comp's palette) — parsed like
    // MainWindowViewModel's live dots / PerformanceViewModel's legend brushes. The health colours use a
    // soft (~0.16 alpha) tint of the same hue for the pill fill.
    private static IBrush Brush(string hex) => new SolidColorBrush(Color.Parse(hex));
    private static readonly IBrush HealthyFg = Brush("#6ccb5f");   // green
    private static readonly IBrush HealthyBg = Brush("#296ccb5f"); // green @ 16%
    private static readonly IBrush CautionFg = Brush("#ffcf4d");   // amber
    private static readonly IBrush CautionBg = Brush("#29ffcf4d"); // amber @ 16%
    private static readonly IBrush BarBlue = Brush("#4cc2ff");
    private static readonly IBrush BarGreen = Brush("#6ccb5f");
    private static readonly IBrush BarAmber = Brush("#ffcf4d");

    /// <summary>The drive summary cards shown in the top row (one per physical drive).</summary>
    public ObservableCollection<DriveCard> Drives { get; } = new() {
        new DriveCard {
            Name = "Local Disk (C:)", Model = "Samsung 990 Pro 2TB",
            Health = "Healthy", HealthForeground = HealthyFg, HealthBackground = HealthyBg,
            UsagePercent = 68, BarBrush = BarBlue,
            Used = "1.36 TB", Free = "640 GB", Read = "48 MB/s", Write = "12 MB/s", Temp = "41°C",
        },
        new DriveCard {
            Name = "Data (D:)", Model = "WD Black SN850X 4TB",
            Health = "Healthy", HealthForeground = HealthyFg, HealthBackground = HealthyBg,
            UsagePercent = 42, BarBrush = BarGreen,
            Used = "1.68 TB", Free = "2.32 TB", Read = "8 MB/s", Write = "2 MB/s", Temp = "38°C",
        },
        new DriveCard {
            Name = "Backup (E:)", Model = "Seagate Exos 8TB HDD",
            Health = "Caution", HealthForeground = CautionFg, HealthBackground = CautionBg,
            UsagePercent = 87, BarBrush = BarAmber,
            Used = "6.96 TB", Free = "1.04 TB", Read = "2 MB/s", Write = "0 MB/s", Temp = "44°C",
        },
    };

    /// <summary>The partition rows shown in the Partitions table (one per volume, lettered or not).</summary>
    public ObservableCollection<PartitionRow> Partitions { get; } = new() {
        new PartitionRow { Vol = "C:", Label = "Windows", FileSystem = "NTFS", Capacity = "2.0 TB", Free = "640 GB" },
        new PartitionRow { Vol = "D:", Label = "Data", FileSystem = "NTFS", Capacity = "4.0 TB", Free = "2.32 TB" },
        new PartitionRow { Vol = "E:", Label = "Backup", FileSystem = "NTFS", Capacity = "8.0 TB", Free = "1.04 TB" },
        new PartitionRow { Vol = "—", Label = "Recovery", FileSystem = "NTFS", Capacity = "990 MB", Free = "120 MB" },
        new PartitionRow { Vol = "—", Label = "EFI System", FileSystem = "FAT32", Capacity = "260 MB", Free = "98 MB" },
    };

    // Width of the Disk Activity history, matching the app's charts (60 samples = one per second).
    private const int WindowSeconds = 60;

    // A fixed, deterministic 60-sample mock "disk active time" history, so the chart resembles the design
    // comp's drifting series until the live sampler is wired. Base ~14 % with bounded drift, like the comp.
    private static readonly double[] MockDiskHistory = BuildMockDiskHistory();

    /// <summary>The Disk Activity chart's points ("x,y …") on the shared Sparkline's 0–100 axis.</summary>
    public string DiskPoints { get; } = SparklinePoints.Build(MockDiskHistory, 100);

    /// <summary>The "Active time" readout — the latest sample of the mock history (e.g. "31%").</summary>
    public string DiskActive { get; } =
        Math.Round(MockDiskHistory[^1]).ToString("0", CultureInfo.InvariantCulture) + "%";

    /// <summary>Builds the deterministic mock disk history (fixed seed → identical across runs) as a
    /// bounded random walk around the comp's ~14 % base, so the area chart looks alive without a sampler.</summary>
    private static double[] BuildMockDiskHistory() {
        var random = new Random(0x5104);
        var history = new double[WindowSeconds];
        var value = 14.0;
        for (var i = 0; i < history.Length; i++) {
            value += (random.NextDouble() - 0.5) * 14; // drift ±7 per step
            value = Math.Clamp(value, 2, 46);
            history[i] = value;
        }
        return history;
    }
}
