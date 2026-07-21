using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using DashDetective.Services.SystemMetrics;
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
/// The live technical pass is landing feature-by-feature: this view model now takes the shared
/// <see cref="SystemMetricsService"/> and opts into the shell's <see cref="IRefreshablePage"/> /
/// <see cref="ILiveSamplingPage"/> routing. The surfaces below are still seeded with <b>static mock
/// data</b> and are replaced one per phase (Disk Activity → Partitions → drive cards).
/// </summary>
public partial class StorageViewModel : ViewModelBase, IRefreshablePage, ILiveSamplingPage, IDisposable {
    // The shared metric hub (CPU/Memory/GPU/Storage/Network). The Disk Activity surface subscribes to its
    // storage feed in a later phase; held here now so the ctor injection and shell routing are in place.
    private readonly SystemMetricsService _service;
    private readonly IDisposable _storageSubscription;

    public StorageViewModel(SystemMetricsService service) {
        _service = service;

        // Feed the Disk Activity surface from the shared storage feed. The subscription immediately replays
        // the latest cached sample, seeding the chart with real data on the first frame.
        _storageSubscription = service.SubscribeStorage(OnStorage, OnStorageFailed);
    }
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

    // The rolling active-time history the Disk Activity chart draws (Task Manager's disk "Active time",
    // 0–100 %). The samplers are shared across pages; this history is this tab's own, like the Dashboard's.
    private readonly double[] _diskHistory = new double[WindowSeconds];

    /// <summary>The Disk Activity chart's points ("x,y …") on the shared Sparkline's 0–100 axis.</summary>
    [ObservableProperty] private string _diskPoints = "";

    /// <summary>The "Active time" readout — the latest active-time sample (e.g. "31%").</summary>
    [ObservableProperty] private string _diskActive = "0%";

    /// <summary>The "Avg response" readout — the average disk transfer time in ms (e.g. "0.4 ms").</summary>
    [ObservableProperty] private string _diskResponse = "0 ms";

    /// <summary>The "Queue" readout — the average disk queue length / outstanding requests (e.g. "0.03").</summary>
    [ObservableProperty] private string _diskQueue = "0.00";

    /// <summary>Storage subscription callback: append the active time to the history, then refresh the
    /// Disk Activity surface (chart, Active time, Avg response and Queue readouts).</summary>
    private void OnStorage(StorageSample sample) {
        MetricChannel.PushHistory(_diskHistory, sample.ActivePercent);
        DiskActive = Math.Round(sample.ActivePercent).ToString("0", CultureInfo.InvariantCulture) + "%";
        DiskResponse = FormatResponse(sample.ResponseSeconds);
        DiskQueue = sample.QueueLength.ToString("0.00", CultureInfo.InvariantCulture);
        DiskPoints = SparklinePoints.Build(_diskHistory, 100);
    }

    /// <summary>Sampler-failure handler for the Disk Activity surface: shows neutral placeholders.</summary>
    private void OnStorageFailed() {
        DiskActive = "—";
        DiskResponse = "—";
        DiskQueue = "—";
    }

    /// <summary>Formats the average transfer time (seconds) as milliseconds, e.g. "0.4 ms".</summary>
    private static string FormatResponse(double seconds) =>
        (seconds * 1000).ToString("0.0", CultureInfo.InvariantCulture) + " ms";

    /// <summary>
    /// Toolbar Refresh for the Storage tab: forces an immediate re-sample of the shared metrics (so the
    /// Disk Activity surface updates once even while paused). Re-reading the static drive/partition
    /// providers is added when those surfaces go live. Drives the shell's Refresh action.
    /// </summary>
    public void Refresh() => _service.RefreshAll();

    /// <summary>
    /// Pauses/resumes the tab's own live sampling for the shell's Live pill. The shared metric feed is
    /// paused separately by the shell via <see cref="SystemMetricsService.Pause"/>; this hook is for the
    /// page-local per-disk throughput timer added in a later phase, so it is a no-op for now.
    /// </summary>
    public void SetLive(bool live) { }

    /// <summary>Unsubscribes from the shared storage feed. The samplers are owned (and disposed) by the
    /// shared service; page-local timers are released here as later phases add them. Safe to call twice.</summary>
    public void Dispose() => _storageSubscription.Dispose();
}
