using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using DashDetective.Services.Network;
using DashDetective.Services.SystemMetrics;
using DashDetective.Shared;
using DashDetective.Shared.Charts;
using DashDetective.Tabs.Dashboard;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
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

    /// <summary>The resource rows shown in the left rail, in display order.</summary>
    public ObservableCollection<ResourceRow> Resources { get; } = new();

    /// <summary>The currently selected resource, whose detail the right pane shows.</summary>
    [ObservableProperty] private ResourceRow _selectedResource = null!;

    private readonly SystemMetricsService _service;
    private readonly IDisposable[] _subscriptions;

    // ---- CPU (live) ----
    private readonly double[] _cpuHistory = new double[WindowSeconds];
    private readonly ResourceRow _cpuRow;
    private readonly StatTile _cpuUtilTile;
    private readonly StatTile _cpuProcessesTile;
    private readonly StatTile _cpuUptimeTile;

    // ---- Memory (live) ----
    private readonly double[] _memoryHistory = new double[WindowSeconds];
    private readonly ResourceRow _memoryRow;
    private readonly StatTile _memInUseTile;
    private readonly StatTile _memAvailableTile;
    private readonly StatTile _memCommittedTile;

    // ---- Disk (live) ----
    private readonly double[] _storageHistory = new double[WindowSeconds];
    private readonly ResourceRow _diskRow;
    private readonly StatTile _diskActiveTile;
    private readonly StatTile _diskReadTile;
    private readonly StatTile _diskWriteTile;
    private readonly StatTile _diskResponseTile;

    // ---- GPU (live) ----
    private readonly double[] _gpuHistory = new double[WindowSeconds];
    private readonly ResourceRow _gpuRow;
    private readonly StatTile _gpu3dTile;

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

        // Disk — live. Rail value + chart show Task Manager's disk "Active time"; tiles show Active /
        // Read / Write / Response, all sampled from the aggregate _Total PhysicalDisk counters.
        _diskActiveTile = new StatTile("Active", "0 %");
        _diskReadTile = new StatTile("Read", "0 MB/s");
        _diskWriteTile = new StatTile("Write", "0 MB/s");
        _diskResponseTile = new StatTile("Response", "0 ms");

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
        Resources.Add(_cpuRow);

        _memoryRow = new ResourceRow("Memory", "", "", "0", "%", Brush("#c58fff"),
                                     SparklinePoints.Build(_memoryHistory, 100),
                                     new[] {
                                         _memInUseTile, _memAvailableTile,
                                         new StatTile("Cached", "—"), _memCommittedTile,
                                     }, Select);
        Resources.Add(_memoryRow);

        _diskRow = new ResourceRow("Disk 0 (C:)", "", "", "0", "%", Brush("#ffcf4d"),
                                   SparklinePoints.Build(_storageHistory, 100),
                                   new[] {
                                       _diskActiveTile, _diskReadTile, _diskWriteTile, _diskResponseTile,
                                   }, Select);
        Resources.Add(_diskRow);

        // GPU — VRAM / Temp / Power are blanked to "—": no reliable standard Windows source (GPU
        // temperature is already deferred out of scope in the project).
        _gpuRow = new ResourceRow("GPU", "", "", "0", "%", Brush("#6ccb5f"),
                                  SparklinePoints.Build(_gpuHistory, 100),
                                  new[] {
                                      _gpu3dTile, new StatTile("VRAM", "—"),
                                      new StatTile("Temp", "—"), new StatTile("Power", "—"),
                                  }, Select);
        Resources.Add(_gpuRow);

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
        Resources.Add(_networkRow);

        SelectedResource = Resources[0];
        SelectedResource.IsSelected = true;

        // Subscribe to the shared metrics; each subscription immediately replays the latest cached sample,
        // seeding the surfaces with real data on the first frame.
        _subscriptions = new[] {
            service.SubscribeCpu(OnCpu, OnCpuFailed),
            service.SubscribeMemory(OnMemory, OnMemoryFailed),
            service.SubscribeStorage(OnStorage, OnStorageFailed),
            service.SubscribeGpu(OnGpu, OnGpuFailed),
            service.SubscribeNetwork(OnNetwork, OnNetworkFailed),
        };

        // Load static hardware info off the UI thread; the sub/spec labels fill in when ready.
        _ = LoadCpuInfoAsync();
        _ = LoadMemoryInfoAsync();
        _ = LoadDiskInfoAsync();
        _ = LoadGpuInfoAsync();
        _ = LoadNetworkInfoAsync();
    }

    /// <summary>Toolbar Refresh: an immediate re-sample of every live metric, even while paused.</summary>
    public void Refresh() {
        _service.RefreshAll();
        _ = LoadCpuInfoAsync();
        _ = LoadMemoryInfoAsync();
        _ = LoadDiskInfoAsync();
        _ = LoadGpuInfoAsync();
        _ = LoadNetworkInfoAsync();
    }

    /// <summary>No local timers to pause — all sampling is the shared service's, which the shell pauses
    /// via <see cref="SystemMetricsService.Pause"/>. Present for the <see cref="ILiveSamplingPage"/> seam.</summary>
    public void SetLive(bool live) { }

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

    /// <summary>Disk subscription callback: append active-time to the history, then refresh the surface.</summary>
    private void OnStorage(StorageSample sample) {
        MetricChannel.PushHistory(_storageHistory, sample.ActivePercent);
        UpdateStorage(sample);
    }

    /// <summary>Sampler-failure handler for the Disk metric: shows neutral placeholders.</summary>
    private void OnStorageFailed() {
        _diskRow.ValueText = "—";
        _diskActiveTile.Value = "—";
        _diskReadTile.Value = "—";
        _diskWriteTile.Value = "—";
        _diskResponseTile.Value = "—";
    }

    private void UpdateStorage(StorageSample sample) {
        var rounded = Math.Round(sample.ActivePercent);
        _diskRow.ValueText = rounded.ToString(CultureInfo.InvariantCulture);
        _diskRow.Points = SparklinePoints.Build(_storageHistory, 100);

        _diskActiveTile.Value = $"{rounded.ToString(CultureInfo.InvariantCulture)} %";
        _diskReadTile.Value = FormatRate(sample.ReadBytesPerSec);
        _diskWriteTile.Value = FormatRate(sample.WriteBytesPerSec);
        // Avg. Disk sec/Transfer is in seconds; show it in milliseconds like Task Manager's "Response".
        _diskResponseTile.Value = $"{(sample.ResponseSeconds * 1000).ToString("0.0", CultureInfo.InvariantCulture)} ms";
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

    private async Task LoadDiskInfoAsync() {
        // GetAsync never throws (it falls back to DiskStaticInfo.Unknown), but guard the whole path so a
        // surprise can't take down the app via an unobserved task exception.
        try {
            var info = await DiskInfoProvider.GetAsync();
            _diskRow.Sub = string.IsNullOrEmpty(info.TypeLabel) ? "Drive" : info.TypeLabel;
            _diskRow.Spec = FormatDiskSpec(info);
        } catch {
            _diskRow.Sub = "";
            _diskRow.Spec = "Unknown drive";
        }
    }

    /// <summary>Model plus capacity for the detail spec header, e.g. "Samsung SSD 990 Pro 1.8 TB".
    /// Uses the app's binary TB/GB convention (1 TB = 1024 GB), matching the Dashboard storage card.</summary>
    private static string FormatDiskSpec(DiskStaticInfo info) {
        if (info.SizeGb <= 0)
            return info.Model;
        var size = info.SizeGb >= 1024
            ? $"{(info.SizeGb / 1024.0).ToString("0.#", CultureInfo.InvariantCulture)} TB"
            : $"{info.SizeGb.ToString("0", CultureInfo.InvariantCulture)} GB";
        return $"{info.Model} {size}";
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
        if (row == SelectedResource)
            return;

        SelectedResource.IsSelected = false;
        SelectedResource = row;
        row.IsSelected = true;
    }

    /// <summary>Unsubscribes from the shared metrics. The samplers are owned (and disposed) by the shared
    /// service. Safe to call more than once.</summary>
    public void Dispose() {
        foreach (var subscription in _subscriptions)
            subscription.Dispose();
    }
}
