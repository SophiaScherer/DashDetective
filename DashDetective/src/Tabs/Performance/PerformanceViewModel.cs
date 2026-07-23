using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashDetective.Services.Network;
using DashDetective.Services.SystemMetrics;
using DashDetective.Shared;
using DashDetective.Shared.Charts;
using DashDetective.Tabs.Dashboard;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading.Tasks;

namespace DashDetective.Tabs.Performance;

/// <summary>
/// The Performance tab: a Task-Manager-style live resource drill-down. A left resource-selector rail
/// swaps a right detail pane (one large utilization chart + a stat-tile strip).
///
/// Live sampling subscribes to the shared <see cref="SystemMetricsService"/>; each metric keeps its own
/// <c>double[60]</c> rolling history, rebuilds the shared <c>Sparkline</c> via <see cref="SparklinePoints"/>,
/// and pushes results into the selected resource's <see cref="ResourceRow"/>.
///
/// All five resources are wired live: <b>CPU</b>, <b>Memory</b>, <b>Disk</b>, <b>GPU</b>, <b>Ethernet</b>.
/// Implements <see cref="IRefreshablePage"/> (toolbar Refresh), <see cref="ILiveSamplingPage"/>
/// (toolbar Live/Pause) and <see cref="IDisposable"/>; <see cref="ISelfScrollingPage"/> keeps the shell
/// hosting it in the bounded, non-scrolling container (it manages its own panes, like File Explorer).
/// </summary>
public partial class PerformanceViewModel : ViewModelBase,
        IRefreshablePage, ILiveSamplingPage, ISelfScrollingPage, IDisposable {
    /// <summary>Width of every rolling metric history, in seconds (one sample per second).</summary>
    private const int WindowSeconds = 60;

    /// <summary>Floor for the network chart's auto-scaled axis, in Mbps: keeps an idle graph pinned flat
    /// near the bottom (rather than amplifying counter noise) and avoids a zero span. Mirrors the
    /// Dashboard's network scale floor.</summary>
    private const double MinNetworkScaleMbps = 1.0;

    // Fixed semantic per-metric legend colours (theme/accent-independent by design), matching the design
    // comp's palette — parsed like MainWindowViewModel's live dots.
    private static IBrush Brush(string hex) => new SolidColorBrush(Color.Parse(hex));

    /// <summary>The resource rows shown in the left rail, in display order (filtered by <see cref="ShowAllDevices"/>).</summary>
    public ObservableCollection<ResourceRow> Resources { get; } = new();

    /// <summary>The currently selected resource, whose detail the right pane shows.</summary>
    [ObservableProperty] private ResourceRow _selectedResource = null!;

    /// <summary>Whether the rail shows every detected instance ("All devices") or just the primary of each kind
    /// ("Primary"). Persisted by the shell; today it only changes the disk rows (the sole multi-instance
    /// category), but the filtering is instance-count-agnostic.</summary>
    [ObservableProperty] private bool _showAllDevices;

    /// <summary>Raised when <see cref="ShowAllDevices"/> changes, so the shell can persist the choice.</summary>
    public event Action? ScopeChanged;

    /// <summary>Raised when a resource's Overall/Detailed view changes, so the shell can persist the choice.</summary>
    public event Action? DetailChanged;

    /// <summary>Whether the GPU resource shows its per-engine "Detailed" charts. Persisted by the shell.</summary>
    public bool GpuDetailedView {
        get => _gpuRow.IsDetailed;
        set => _gpuRow.IsDetailed = value;
    }

    /// <summary>Whether the CPU resource shows its per-logical-processor "Detailed" charts. Persisted by the shell.</summary>
    public bool CpuDetailedView {
        get => _cpuRow.IsDetailed;
        set => _cpuRow.IsDetailed = value;
    }

    private readonly SystemMetricsService _service;
    private readonly IDisposable[] _subscriptions;

    // ---- CPU (live) ----
    private readonly double[] _cpuHistory = new double[WindowSeconds];
    private readonly ResourceRow _cpuRow;
    private readonly StatTile _cpuUtilTile;
    private readonly StatTile _cpuProcessesTile;
    private readonly StatTile _cpuUptimeTile;

    // CPU per-logical-processor "Detailed" view: a page-local per-core sampler drives one mini chart per
    // logical processor, built lazily on the first sample (its instances name and count the charts) and updated
    // on the disk timer. Capped so an extreme core count stays responsive.
    private const int MaxLogicalProcessorCharts = 64;
    private readonly LogicalProcessorSampler _cpuSampler = new();
    private readonly List<CoreChart> _cpuCores = new();
    private readonly Dictionary<string, CoreChart> _cpuCoresByInstance = new();

    // ---- Memory (live) ----
    private readonly double[] _memoryHistory = new double[WindowSeconds];
    private readonly ResourceRow _memoryRow;
    private readonly StatTile _memInUseTile;
    private readonly StatTile _memAvailableTile;
    private readonly StatTile _memCommittedTile;

    // ---- Disks (live, one row per physical disk) ----
    // Rows are built from the DeviceInventory; each disk's active-time / read / write / response come from the
    // page-local per-disk sampler (the shared storage feed is _Total-only) on its own timer, keyed by disk
    // number. This is the multi-instance category the Primary/All toggle expands or collapses.
    private readonly List<DiskResource> _disks = new();
    private readonly Dictionary<int, DiskResource> _disksByNumber = new();
    private readonly PhysicalDiskThroughputSampler _throughputSampler = new();
    private readonly DispatcherTimer _throughputTimer;

    // ---- GPU (live) ----
    private readonly double[] _gpuHistory = new double[WindowSeconds];
    private readonly ResourceRow _gpuRow;
    private readonly StatTile _gpu3dTile;

    // GPU per-engine "Detailed" view: a page-local engine sampler (the shared feed carries only the overall
    // busiest-engine figure) drives one mini chart per engine type on the disk timer.
    private readonly GpuUsageSampler _gpuEngineSampler = new();
    private readonly List<EngineChart> _gpuEngines = new();
    private readonly Dictionary<string, EngineChart> _gpuEnginesByBase = new(StringComparer.Ordinal);

    // ---- Ethernet / network (live) ----
    private readonly double[] _downHistory = new double[WindowSeconds];
    private readonly NetworkInterface? _networkInterface = NetworkUsageSampler.SelectPrimary();
    private readonly ResourceRow _networkRow;
    private readonly StatTile _netReceiveTile;
    private readonly StatTile _netSendTile;
    private readonly StatTile _netErrorsTile;

    public PerformanceViewModel(SystemMetricsService service) {
        _service = service;

        // Build the stat tiles first, then the resource rows (their initial charts come from the all-zero
        // histories), then subscribe to the shared metrics.

        // CPU — live. Tiles: Utilization / Processes / Up time update every tick; Speed is blanked to
        // "—" (no reliable current-clock source, and the base clock already appears in the sub-label).
        _cpuUtilTile = new StatTile("Utilization", "0 %");
        _cpuProcessesTile = new StatTile("Processes", "0");
        _cpuUptimeTile = new StatTile("Up time", "0m");

        // Memory — live. Tiles: In use / Available / Committed update every tick; Cached is blanked to
        // "—" (no reliable source without adding a PDH counter to the pure-Win32 memory sampler).
        _memInUseTile = new StatTile("In use", "0 GB");
        _memAvailableTile = new StatTile("Available", "0 GB");
        _memCommittedTile = new StatTile("Committed", "0 / 0 GB");

        // GPU — live. Rail value + chart show the busiest GPU engine's utilization; 3D tile mirrors it.
        _gpu3dTile = new StatTile("3D", "0 %");

        // Ethernet / network — live. Rail value + chart show the primary adapter's receive throughput;
        // tiles show Receive / Send / Link / Errors.
        _netReceiveTile = new StatTile("Receive", "0 Mbps");
        _netSendTile = new StatTile("Send", "0 Mbps");
        _netErrorsTile = new StatTile("Errors", "0");

        _cpuRow = new ResourceRow("CPU", "", "", "0", "%", Brush("#4cc2ff"),
                                  SparklinePoints.Build(_cpuHistory, 100),
                                  new[] {
                                      _cpuUtilTile, new StatTile("Speed", "—"),
                                      _cpuProcessesTile, _cpuUptimeTile,
                                  }, Select);

        _memoryRow = new ResourceRow("Memory", "", "", "0", "%", Brush("#c58fff"),
                                     SparklinePoints.Build(_memoryHistory, 100),
                                     new[] {
                                         _memInUseTile, _memAvailableTile,
                                         new StatTile("Cached", "—"), _memCommittedTile,
                                     }, Select);

        // GPU — VRAM / Temp / Power are blanked to "—": no reliable standard Windows source (GPU
        // temperature is already deferred out of scope in the project).
        _gpuRow = new ResourceRow("GPU", "", "", "0", "%", Brush("#6ccb5f"),
                                  SparklinePoints.Build(_gpuHistory, 100),
                                  new[] {
                                      _gpu3dTile, new StatTile("VRAM", "—"),
                                      new StatTile("Temp", "—"), new StatTile("Power", "—"),
                                  }, Select);

        // The row is named after the real primary adapter (e.g. "Ethernet", "Wi-Fi"), and Link speed is
        // read once at construction (it rarely changes).
        var adapterName = string.IsNullOrWhiteSpace(service.NetworkAdapterName) ? "Ethernet" : service.NetworkAdapterName;
        var linkSpeed = FormatLinkSpeed(_networkInterface?.Speed ?? 0);
        _networkRow = new ResourceRow(adapterName, "", "", "0", "Mbps", Brush("#4cc2ff"),
                                      SparklinePoints.Build(_downHistory, MinNetworkScaleMbps),
                                      new[] {
                                          _netReceiveTile, _netSendTile,
                                          new StatTile("Link", linkSpeed), _netErrorsTile,
                                      }, Select);

        // The GPU engine and CPU core charts are discovered on their first sample. Forward each row's
        // Overall/Detailed flip to DetailChanged (the shell subscribes after seeding, so the seed doesn't
        // trigger a save).
        _gpuRow.PropertyChanged += OnResourceDetailChanged;
        _cpuRow.PropertyChanged += OnResourceDetailChanged;

        // Populate the rail (CPU, Memory, [disks], GPU, Network) and select the first row. Disk rows are added
        // once the inventory load completes.
        RebuildResources();

        // Subscribe to the shared metrics; each subscription immediately replays the latest cached sample,
        // seeding the surfaces with real data on the first frame. Disks are driven by the page-local per-disk
        // sampler instead — the shared storage feed reports only the _Total aggregate, not per drive.
        _subscriptions = new[] {
            service.SubscribeCpu(OnCpu, OnCpuFailed),
            service.SubscribeMemory(OnMemory, OnMemoryFailed),
            service.SubscribeGpu(OnGpu, OnGpuFailed),
            service.SubscribeNetwork(OnNetwork, OnNetworkFailed),
        };

        // Drive the per-disk rows from the page-local throughput sampler on its own 1 Hz timer.
        _throughputTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _throughputTimer.Tick += OnThroughputTick;
        _throughputTimer.Start();

        // Load static hardware info off the UI thread; the sub/spec labels fill in when ready. The device
        // inventory enumerates the physical disks into their own rows.
        _ = LoadCpuInfoAsync();
        _ = LoadMemoryInfoAsync();
        _ = LoadGpuInfoAsync();
        _ = LoadNetworkInfoAsync();
        _ = LoadInventoryAsync();
    }

    /// <summary>Rebuilds the rail from the current rows, filtered by <see cref="ShowAllDevices"/>: the single
    /// categories always show, while the disks collapse to the primary drive ("Primary") or expand to every
    /// drive ("All devices"). Keeps the current selection when it survives the filter, else selects the first
    /// row.</summary>
    private void RebuildResources() {
        var previous = SelectedResource;

        Resources.Clear();
        Resources.Add(_cpuRow);
        Resources.Add(_memoryRow);
        if (ShowAllDevices)
            foreach (var disk in _disks)
                Resources.Add(disk.Row);
        else if (_disks.Count > 0)
            Resources.Add(_disks[0].Row);
        Resources.Add(_gpuRow);
        Resources.Add(_networkRow);

        Select(previous is not null && Resources.Contains(previous) ? previous : Resources[0]);
    }

    partial void OnShowAllDevicesChanged(bool value) {
        RebuildResources();
        ScopeChanged?.Invoke();
    }

    /// <summary>Rail scope segments: "Primary" (one of each kind) and "All devices" (every instance).</summary>
    [RelayCommand] private void SelectPrimaryScope() => ShowAllDevices = false;
    [RelayCommand] private void SelectAllScope() => ShowAllDevices = true;

    /// <summary>Detail segments (shown only for resources that <see cref="ResourceRow.SupportsDetail"/>):
    /// "Overall" (one chart) and "Detailed" (per-subunit mini charts) on the selected resource.</summary>
    [RelayCommand]
    private void SelectOverallView() {
        if (SelectedResource is not null)
            SelectedResource.IsDetailed = false;
    }

    [RelayCommand]
    private void SelectDetailedView() {
        if (SelectedResource is not null)
            SelectedResource.IsDetailed = true;
    }

    /// <summary>Forwards a resource's Overall/Detailed flip to <see cref="DetailChanged"/> so the shell can
    /// persist it.</summary>
    private void OnResourceDetailChanged(object? sender, PropertyChangedEventArgs e) {
        if (e.PropertyName == nameof(ResourceRow.IsDetailed))
            DetailChanged?.Invoke();
    }

    /// <summary>Toolbar Refresh: an immediate re-sample of every live metric, even while paused, plus a
    /// re-enumeration of the physical disks.</summary>
    public void Refresh() {
        _service.RefreshAll();
        UpdateDisks();
        UpdateGpuEngines();
        UpdateCpuCores();
        _ = LoadCpuInfoAsync();
        _ = LoadMemoryInfoAsync();
        _ = LoadGpuInfoAsync();
        _ = LoadNetworkInfoAsync();
        _ = LoadInventoryAsync();
    }

    /// <summary>Pauses/resumes the page-local per-disk throughput timer for the shell's Live toggle. The shared
    /// CPU/Memory/GPU/Network feeds are paused separately by the shell via
    /// <see cref="SystemMetricsService.Pause"/>.</summary>
    public void SetLive(bool live) {
        if (live)
            _throughputTimer.Start();
        else
            _throughputTimer.Stop();
    }

    /// <summary>CPU subscription callback: append to the history, then refresh the utilization surfaces
    /// plus the live process count and uptime tiles (all keyed off the CPU tick).</summary>
    private void OnCpu(double value) {
        MetricChannel.PushHistory(_cpuHistory, value);
        UpdateCpu(value);
        UpdateCpuProcesses();
        UpdateCpuUptime();
    }

    /// <summary>Sampler-failure handler for the CPU channel: shows neutral placeholders.</summary>
    private void OnCpuFailed() {
        _cpuRow.ValueText = "—";
        _cpuUtilTile.Value = "—";
    }

    private void UpdateCpu(double value) {
        var rounded = Math.Round(value);
        _cpuRow.ValueText = rounded.ToString(CultureInfo.InvariantCulture);
        _cpuUtilTile.Value = $"{rounded.ToString(CultureInfo.InvariantCulture)} %";
        _cpuRow.Points = SparklinePoints.Build(_cpuHistory, 100);
    }

    /// <summary>Updates the live process count. <see cref="Process.GetProcesses"/> returns disposable
    /// handles, so they're released immediately after counting.</summary>
    private void UpdateCpuProcesses() {
        try {
            var processes = Process.GetProcesses();
            _cpuProcessesTile.Value = processes.Length.ToString(CultureInfo.InvariantCulture);
            foreach (var process in processes)
                process.Dispose();
        } catch {
            _cpuProcessesTile.Value = "—";
        }
    }

    /// <summary>Refreshes the uptime readout from the system tick count. <see cref="Environment.TickCount64"/>
    /// is milliseconds since boot and, unlike the 32-bit <c>TickCount</c>, does not wrap.</summary>
    private void UpdateCpuUptime() =>
        _cpuUptimeTile.Value = UptimeFormatter.Format(TimeSpan.FromMilliseconds(Environment.TickCount64));

    private async Task LoadCpuInfoAsync() {
        // GetAsync never throws (it falls back to CpuStaticInfo.Unknown), but guard the whole path so a
        // surprise can't take down the app via an unobserved task exception.
        try {
            var info = await CpuInfoProvider.GetAsync();
            _cpuRow.Sub = FormatCpuSub(info);
            _cpuRow.Spec = HardwareNameFormatter.ShortenCpu(info.Name);
        } catch {
            _cpuRow.Sub = "";
            _cpuRow.Spec = "Unknown CPU";
        }
    }

    /// <summary>Cores plus base clock for the rail sub-label, e.g. "24 cores · 3.2 GHz".</summary>
    private static string FormatCpuSub(CpuStaticInfo info) {
        var cores = info.PhysicalCores > 0 ? info.PhysicalCores : info.LogicalCores;
        if (cores > 0 && info.MaxClockMhz > 0)
            return $"{cores} cores · {(info.MaxClockMhz / 1000.0).ToString("0.0", CultureInfo.InvariantCulture)} GHz";
        return cores > 0 ? $"{cores} cores" : "";
    }

    /// <summary>Memory subscription callback: append load% to the history, then refresh the surface.</summary>
    private void OnMemory(MemorySample sample) {
        MetricChannel.PushHistory(_memoryHistory, sample.LoadPercent);
        UpdateMemory(sample);
    }

    /// <summary>Sampler-failure handler for the Memory metric: shows neutral placeholders.</summary>
    private void OnMemoryFailed() {
        _memoryRow.ValueText = "—";
        _memInUseTile.Value = "—";
        _memAvailableTile.Value = "—";
        _memCommittedTile.Value = "—";
    }

    private void UpdateMemory(MemorySample sample) {
        const double gb = 1L << 30;
        var usedGb = sample.UsedBytes / gb;
        var totalGb = sample.TotalBytes / gb;
        var committedGb = sample.CommittedBytes / gb;
        var limitGb = sample.CommitLimitBytes / gb;
        var rounded = Math.Round(sample.LoadPercent);

        _memoryRow.ValueText = rounded.ToString(CultureInfo.InvariantCulture);
        // Rail sub-caption mirrors the design comp's live "used / total" figure (e.g. "19.5 / 32 GB").
        _memoryRow.Sub = totalGb > 0
            ? $"{usedGb.ToString("F1", CultureInfo.InvariantCulture)} / {totalGb.ToString("F0", CultureInfo.InvariantCulture)} GB"
            : "";
        _memoryRow.Points = SparklinePoints.Build(_memoryHistory, 100);

        _memInUseTile.Value = $"{usedGb.ToString("F1", CultureInfo.InvariantCulture)} GB";
        _memAvailableTile.Value = $"{Math.Max(0, totalGb - usedGb).ToString("F1", CultureInfo.InvariantCulture)} GB";
        _memCommittedTile.Value = limitGb > 0
            ? $"{committedGb.ToString("F0", CultureInfo.InvariantCulture)} / {limitGb.ToString("F0", CultureInfo.InvariantCulture)} GB"
            : "—";
    }

    private async Task LoadMemoryInfoAsync() {
        // GetAsync never throws (it falls back to MemoryStaticInfo.Unknown), but guard the whole path
        // so a surprise can't take down the app via an unobserved task exception.
        try {
            var info = await MemoryInfoProvider.GetAsync();
            _memoryRow.Spec = FormatMemorySpec(info);
        } catch {
            _memoryRow.Spec = "Unknown RAM";
        }
    }

    /// <summary>Type, speed and slot count for the detail spec header, e.g. "DDR5-6000 · 2 slots".</summary>
    private static string FormatMemorySpec(MemoryStaticInfo info) {
        var label = info.SpeedMhz > 0
            ? $"{info.TypeLabel}-{info.SpeedMhz.ToString(CultureInfo.InvariantCulture)}"
            : info.TypeLabel;
        return info.ModuleCount > 0
            ? $"{label} · {info.ModuleCount.ToString(CultureInfo.InvariantCulture)} slots"
            : label;
    }

    /// <summary>Enumerates the physical disks (off the UI thread) via the shared <see cref="DeviceInventory"/>
    /// and rebuilds the per-disk rows. Soft-fails to no disk rows on any error.</summary>
    private async Task LoadInventoryAsync() {
        try {
            var inventory = await DeviceInventory.LoadAsync();
            BuildDiskRows(inventory.All(DeviceCategory.Disk));
        } catch {
            // Leave the rail without disk rows on a transient failure.
        }
    }

    /// <summary>Builds one live rail row per physical disk (identity from the inventory; live active-time /
    /// read / write / response from the page-local sampler), then re-filters the rail and seeds the readouts
    /// once so the new rows aren't blank until the next tick.</summary>
    private void BuildDiskRows(IReadOnlyList<DeviceInstance> disks) {
        _disks.Clear();
        _disksByNumber.Clear();

        foreach (var disk in disks) {
            var history = new double[WindowSeconds];
            var activeTile = new StatTile("Active", "0 %");
            var readTile = new StatTile("Read", "0 MB/s");
            var writeTile = new StatTile("Write", "0 MB/s");
            var responseTile = new StatTile("Response", "0 ms");
            var row = new ResourceRow(disk.Name, disk.Sub, disk.Spec, "0", "%", Brush("#ffcf4d"),
                                      SparklinePoints.Build(history, 100),
                                      new[] { activeTile, readTile, writeTile, responseTile }, Select);
            var resource = new DiskResource {
                DiskNumber = disk.DiskNumber ?? -1, Row = row, History = history,
                ActiveTile = activeTile, ReadTile = readTile, WriteTile = writeTile, ResponseTile = responseTile,
            };
            _disks.Add(resource);
            if (resource.DiskNumber >= 0)
                _disksByNumber[resource.DiskNumber] = resource;
        }

        RebuildResources();
        UpdateDisks();
    }

    private void OnThroughputTick(object? sender, EventArgs e) {
        UpdateDisks();
        UpdateGpuEngines();
        UpdateCpuCores();
    }

    /// <summary>Samples per-logical-processor utilisation and rebuilds each core's mini chart. Builds the core
    /// charts lazily on the first non-empty sample (its instances name and count the charts, capped for very
    /// high core counts). Sampled every tick so the Detailed view is warm when opened.</summary>
    private void UpdateCpuCores() {
        var samples = _cpuSampler.Sample();
        if (samples.Count == 0)
            return;
        if (_cpuCores.Count == 0)
            BuildCpuCores(samples);

        foreach (var sample in samples) {
            if (!_cpuCoresByInstance.TryGetValue(sample.Instance, out var core))
                continue;
            MetricChannel.PushHistory(core.History, Math.Clamp(sample.Percent, 0, 100));
            core.Chart.Points = SparklinePoints.Build(core.History, 100);
        }
    }

    /// <summary>Creates one mini chart per logical processor (labelled "CPU 0", "CPU 1", … in group/core order)
    /// and marks the CPU row detail-capable. Called once, on the first sample that reports cores.</summary>
    private void BuildCpuCores(IReadOnlyList<LogicalProcessorSample> samples) {
        var count = Math.Min(samples.Count, MaxLogicalProcessorCharts);
        for (var i = 0; i < count; i++) {
            var core = new CoreChart {
                Instance = samples[i].Instance, Chart = new SubChart($"CPU {i}", _cpuRow.ValueBrush),
                History = new double[WindowSeconds],
            };
            _cpuCores.Add(core);
            _cpuCoresByInstance[core.Instance] = core;
        }

        // Set SupportsDetail last (after the label + charts) so the toggle only appears once the grid is ready.
        _cpuRow.DetailLabel = "Logical processors";
        _cpuRow.SubCharts = _cpuCores.ConvertAll(c => c.Chart);
        _cpuRow.SupportsDetail = true;
    }

    /// <summary>
    /// Samples per-engine GPU utilisation and rebuilds each engine's mini chart. Drivers expose different,
    /// variably-cased engine sets (e.g. "3d", "compute 0", "videodecode", "high priority 3d"), so the charts
    /// are discovered dynamically rather than hardcoded: raw engtype instances are aggregated by base engine
    /// (dropping a trailing instance index, so "compute 0" + "compute 1" fold into "Compute"), and a chart is
    /// added the first time each engine reports. Sampled every tick so the Detailed view is warm when opened.
    /// </summary>
    private void UpdateGpuEngines() {
        var raw = _gpuEngineSampler.SampleEngines();
        if (raw.Count == 0)
            return;

        var byEngine = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var (token, value) in raw) {
            var key = NormalizeEngine(token);
            byEngine[key] = byEngine.GetValueOrDefault(key) + value;
        }

        if (AddNewEngines(byEngine.Keys))
            PublishGpuEngines();

        foreach (var engine in _gpuEngines) {
            byEngine.TryGetValue(engine.Key, out var value);
            MetricChannel.PushHistory(engine.History, Math.Clamp(value, 0, 100));
            engine.Chart.Points = SparklinePoints.Build(engine.History, 100);
        }
    }

    /// <summary>Adds a mini chart for any base engine not seen before; returns whether the set changed.</summary>
    private bool AddNewEngines(IEnumerable<string> engineKeys) {
        var added = false;
        foreach (var key in engineKeys) {
            if (_gpuEnginesByBase.ContainsKey(key))
                continue;
            var chart = new EngineChart {
                Key = key, Chart = new SubChart(FormatEngineLabel(key), _gpuRow.ValueBrush),
                History = new double[WindowSeconds],
            };
            _gpuEngines.Add(chart);
            _gpuEnginesByBase[key] = chart;
            added = true;
        }
        return added;
    }

    /// <summary>Orders the engine charts (main engines first) and republishes them to the GPU row, marking it
    /// detail-capable once the first engine appears.</summary>
    private void PublishGpuEngines() {
        _gpuEngines.Sort(static (a, b) => {
            var order = EngineOrder(a.Key).CompareTo(EngineOrder(b.Key));
            return order != 0 ? order : string.CompareOrdinal(a.Key, b.Key);
        });
        _gpuRow.SubCharts = _gpuEngines.ConvertAll(e => e.Chart);
        if (!_gpuRow.SupportsDetail) {
            _gpuRow.DetailLabel = "Individual engines";
            _gpuRow.SupportsDetail = true;
        }
    }

    // Curated labels for the common engtype tokens; anything else falls back to a title-cased form.
    private static readonly Dictionary<string, string> KnownEngineLabels = new(StringComparer.Ordinal) {
        ["3d"] = "3D", ["copy"] = "Copy", ["compute"] = "Compute",
        ["videodecode"] = "Video Decode", ["video decode"] = "Video Decode",
        ["videoencode"] = "Video Encode", ["video encode"] = "Video Encode",
        ["videoprocessing"] = "Video Processing", ["videocodec"] = "Video Codec", ["video codec"] = "Video Codec",
        ["videojpeg"] = "Video JPEG", ["video jpeg"] = "Video JPEG", ["legacyoverlay"] = "Legacy Overlay",
        ["security"] = "Security", ["timer"] = "Timer", ["ofa"] = "OFA", ["vr"] = "VR",
    };

    /// <summary>Normalises a raw engtype token to a base engine name: lower-cased, underscores → spaces, with a
    /// trailing instance index dropped so "compute 0" and "compute 1" fold together.</summary>
    private static string NormalizeEngine(string token) {
        var name = token.Replace('_', ' ').Trim().ToLowerInvariant();
        var lastSpace = name.LastIndexOf(' ');
        if (lastSpace > 0 && int.TryParse(name.AsSpan(lastSpace + 1), out _))
            name = name[..lastSpace];
        return name;
    }

    /// <summary>Friendly label for a base engine name — a curated name where known, else a title-cased form
    /// (tokens containing a digit, e.g. "3d", are upper-cased whole).</summary>
    private static string FormatEngineLabel(string baseName) {
        if (KnownEngineLabels.TryGetValue(baseName, out var label))
            return label;
        var words = baseName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < words.Length; i++) {
            var w = words[i];
            words[i] = w.Any(char.IsDigit) ? w.ToUpperInvariant() : char.ToUpperInvariant(w[0]) + w[1..];
        }
        return string.Join(' ', words);
    }

    /// <summary>Display order for the engine grid — the main engines first, everything else after.</summary>
    private static int EngineOrder(string baseName) => baseName switch {
        "3d" => 0,
        "compute" => 1,
        "copy" => 2,
        "videodecode" or "video decode" => 3,
        "videoencode" or "video encode" => 4,
        "videoprocessing" => 5,
        "videocodec" or "video codec" => 6,
        _ => 9,
    };

    /// <summary>Samples each disk and refreshes its row + tiles in place: rail value + chart + Active tile show
    /// Task Manager's "Active time" (0–100 %); Read/Write show throughput; Response shows the average transfer
    /// time. Disks without a current reading are left unchanged.</summary>
    private void UpdateDisks() {
        foreach (var sample in _throughputSampler.Sample()) {
            if (!_disksByNumber.TryGetValue(sample.DiskNumber, out var disk))
                continue;
            MetricChannel.PushHistory(disk.History, sample.ActivePercent);
            var rounded = Math.Round(sample.ActivePercent);
            disk.Row.ValueText = rounded.ToString(CultureInfo.InvariantCulture);
            disk.Row.Points = SparklinePoints.Build(disk.History, 100);
            disk.ActiveTile.Value = $"{rounded.ToString(CultureInfo.InvariantCulture)} %";
            disk.ReadTile.Value = FormatRate(sample.ReadBytesPerSec);
            disk.WriteTile.Value = FormatRate(sample.WriteBytesPerSec);
            // Avg. Disk sec/Transfer is in seconds; show it in milliseconds like Task Manager's "Response".
            disk.ResponseTile.Value = $"{(sample.ResponseSeconds * 1000).ToString("0.0", CultureInfo.InvariantCulture)} ms";
        }
    }

    /// <summary>Formats a byte/second throughput as "N MB/s" (binary MiB), whole at ≥ 10 and one
    /// decimal below so small transfers stay legible.</summary>
    private static string FormatRate(double bytesPerSec) {
        var mib = bytesPerSec / (1L << 20);
        var value = mib >= 10
            ? Math.Round(mib).ToString(CultureInfo.InvariantCulture)
            : mib.ToString("F1", CultureInfo.InvariantCulture);
        return $"{value} MB/s";
    }

    /// <summary>GPU subscription callback: append to the history, then refresh the surface.</summary>
    private void OnGpu(double value) {
        MetricChannel.PushHistory(_gpuHistory, value);
        UpdateGpu(value);
    }

    /// <summary>Sampler-failure handler for the GPU metric: shows neutral placeholders.</summary>
    private void OnGpuFailed() {
        _gpuRow.ValueText = "—";
        _gpu3dTile.Value = "—";
    }

    private void UpdateGpu(double value) {
        var rounded = Math.Round(value);
        _gpuRow.ValueText = rounded.ToString(CultureInfo.InvariantCulture);
        _gpu3dTile.Value = $"{rounded.ToString(CultureInfo.InvariantCulture)} %";
        _gpuRow.Points = SparklinePoints.Build(_gpuHistory, 100);
    }

    private async Task LoadGpuInfoAsync() {
        // GetAsync never throws (it falls back to GpuStaticInfo.Unknown), but guard the whole path so a
        // surprise can't take down the app via an unobserved task exception.
        try {
            var info = await GpuInfoProvider.GetAsync();
            _gpuRow.Sub = HardwareNameFormatter.ShortenGpu(info.Name);
            _gpuRow.Spec = info.Name;
        } catch {
            _gpuRow.Sub = "";
            _gpuRow.Spec = "Unknown GPU";
        }
    }

    /// <summary>Network subscription callback: append the download rate to the history, then refresh.</summary>
    private void OnNetwork(NetworkSample sample) {
        MetricChannel.PushHistory(_downHistory, sample.DownMbps);
        UpdateNetwork(sample);
    }

    /// <summary>Sampler-failure handler for the Network metric: shows neutral placeholders.</summary>
    private void OnNetworkFailed() {
        _networkRow.ValueText = "—";
        _netReceiveTile.Value = "—";
        _netSendTile.Value = "—";
    }

    private void UpdateNetwork(NetworkSample sample) {
        // Each readout auto-scales to its own value so a small flow shows kbps beside a large one — the
        // rail value + unit and the Receive/Send tiles all read through the shared DataRateFormatter.
        var (downValue, downUnit) = DataRateFormatter.Split(sample.DownMbps);
        _networkRow.ValueText = downValue;
        _networkRow.Unit = downUnit;
        _netReceiveTile.Value = $"{downValue} {downUnit}";
        _netSendTile.Value = DataRateFormatter.Format(sample.UpMbps);

        // The chart has no natural 0–100 axis, so normalise against the rolling receive peak (with
        // headroom and a floor, like the Dashboard) so the filled area still spans the fixed axis.
        _networkRow.Points = SparklinePoints.Build(
            _downHistory, ChartScale.FitAxis(_downHistory, floor: MinNetworkScaleMbps));

        _netErrorsTile.Value = ReadNetworkErrors();
    }

    /// <summary>Cumulative incoming + outgoing packet errors on the primary adapter, or "—" if the
    /// adapter can't be read. Read from the cached interface each tick (a cheap syscall).</summary>
    private string ReadNetworkErrors() {
        try {
            if (_networkInterface is null)
                return "0";
            var stats = _networkInterface.GetIPStatistics();
            var errors = stats.IncomingPacketsWithErrors + stats.OutgoingPacketsWithErrors;
            return errors.ToString(CultureInfo.InvariantCulture);
        } catch {
            return "—";
        }
    }

    private async Task LoadNetworkInfoAsync() {
        // Resolve the adapter's link speed + description off the UI thread (enumeration can be slow),
        // then bind on the UI thread. SelectPrimary never throws in practice, but guard defensively.
        try {
            var (sub, spec) = await Task.Run(ReadNetworkInfo);
            _networkRow.Sub = sub;
            _networkRow.Spec = spec;
        } catch {
            _networkRow.Sub = "";
            _networkRow.Spec = "";
        }
    }

    /// <summary>Link speed (sub) and adapter description (spec) for the primary adapter.</summary>
    private static (string Sub, string Spec) ReadNetworkInfo() {
        var adapter = NetworkUsageSampler.SelectPrimary();
        if (adapter is null)
            return ("", "");
        return (FormatLinkSpeed(adapter.Speed), adapter.Description ?? "");
    }

    /// <summary>Formats an adapter link speed (bits/second) as "2.5 Gbps" / "866 Mbps", or "—" when
    /// unknown. Uses the decimal (1000) base that matches adapter link-speed conventions.</summary>
    private static string FormatLinkSpeed(long bitsPerSecond) {
        if (bitsPerSecond <= 0)
            return "—";
        var mbps = bitsPerSecond / 1_000_000.0;
        return mbps >= 1000
            ? $"{(mbps / 1000.0).ToString("0.#", CultureInfo.InvariantCulture)} Gbps"
            : $"{Math.Round(mbps).ToString(CultureInfo.InvariantCulture)} Mbps";
    }

    /// <summary>Selects a resource (single-select) so the detail pane swaps to it. No-ops when the
    /// resource is already selected. Same idiom as <c>NavigationViewModel.Navigate</c>.</summary>
    private void Select(ResourceRow row) {
        if (ReferenceEquals(row, SelectedResource))
            return;

        if (SelectedResource is not null)
            SelectedResource.IsSelected = false;
        SelectedResource = row;
        row.IsSelected = true;
    }

    /// <summary>Unsubscribes from the shared metrics and tears down the page-local per-disk + GPU-engine
    /// samplers and the timer. The shared feed's samplers are owned (and disposed) by the service. Safe to
    /// call more than once.</summary>
    public void Dispose() {
        foreach (var subscription in _subscriptions)
            subscription.Dispose();
        _throughputTimer.Stop();
        _throughputTimer.Tick -= OnThroughputTick;
        _gpuRow.PropertyChanged -= OnResourceDetailChanged;
        _cpuRow.PropertyChanged -= OnResourceDetailChanged;
        _throughputSampler.Dispose();
        _gpuEngineSampler.Dispose();
        _cpuSampler.Dispose();
    }

    /// <summary>A live per-disk rail row and its backing state: the rolling active-time history and the four
    /// stat tiles (Active / Read / Write / Response) the throughput sampler updates in place each tick.</summary>
    private sealed class DiskResource {
        public required int DiskNumber { get; init; }
        public required ResourceRow Row { get; init; }
        public required double[] History { get; init; }
        public required StatTile ActiveTile { get; init; }
        public required StatTile ReadTile { get; init; }
        public required StatTile WriteTile { get; init; }
        public required StatTile ResponseTile { get; init; }
    }

    /// <summary>One GPU engine's mini chart and its backing state: the engtype key the sampler reports under
    /// and the rolling history the view model rebuilds into <see cref="SubChart.Points"/> each tick.</summary>
    private sealed class EngineChart {
        public required string Key { get; init; }
        public required SubChart Chart { get; init; }
        public required double[] History { get; init; }
    }

    /// <summary>One logical processor's mini chart and its backing state: the PDH instance name the sampler
    /// reports under and the rolling history rebuilt into <see cref="SubChart.Points"/> each tick.</summary>
    private sealed class CoreChart {
        public required string Instance { get; init; }
        public required SubChart Chart { get; init; }
        public required double[] History { get; init; }
    }
}
