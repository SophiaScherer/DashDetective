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
/// Everything on the page is live and per-drive: the Partitions table (<see cref="VolumeProvider"/>), the
/// drive summary cards (<see cref="PhysicalDiskProvider"/> + <see cref="StorageComposer"/>), each card's
/// Read/Write and the Disk Activity chart + readouts from the page-local
/// <see cref="IPhysicalDiskThroughputSampler"/>, and each NVMe card's Temp from
/// <see cref="DiskTemperatureProvider"/> (refreshed on a slow sub-cadence of the throughput timer). Non-NVMe
/// drives show "—" for Temp (no readable SMART temperature without admin).
///
/// The drive cards double as the page's drive selector: the Disk Activity panel shows the <b>selected</b>
/// physical disk, not the <c>PhysicalDisk(_Total)</c> aggregate — that instance averages idle time across
/// every disk, so on a multi-disk machine one busy drive would read diluted under a title naming a single
/// drive. Every disk's history is kept warm, so switching drives shows that drive's real recent activity
/// rather than restarting the chart.
/// </summary>
public partial class StorageViewModel : ViewModelBase, IRefreshablePage, ILiveSamplingPage, IActivatablePage, IDisposable, IReorderablePage {
    /// <summary>Key this page's widget order is persisted under.</summary>
    public string PageKey => "storage";

    private IReadOnlyList<string> _widgetOrder = [];

    /// <summary>The widget ids in display order, bound two-way to the page's WidgetBoard.</summary>
    public IReadOnlyList<string> WidgetOrder {
        get => _widgetOrder;
        set {
            if (ReferenceEquals(_widgetOrder, value))
                return;
            _widgetOrder = value ?? [];
            OnPropertyChanged();
            WidgetOrderChanged?.Invoke();
        }
    }

    /// <summary>Raised when a drag changes the order, so the shell can persist it.</summary>
    public event Action? WidgetOrderChanged;

    // Temperature moves slowly and each read opens a drive handle, so refresh it only every N throughput ticks
    // (≈ every 15 s at the default cadence) rather than every tick.
    private const int TemperatureRefreshTicks = 15;

    // Held only to follow the Settings refresh interval: the tab reads no shared feed, but its Disk Activity
    // chart has to advance at the same rate as the charts on other pages to cover the same span of time.
    private readonly SystemMetricsService _service;
    private readonly HardwareProviders _providers;

    // Page-local per-disk sampler + its timer, and the disk-number → card / history / latest-sample maps the
    // tick updates. Histories are kept for every disk (not just the selected one) so switching drives shows
    // that drive's real last minute.
    private readonly IPhysicalDiskThroughputSampler _throughputSampler =
        IPhysicalDiskThroughputSampler.ForCurrentPlatform();
    private readonly DispatcherTimer _throughputTimer;
    private readonly SamplingGate _gate;
    private readonly Dictionary<int, DriveCard> _cardsByDisk = new();
    private readonly Dictionary<int, MetricHistory> _historiesByDisk = new();
    private readonly Dictionary<int, DiskThroughputSample> _latestByDisk = new();

    /// <summary>Physical disk number the Disk Activity panel is showing, or −1 before the drives load.</summary>
    private int _selectedDisk = -1;

    // Disk numbers that reported a temperature at load (NVMe drives) — the ones the slow poll re-reads.
    private readonly List<int> _temperatureDiskNumbers = new();
    private int _temperatureTickCounter;

    public StorageViewModel(SystemMetricsService service)
        : this(service, HardwareProviders.ForCurrentPlatform()) { }

    /// <summary>Test seam: the same page over an explicit provider set. The public ctor resolves the real
    /// one, so the shell still builds this exactly as before.</summary>
    internal StorageViewModel(SystemMetricsService service, HardwareProviders providers) {
        _providers = providers;

        _service = service;

        // Built FIRST, before anything that reads it: every load captures _gate.Token, so a load started
        // above this line would dereference a null field and its soft-fail would wipe the page. The gate
        // starts idle and fires no callback until a transition, so building it early costs nothing.
        _gate = new SamplingGate(ApplySampling);

        // Load the (static structural) drive + volume info off the UI thread; the surfaces fill in when ready.
        _ = LoadStorageAsync();

        // Drive the per-disk readouts and the Disk Activity surface from the page-local sampler, at the
        // Settings cadence so this chart covers the same span as the other pages'. The timer is not
        // started here: the gate runs it only while the page is on screen and the Live pill is on.
        _throughputTimer = new DispatcherTimer { Interval = service.Interval };
        _throughputTimer.Tick += OnThroughputTick;
        service.IntervalChanged += OnIntervalChanged;

        ApplyChartWindow();
    }

    private void OnIntervalChanged(TimeSpan interval) {
        _throughputTimer.Interval = interval;
        ApplyChartWindow();
    }

    /// <summary>Rewrites the chart caption and time axis for the current window. The buffer is a fixed slot
    /// count, so the span it covers is the refresh interval times that count — a caption that hardcoded
    /// "60 seconds" would be wrong at every cadence but the default.</summary>
    private void ApplyChartWindow() {
        DiskChartCaption = $"% Active time over {ChartWindow.Describe(WindowSeconds, _service.Interval)}";
        ChartRangeStart = ChartWindow.StartLabel(WindowSeconds, _service.Interval);
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

    /// <summary>Whether the drive picker's dropdown is open. Two-way bound to the toggle and the popup, and
    /// cleared by <see cref="SelectDrive"/> so choosing a drive closes it.</summary>
    [ObservableProperty] private bool _drivePickerOpen;

    /// <summary>Whether the machine has more than one drive to switch between. On a single-drive machine the
    /// panel just names the drive — a picker offering one choice is a dead end.</summary>
    [ObservableProperty] private bool _hasMultipleDrives;

    /// <summary>What the Disk Activity chart plots and over how long, restated whenever the Settings
    /// refresh interval moves the window.</summary>
    [ObservableProperty] private string _diskChartCaption = "";

    /// <summary>The oldest end of the chart's time axis, e.g. "−60s".</summary>
    [ObservableProperty] private string _chartRangeStart = "";

    /// <summary>The chart's cold-start line, cleared as soon as this disk has a trace to show. Starts set:
    /// no disk has a sample before the first tick.</summary>
    [ObservableProperty] private string _diskChartStatus = ChartStatus.Collecting;

    /// <summary>The Disk Activity chart's points ("x,y …") on the shared Sparkline's 0–100 axis.</summary>
    [ObservableProperty] private string _diskPoints = "";

    /// <summary>The "Active time" readout — the latest active-time sample (e.g. "31%").</summary>
    [ObservableProperty] private string _diskActive = "0%";

    /// <summary>The "Avg response" readout — the average disk transfer time in ms (e.g. "0.4 ms").</summary>
    [ObservableProperty] private string _diskResponse = "0 ms";

    /// <summary>The "Queue" readout — the average disk queue length / outstanding requests (e.g. "0.03").</summary>
    [ObservableProperty] private string _diskQueue = "0.00";

    /// <summary>Points the Disk Activity panel at a drive (single-select, like the Performance rail's
    /// <c>ResourceRow</c>) and redraws it at once from that disk's kept history, so the panel doesn't sit
    /// blank until the next tick. Closes the picker either way, so re-choosing the current drive still
    /// dismisses the dropdown.</summary>
    private void SelectDrive(DriveCard card) {
        DrivePickerOpen = false;
        if (ReferenceEquals(card, SelectedDrive))
            return;

        if (SelectedDrive is not null)
            SelectedDrive.IsSelected = false;
        SelectedDrive = card;
        card.IsSelected = true;
        _selectedDisk = card.DiskNumber;
        UpdateActivity();
    }

    /// <summary>The drive card whose disk the Disk Activity panel is showing, or null before the load.</summary>
    [ObservableProperty] private DriveCard? _selectedDrive;

    // ----- Reveal (a Performance disk row jumping here) -----

    /// <summary>Raised once a revealed drive has been selected, so the view can scroll its card into sight
    /// and flash it. UI-only; the card itself is already selected by then.</summary>
    public event Action? RevealRequested;

    // A reveal that lands before the async card load finds nothing to select, so the disk number waits here
    // and SelectDefaultDrive consumes it — the same pending-slot shape ToolkitViewModel.Reveal uses.
    private int? _pendingReveal;

    /// <summary>Points the page at a physical disk, selecting its card and asking the view to reveal it.
    /// Held until the drives finish loading if it arrives first. An unknown disk number is ignored rather
    /// than clearing the current selection.</summary>
    public void Reveal(int diskNumber) {
        if (_cardsByDisk.TryGetValue(diskNumber, out var card)) {
            SelectDrive(card);
            RevealRequested?.Invoke();
            return;
        }

        _pendingReveal = diskNumber;
    }

    /// <summary>Redraws the Disk Activity surface (chart, Active time, Avg response, Queue) from the selected
    /// disk's latest sample and kept history. Shows neutral placeholders when that disk has no reading —
    /// a drive the PDH counters don't report, or before the first tick.</summary>
    private void UpdateActivity() {
        if (!_historiesByDisk.TryGetValue(_selectedDisk, out var history)) {
            DiskPoints = "";
            DiskChartStatus = ChartStatus.Collecting;
            DiskActive = "—";
            DiskResponse = "—";
            DiskQueue = "—";
            return;
        }

        DiskPoints = history.Points(100);
        DiskChartStatus = ChartStatus.For(history);
        if (!_latestByDisk.TryGetValue(_selectedDisk, out var sample)) {
            DiskActive = "—";
            DiskResponse = "—";
            DiskQueue = "—";
            return;
        }

        DiskActive = Math.Round(sample.ActivePercent).ToString("0", CultureInfo.InvariantCulture) + "%";
        DiskResponse = FormatResponse(sample.ResponseSeconds);
        DiskQueue = sample.QueueLength.ToString("0.00", CultureInfo.InvariantCulture);
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
    /// Toolbar Refresh for the Storage tab: an immediate re-sample of the per-disk counters (so the readouts
    /// and Disk Activity surface update once even while paused) plus a re-read of the drive + volume info.
    /// Drives the shell's Refresh action.
    /// </summary>
    public void Refresh() {
        UpdateThroughput();
        UpdateTemperatures();
        _ = LoadStorageAsync();
    }

    /// <summary>
    /// Reads the physical disks and volumes once (off the UI thread) and rebuilds both structural surfaces:
    /// the drive summary cards (composed per disk) and the Partitions table (lettered volumes first, then
    /// unlettered Recovery/EFI). Both providers soft-fail to empty lists, so any failure just clears the
    /// surfaces rather than faulting the task.
    /// </summary>
    /// <summary>Internal rather than private so a test can await the read the ctor fires and forgets.</summary>
    internal async Task LoadStorageAsync() {
        // The token is read BEFORE the await, not after: once sampling stops the gate hands out
        // CancellationToken.None, so a lazy read would never observe the cancellation it checks for.
        var token = _gate.Token;
        try {
            var disksTask = _providers.Disks.GetAsync();
            var volumesTask = _providers.Volumes.GetAsync();
            await Task.WhenAll(disksTask, volumesTask);
            token.ThrowIfCancellationRequested();
            var disks = disksTask.Result;
            var volumes = volumesTask.Result;

            // A reload rebuilds every card, so remember which disk was on show and re-select it below.
            var previousDisk = _selectedDisk;

            Drives.Clear();
            _cardsByDisk.Clear();
            _temperatureDiskNumbers.Clear();
            SelectedDrive = null;
            foreach (var data in StorageComposer.Compose(disks, volumes)) {
                var card = ToDriveCard(data, SelectDrive);
                Drives.Add(card);
                _cardsByDisk[data.DiskNumber] = card;
                if (data.TemperatureCelsius.HasValue)
                    _temperatureDiskNumbers.Add(data.DiskNumber);
            }

            Partitions.Clear();
            // Lettered volumes first and in letter order (Windows), then mounted ones shallowest-first
            // (Linux). Only one of the two keys is ever populated, so neither platform sees the other's.
            foreach (var volume in volumes
                         .OrderByDescending(v => v.DriveLetter.HasValue)
                         .ThenBy(v => v.DriveLetter)
                         .ThenBy(v => (v.MountPoint ?? "").Length)
                         .ThenBy(v => v.MountPoint, StringComparer.Ordinal))
                Partitions.Add(ToPartitionRow(volume));

            HasMultipleDrives = Drives.Count > 1;
            SelectDefaultDrive(volumes, previousDisk);

            // Seed the new cards' Read/Write once so they don't sit on "—" until the next timer tick.
            UpdateThroughput();
        } catch when (token.IsCancellationRequested) {
            // Left mid-read: cancelled, or failed once the user had already gone. Either way the wipe
            // below must NOT run — it would blank every drive card and the partitions table they come
            // back to.
        } catch {
            Drives.Clear();
            _cardsByDisk.Clear();
            _temperatureDiskNumbers.Clear();
            Partitions.Clear();
            SelectedDrive = null;
            HasMultipleDrives = false;
            _selectedDisk = -1;
            UpdateActivity();
        }
    }

    /// <summary>Selects the drive the Disk Activity panel shows after a (re)load: whatever the user had
    /// chosen if that disk is still present — a toolbar Refresh rebuilds the cards, and must not silently
    /// move the panel off the drive being watched — else the one hosting Windows, since that is the drive the
    /// page previously named in its title. Falls back to the first card when neither resolves.</summary>
    private void SelectDefaultDrive(IReadOnlyList<VolumeInfo> volumes, int previousDisk) {
        if (Drives.Count == 0)
            return;

        // A reveal that arrived before the cards existed outranks both: it is what the user just asked for.
        if (_pendingReveal is { } pending) {
            _pendingReveal = null;
            if (_cardsByDisk.TryGetValue(pending, out var revealed)) {
                SelectDrive(revealed);
                RevealRequested?.Invoke();
                return;
            }
        }

        if (_cardsByDisk.TryGetValue(previousDisk, out var previous)) {
            SelectDrive(previous);
            return;
        }

        var systemDisk = SystemVolume.FindDiskNumber(volumes);

        SelectDrive(systemDisk is { } disk && _cardsByDisk.TryGetValue(disk, out var card) ? card : Drives[0]);
    }

    private void OnThroughputTick(object? sender, EventArgs e) {
        UpdateThroughput();
        // Poll temperature far less often than Read/Write — it barely moves and each read hits the drive.
        if (++_temperatureTickCounter >= TemperatureRefreshTicks) {
            _temperatureTickCounter = 0;
            UpdateTemperatures();
        }
    }

    /// <summary>Re-reads each NVMe drive's temperature and updates its card in place. A transient read miss
    /// leaves the last shown value untouched.</summary>
    private void UpdateTemperatures() {
        foreach (var diskNumber in _temperatureDiskNumbers)
            if (_providers.DiskTemperature.ReadCelsius(diskNumber) is double celsius
                && _cardsByDisk.TryGetValue(diskNumber, out var card))
                card.Temp = DriveTemperatureFormatter.Format(celsius);
    }

    /// <summary>Samples every disk once and updates each card's Read/Write readouts in place (bytes/sec
    /// formatted like "48 MB/s"), appending each disk's active time to its own rolling history so any drive
    /// the user switches to already has a minute behind it. Ends by redrawing the Disk Activity surface for
    /// the selected disk. Disks without a current reading are left unchanged.</summary>
    private void UpdateThroughput() {
        foreach (var sample in _throughputSampler.Sample()) {
            if (!_cardsByDisk.TryGetValue(sample.DiskNumber, out var card))
                continue;
            card.Read = FormatRate(sample.ReadBytesPerSec);
            card.Write = FormatRate(sample.WriteBytesPerSec);

            if (!_historiesByDisk.TryGetValue(sample.DiskNumber, out var history))
                _historiesByDisk[sample.DiskNumber] = history = new MetricHistory(WindowSeconds);
            history.Push(sample.ActivePercent);
            _latestByDisk[sample.DiskNumber] = sample;
        }

        UpdateActivity();
    }

    /// <summary>Formats a byte-per-second rate as "&lt;size&gt;/s" (e.g. "48 MB/s"), reusing the shared
    /// binary size formatter.</summary>
    private static string FormatRate(double bytesPerSec) =>
        FileSizeFormatter.Format((long)bytesPerSec) + "/s";

    /// <summary>Maps composed drive data to a summary card: the health pill + usage-bar brushes are the
    /// fixed semantic colours; used/free are formatted (binary units, like the Dashboard). Read/Write are
    /// seeded by the throughput sampler; Temp shows the NVMe reading or "—" when none is available. The card
    /// carries its disk number and a select command, so it also acts as the Disk Activity panel's selector.</summary>
    private static DriveCard ToDriveCard(DriveCardData data, Action<DriveCard> onSelected) {
        var healthy = data.Health == DriveHealth.Healthy;
        return new DriveCard(data.DiskNumber, onSelected) {
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
            Temp = DriveTemperatureFormatter.Format(data.TemperatureCelsius),
        };
    }

    /// <summary>Usage-bar colour, warming as the drive fills: amber when in caution or ≥ 85 % full, blue in
    /// the mid range, green when comfortably free — reproducing the design comp's per-drive tints.</summary>
    private static IBrush BarBrushFor(DriveCardData data) {
        if (data.Health == DriveHealth.Caution || data.UsagePercent >= 85)
            return BarAmber;
        return data.UsagePercent >= 65 ? BarBlue : BarGreen;
    }

    /// <summary>Maps one volume to a display row: the drive letter or mount point ("C:", "/boot/efi", "—"
    /// for an unlettered Recovery partition), the formatted capacity/free (binary units, like the
    /// Dashboard), and "—" for a missing file system. An unlabelled but reachable volume falls back to
    /// "Local Disk" (Explorer's convention, matching <see cref="StorageComposer"/>'s cards).</summary>
    private static PartitionRow ToPartitionRow(VolumeInfo volume) {
        var mounted = volume.DriveLetter.HasValue || !string.IsNullOrEmpty(volume.MountPoint);

        return new PartitionRow {
            Vol = volume.DriveLetter is { } letter ? $"{letter}:"
                : mounted ? volume.MountPoint
                : "—",
            Label = !string.IsNullOrEmpty(volume.Label) ? volume.Label
                : mounted ? "Local Disk"
                : "—",
            FileSystem = string.IsNullOrEmpty(volume.FileSystem) ? "—" : volume.FileSystem,
            Type = PartitionTypeFormatter.Format(volume.GptType, mounted),
            Capacity = FileSizeFormatter.Format((long)volume.SizeBytes),
            Free = FileSizeFormatter.Format((long)volume.FreeBytes),
        };
    }

    /// <summary>Pauses/resumes the tab's sampling for the shell's Live pill.</summary>
    public void SetLive(bool live) => _gate.Live = live;

    /// <summary>Starts/stops the tab's sampling as it comes on and off screen.</summary>
    public void SetActive(bool active) => _gate.Active = active;

    /// <summary>Runs or halts the per-disk throughput timer, which drives every live surface on the page —
    /// the gate's composed answer, so it reflects the Live pill and the tab's visibility at once.</summary>
    private void ApplySampling(bool running) {
        if (running)
            _throughputTimer.Start();
        else
            _throughputTimer.Stop();
    }

    /// <summary>Tears down the page-local throughput timer + sampler. Safe to call more than once.</summary>
    public void Dispose() {
        _gate.Dispose();
        _service.IntervalChanged -= OnIntervalChanged;
        _throughputTimer.Stop();
        _throughputTimer.Tick -= OnThroughputTick;
        _throughputSampler.Dispose();
    }
}
