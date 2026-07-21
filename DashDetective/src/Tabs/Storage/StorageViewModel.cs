using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using DashDetective.Services.SystemMetrics;
using DashDetective.Shared;
using DashDetective.Shared.Charts;
using DashDetective.Tabs.FileExplorer;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace DashDetective.Tabs.Storage;

/// <summary>
/// The Storage tab: a read-only drives/health view per the design comp — three drive summary cards over
/// a Partitions table and a Disk Activity chart. Page-scrolls as a whole like the Dashboard/Network (not
/// <see cref="ISelfScrollingPage"/>).
///
/// The live technical pass is landing feature-by-feature: this view model takes the shared
/// <see cref="SystemMetricsService"/> and opts into the shell's <see cref="IRefreshablePage"/> /
/// <see cref="ILiveSamplingPage"/> routing. Live now: the Disk Activity chart + readouts (shared storage
/// feed), the Partitions table (<see cref="VolumeProvider"/>) and the drive summary cards
/// (<see cref="PhysicalDiskProvider"/> + <see cref="StorageComposer"/>), and each card's Read/Write from the
/// page-local <see cref="PhysicalDiskThroughputSampler"/>. Drive temperature is deferred (SMART), so cards
/// show "—" for Temp.
/// </summary>
public partial class StorageViewModel : ViewModelBase, IRefreshablePage, ILiveSamplingPage, IDisposable {
    // The shared metric hub (CPU/Memory/GPU/Storage/Network). The Disk Activity surface subscribes to its
    // storage feed in a later phase; held here now so the ctor injection and shell routing are in place.
    // Interval of the page-local per-disk throughput timer. Like the Network tab's own coarse timers, this
    // is deliberately NOT retimed by the Settings refresh interval (which scales only the shared feeds).
    private static readonly TimeSpan ThroughputInterval = TimeSpan.FromSeconds(1);

    private readonly SystemMetricsService _service;
    private readonly IDisposable _storageSubscription;

    // Page-local per-disk read/write sampler + its timer, and the disk-number → card map the tick updates.
    private readonly PhysicalDiskThroughputSampler _throughputSampler = new();
    private readonly DispatcherTimer _throughputTimer;
    private readonly Dictionary<int, DriveCard> _cardsByDisk = new();

    public StorageViewModel(SystemMetricsService service) {
        _service = service;

        // Feed the Disk Activity surface from the shared storage feed. The subscription immediately replays
        // the latest cached sample, seeding the chart with real data on the first frame.
        _storageSubscription = service.SubscribeStorage(OnStorage, OnStorageFailed);

        // Load the (static structural) drive + volume info off the UI thread; the surfaces fill in when ready.
        _ = LoadStorageAsync();

        // Drive the per-disk Read/Write readouts from the page-local sampler on their own 1 Hz timer.
        _throughputTimer = new DispatcherTimer { Interval = ThroughputInterval };
        _throughputTimer.Tick += OnThroughputTick;
        _throughputTimer.Start();
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

    /// <summary>The drive summary cards shown in the top row (one per physical disk). Composed from
    /// <see cref="PhysicalDiskProvider"/> + <see cref="VolumeProvider"/> at startup and rebuilt on Refresh;
    /// empty until the first load.</summary>
    public ObservableCollection<DriveCard> Drives { get; } = new();

    /// <summary>The partition rows shown in the Partitions table (one per volume, lettered or not). Loaded
    /// from <see cref="VolumeProvider"/> at startup and rebuilt on Refresh; empty until the first load.</summary>
    public ObservableCollection<PartitionRow> Partitions { get; } = new();

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

    /// <summary>Drive summary rows for the shell's system report: one line per drive with its capacity
    /// split and health, read from the current on-screen cards (no re-sampling). Matches the Hardware /
    /// Network report sections.</summary>
    public IReadOnlyList<(string Key, string Value)> GetReportRows() {
        var rows = new List<(string Key, string Value)>(Drives.Count);
        foreach (var drive in Drives)
            rows.Add((drive.Name, $"{drive.Used} used / {drive.Free} free · {drive.Health}"));
        return rows;
    }

    /// <summary>
    /// Toolbar Refresh for the Storage tab: forces an immediate re-sample of the shared metrics (so the
    /// Disk Activity surface updates once even while paused) and re-reads the drive + volume info. Drives
    /// the shell's Refresh action.
    /// </summary>
    public void Refresh() {
        _service.RefreshAll();
        _ = LoadStorageAsync();
    }

    /// <summary>
    /// Reads the physical disks and volumes once (off the UI thread) and rebuilds both structural surfaces:
    /// the drive summary cards (composed per disk) and the Partitions table (lettered volumes first, then
    /// unlettered Recovery/EFI). Both providers soft-fail to empty lists, so any failure just clears the
    /// surfaces rather than faulting the task.
    /// </summary>
    private async Task LoadStorageAsync() {
        try {
            var disksTask = PhysicalDiskProvider.GetAsync();
            var volumesTask = VolumeProvider.GetAsync();
            await Task.WhenAll(disksTask, volumesTask);
            var disks = disksTask.Result;
            var volumes = volumesTask.Result;

            Drives.Clear();
            _cardsByDisk.Clear();
            foreach (var data in StorageComposer.Compose(disks, volumes)) {
                var card = ToDriveCard(data);
                Drives.Add(card);
                _cardsByDisk[data.DiskNumber] = card;
            }

            Partitions.Clear();
            foreach (var volume in volumes
                         .OrderByDescending(v => v.DriveLetter.HasValue)
                         .ThenBy(v => v.DriveLetter))
                Partitions.Add(ToPartitionRow(volume));

            // Seed the new cards' Read/Write once so they don't sit on "—" until the next timer tick.
            UpdateThroughput();
        } catch {
            Drives.Clear();
            _cardsByDisk.Clear();
            Partitions.Clear();
        }
    }

    private void OnThroughputTick(object? sender, EventArgs e) => UpdateThroughput();

    /// <summary>Samples per-disk throughput and updates each card's Read/Write readouts in place (bytes/sec
    /// formatted like "48 MB/s"). Disks without a current reading are left unchanged.</summary>
    private void UpdateThroughput() {
        foreach (var sample in _throughputSampler.Sample()) {
            if (!_cardsByDisk.TryGetValue(sample.DiskNumber, out var card))
                continue;
            card.Read = FormatRate(sample.ReadBytesPerSec);
            card.Write = FormatRate(sample.WriteBytesPerSec);
        }
    }

    /// <summary>Formats a byte-per-second rate as "&lt;size&gt;/s" (e.g. "48 MB/s"), reusing the shared
    /// binary size formatter.</summary>
    private static string FormatRate(double bytesPerSec) =>
        FileSizeFormatter.Format((long)bytesPerSec) + "/s";

    /// <summary>Maps composed drive data to a summary card: the health pill + usage-bar brushes are the
    /// fixed semantic colours; used/free are formatted (binary units, like the Dashboard). Read/Write are
    /// placeholders until the live-throughput phase; Temp is "—" (SMART temperature is deferred).</summary>
    private static DriveCard ToDriveCard(DriveCardData data) {
        var healthy = data.Health == DriveHealth.Healthy;
        return new DriveCard {
            Name = data.Name,
            Model = data.Model,
            Health = healthy ? "Healthy" : "Caution",
            HealthForeground = healthy ? HealthyFg : CautionFg,
            HealthBackground = healthy ? HealthyBg : CautionBg,
            UsagePercent = data.UsagePercent,
            BarBrush = BarBrushFor(data),
            Used = FileSizeFormatter.Format(data.UsedBytes),
            Free = FileSizeFormatter.Format(data.FreeBytes),
            Read = "—",
            Write = "—",
            Temp = "—",
        };
    }

    /// <summary>Usage-bar colour, warming as the drive fills: amber when in caution or ≥ 85 % full, blue in
    /// the mid range, green when comfortably free — reproducing the design comp's per-drive tints.</summary>
    private static IBrush BarBrushFor(DriveCardData data) {
        if (data.Health == DriveHealth.Caution || data.UsagePercent >= 85)
            return BarAmber;
        return data.UsagePercent >= 65 ? BarBlue : BarGreen;
    }

    /// <summary>Maps one volume to a display row: "C:"/"—" for the letter, the formatted capacity/free
    /// (binary units, like the Dashboard), and "—" for a missing label/file system.</summary>
    private static PartitionRow ToPartitionRow(VolumeInfo volume) => new() {
        Vol = volume.DriveLetter is { } letter ? $"{letter}:" : "—",
        Label = string.IsNullOrEmpty(volume.Label) ? "—" : volume.Label,
        FileSystem = string.IsNullOrEmpty(volume.FileSystem) ? "—" : volume.FileSystem,
        Capacity = FileSizeFormatter.Format((long)volume.SizeBytes),
        Free = FileSizeFormatter.Format((long)volume.FreeBytes),
    };

    /// <summary>
    /// Pauses/resumes the tab's own per-disk throughput timer for the shell's Live pill. The shared metric
    /// feed (the Disk Activity surface) is paused separately by the shell via
    /// <see cref="SystemMetricsService.Pause"/>.
    /// </summary>
    public void SetLive(bool live) {
        if (live)
            _throughputTimer.Start();
        else
            _throughputTimer.Stop();
    }

    /// <summary>Unsubscribes from the shared storage feed and tears down the page-local throughput timer +
    /// sampler (the shared feed's samplers are owned/disposed by the service). Safe to call more than once.</summary>
    public void Dispose() {
        _storageSubscription.Dispose();
        _throughputTimer.Stop();
        _throughputTimer.Tick -= OnThroughputTick;
        _throughputSampler.Dispose();
    }
}
