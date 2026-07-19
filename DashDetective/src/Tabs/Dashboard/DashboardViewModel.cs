using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using DashDetective.Services.Network;
using DashDetective.Services.SystemMetrics;
using DashDetective.Shared;
using DashDetective.Shared.Charts;
using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace DashDetective.Tabs.Dashboard;

/// <summary>
/// View model for the Dashboard page. Drives the live CPU / Memory / GPU / Storage / Network surfaces
/// by subscribing to the shared <see cref="SystemMetricsService"/> — the samplers are shared across
/// pages; each surface keeps its own rolling history and rebuilds its chart in the subscription callback.
/// </summary>
public partial class DashboardViewModel : ViewModelBase, IRefreshablePage, ILiveSamplingPage, IDisposable {
    /// <summary>Width of the rolling CPU history, in seconds (one sample per second).</summary>
    private const int WindowSeconds = 60;

    /// <summary>
    /// Floor for the network throughput chart's shared vertical scale, in Mbps. Keeps an idle graph
    /// pinned flat near the bottom (rather than amplifying counter noise) and avoids a zero span.
    /// </summary>
    private const double MinNetworkScaleMbps = 1.0;

    private readonly SystemMetricsService _service;
    private readonly IDisposable[] _subscriptions;

    // Per-view rolling histories (the samplers are shared; the histories are not).
    private readonly double[] _cpuHistory = new double[WindowSeconds];
    private readonly double[] _memoryHistory = new double[WindowSeconds];
    private readonly double[] _gpuHistory = new double[WindowSeconds];
    private readonly double[] _storageHistory = new double[WindowSeconds];
    private readonly double[] _downHistory = new double[WindowSeconds];
    private readonly double[] _upHistory = new double[WindowSeconds];

    private readonly DispatcherTimer _uptimeTimer;

    [ObservableProperty] private double _cpuPercent;
    [ObservableProperty] private string _cpuValueText = "0";
    [ObservableProperty] private string _cpuPercentText = "0%";
    [ObservableProperty] private string _cpuPoints = "";
    [ObservableProperty] private string _cpuModelShort = "";
    [ObservableProperty] private string _cpuModelText = "";
    [ObservableProperty] private string _cpuCoresText = "";

    [ObservableProperty] private string _memoryValueText = "0";
    [ObservableProperty] private string _memorySubText = "";
    [ObservableProperty] private string _memoryUtilizationText = "";
    [ObservableProperty] private string _memoryPoints = "";
    [ObservableProperty] private string _memoryModelText = "";

    [ObservableProperty] private double _gpuPercent;
    [ObservableProperty] private string _gpuValueText = "0";
    [ObservableProperty] private string _gpuPoints = "";
    [ObservableProperty] private string _gpuModelShort = "";
    [ObservableProperty] private string _gpuModelText = "";

    [ObservableProperty] private string _storageValueText = "0";
    [ObservableProperty] private string _storageSubText = "";
    [ObservableProperty] private string _storagePoints = "";

    [ObservableProperty] private string _networkDownText = "0";
    [ObservableProperty] private string _networkUpText = "0";
    [ObservableProperty] private string _networkSubText = "↑ 0 Mbps";

    /// <summary>The download readout + stat card unit ("kbps"/"Mbps"/"Gbps"), auto-scaled from its value.</summary>
    [ObservableProperty] private string _networkDownUnit = "Mbps";

    /// <summary>The upload readout's unit ("kbps"/"Mbps"/"Gbps"), auto-scaled from its own value.</summary>
    [ObservableProperty] private string _networkUpUnit = "Mbps";
    [ObservableProperty] private string _networkDownPoints = "";
    [ObservableProperty] private string _networkUpPoints = "";
    [ObservableProperty] private double _networkYMax = MinNetworkScaleMbps;
    [ObservableProperty] private string _networkAdapterName = "Network";

    [ObservableProperty] private string _osText = "";
    [ObservableProperty] private string _deviceText = "";
    [ObservableProperty] private string _biosText = "";
    [ObservableProperty] private string _buildText = "";
    [ObservableProperty] private string _motherboardText = "";
    [ObservableProperty] private string _uptimeText = "";

    public DashboardViewModel(SystemMetricsService service) {
        _service = service;

        // The adapter label is chosen once from the busiest active adapter.
        if (!string.IsNullOrWhiteSpace(service.NetworkAdapterName))
            NetworkAdapterName = service.NetworkAdapterName;

        // Subscribe to the shared metrics. Each subscription immediately replays the latest cached
        // sample, seeding the surface with real data on the first frame; ticks then shift in from the right.
        _subscriptions = new[] {
            service.SubscribeCpu(OnCpu, OnCpuFailed),
            service.SubscribeMemory(OnMemory, OnMemoryFailed),
            service.SubscribeGpu(OnGpu, OnGpuFailed),
            service.SubscribeStorage(OnStorage, OnStorageFailed),
            service.SubscribeNetwork(OnNetwork, OnNetworkFailed),
        };

        // Uptime has no sampler/history, so it stays a plain 30 s timer. Seed once for the first frame.
        UpdateUptime();

        _uptimeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _uptimeTimer.Tick += OnUptimeTick;
        _uptimeTimer.Start();

        // Load static CPU hardware info off the UI thread; results are applied when ready.
        _ = LoadCpuInfoAsync();
        _ = LoadMemoryInfoAsync();
        _ = LoadGpuInfoAsync();
        _ = LoadSystemInfoAsync();
    }

    /// <summary>
    /// Forces an immediate update of every metric and re-reads the static hardware/system info,
    /// instead of waiting for the 1 Hz timers. Runs even while paused — a manual refresh should
    /// still update once. Drives the shell's Refresh action.
    /// </summary>
    public void RefreshNow() {
        _service.RefreshAll();
        UpdateUptime();

        _ = LoadCpuInfoAsync();
        _ = LoadMemoryInfoAsync();
        _ = LoadGpuInfoAsync();
        _ = LoadSystemInfoAsync();
    }

    /// <summary>Toolbar Refresh for the Dashboard: an immediate re-sample of every metric.</summary>
    public void Refresh() => RefreshNow();

    /// <summary>
    /// Pauses/resumes the Dashboard's own uptime timer for the shell's Live toggle. The shared metric
    /// sampling is paused separately by the shell via <see cref="SystemMetricsService.Pause"/>.
    /// </summary>
    public void SetLive(bool live) {
        if (live)
            _uptimeTimer.Start();
        else
            _uptimeTimer.Stop();
    }

    /// <summary>
    /// Builds a plain-text diagnostics report from the current on-screen values (no re-sampling),
    /// for the shell's Export action. Mirrors the "Save Report as .txt" convention of tools like
    /// msinfo32 and CPU-Z.
    /// </summary>
    public string BuildDiagnosticsReport() {
        var sb = new StringBuilder();
        sb.AppendLine("DashDetective — System Report");
        sb.AppendLine($"Generated: {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}");
        sb.AppendLine();

        sb.AppendLine("System");
        AppendReportRow(sb, "OS", OsText);
        AppendReportRow(sb, "Device", DeviceText);
        AppendReportRow(sb, "Motherboard", MotherboardText);
        AppendReportRow(sb, "BIOS", BiosText);
        AppendReportRow(sb, "Build", BuildText);
        AppendReportRow(sb, "Uptime", UptimeText);
        sb.AppendLine();

        sb.AppendLine("Live metrics");
        AppendReportRow(sb, "CPU", $"{CpuValueText}%  ({CpuModelText})");
        AppendReportRow(sb, "Memory", $"{MemoryUtilizationText}  ({MemoryModelText})");
        AppendReportRow(sb, "GPU", $"{GpuValueText}%  ({GpuModelText})");
        AppendReportRow(sb, "Storage", $"{StorageValueText}% active  ({StorageSubText})");
        AppendReportRow(sb, "Network", $"↓ {NetworkDownText} {NetworkDownUnit} / ↑ {NetworkUpText} {NetworkUpUnit}  ({NetworkAdapterName})");

        return sb.ToString();
    }

    /// <summary>Appends a left-aligned "key: value" line for the diagnostics report.</summary>
    private static void AppendReportRow(StringBuilder sb, string key, string value) =>
        sb.AppendLine($"  {(key + ":").PadRight(14)}{value}");

    private async Task LoadCpuInfoAsync() {
        // GetAsync never throws (it falls back to CpuStaticInfo.Unknown), but guard the whole
        // path so a surprise can't take down the app via an unobserved task exception.
        try {
            var info = await CpuInfoProvider.GetAsync();
            // Constructed on the UI thread, so the continuation resumes there — safe to bind.
            CpuModelShort = HardwareNameFormatter.ShortenCpu(info.Name);
            CpuModelText = FormatCpuModel(info);
            CpuCoresText = FormatCpuCores(info);
        } catch {
            CpuModelShort = "Unknown CPU";
            CpuModelText = "Unknown CPU";
        }
    }

    private async Task LoadMemoryInfoAsync() {
        // GetAsync never throws (it falls back to MemoryStaticInfo.Unknown), but guard the whole
        // path so a surprise can't take down the app via an unobserved task exception.
        try {
            var info = await MemoryInfoProvider.GetAsync();
            // Constructed on the UI thread, so the continuation resumes there — safe to bind.
            MemoryModelText = FormatMemoryModel(info);
        } catch {
            MemoryModelText = "Unknown RAM";
        }
    }

    private async Task LoadGpuInfoAsync() {
        // GetAsync never throws (it falls back to GpuStaticInfo.Unknown), but guard the whole path
        // so a surprise can't take down the app via an unobserved task exception.
        try {
            var info = await GpuInfoProvider.GetAsync();
            // Constructed on the UI thread, so the continuation resumes there — safe to bind.
            GpuModelShort = HardwareNameFormatter.ShortenGpu(info.Name);
            GpuModelText = info.Name;
        } catch {
            GpuModelShort = "Unknown GPU";
            GpuModelText = "Unknown GPU";
        }
    }

    private async Task LoadSystemInfoAsync() {
        // GetAsync never throws (it falls back to SystemStaticInfo.Unknown), but guard the whole
        // path so a surprise can't take down the app via an unobserved task exception.
        try {
            var info = await SystemInfoProvider.GetAsync();
            // Constructed on the UI thread, so the continuation resumes there — safe to bind.
            OsText = info.Os;
            DeviceText = info.Device;
            BiosText = info.Bios;
            BuildText = info.Build;
            MotherboardText = info.Motherboard;
        } catch {
            OsText = "Unknown OS";
            DeviceText = Environment.MachineName;
            BiosText = "Unknown BIOS";
            BuildText = "Unknown";
            MotherboardText = "Unknown motherboard";
        }
    }

    private void OnUptimeTick(object? sender, EventArgs e) => UpdateUptime();

    /// <summary>
    /// Refreshes the uptime readout from the system tick count. <see cref="Environment.TickCount64"/>
    /// is milliseconds since boot and, unlike the 32-bit <c>TickCount</c>, does not wrap.
    /// </summary>
    private void UpdateUptime() =>
        UptimeText = UptimeFormatter.Format(TimeSpan.FromMilliseconds(Environment.TickCount64));

    /// <summary>Capacity, type and speed for the System Information row, e.g. "32 GB DDR5-6000".</summary>
    private static string FormatMemoryModel(MemoryStaticInfo info) {
        if (info.TotalGb <= 0)
            return "Unknown RAM";

        var text = $"{info.TotalGb.ToString("F0", CultureInfo.InvariantCulture)} GB {info.TypeLabel}";
        return info.SpeedMhz > 0
            ? $"{text}-{info.SpeedMhz.ToString(CultureInfo.InvariantCulture)}"
            : text;
    }

    /// <summary>Model plus base clock for the System Information row, e.g. "AMD Ryzen 5 7600X @ 4.70GHz".</summary>
    private static string FormatCpuModel(CpuStaticInfo info) {
        var name = HardwareNameFormatter.ShortenCpu(info.Name);
        return info.MaxClockMhz > 0
            ? $"{name} @ {info.MaxClockMhz / 1000.0:F2}GHz"
            : name;
    }

    /// <summary>Physical/logical core counts, e.g. "6 cores · 12 threads".</summary>
    private static string FormatCpuCores(CpuStaticInfo info) =>
        info.PhysicalCores > 0
            ? $"{info.PhysicalCores} cores · {info.LogicalCores} threads"
            : $"{info.LogicalCores} threads";

    /// <summary>CPU subscription callback: append to the history, then refresh the surface.</summary>
    private void OnCpu(double value) {
        MetricChannel.PushHistory(_cpuHistory, value);
        UpdateCpu(value);
    }

    /// <summary>Sampler-failure handler for the CPU metric: shows a neutral placeholder.</summary>
    private void OnCpuFailed() {
        CpuValueText = "—";
        CpuPercentText = "—";
    }

    private void UpdateCpu(double value) {
        var rounded = Math.Round(value);
        CpuPercent = value;
        CpuValueText = rounded.ToString(CultureInfo.InvariantCulture);
        CpuPercentText = $"{rounded}%";
        CpuPoints = SparklinePoints.Build(_cpuHistory, 100);
    }

    /// <summary>GPU subscription callback: append to the history, then refresh the surface.</summary>
    private void OnGpu(double value) {
        MetricChannel.PushHistory(_gpuHistory, value);
        UpdateGpu(value);
    }

    /// <summary>Sampler-failure handler for the GPU metric: shows a neutral placeholder.</summary>
    private void OnGpuFailed() => GpuValueText = "—";

    private void UpdateGpu(double value) {
        var rounded = Math.Round(value);
        GpuPercent = value;
        GpuValueText = rounded.ToString(CultureInfo.InvariantCulture);
        GpuPoints = SparklinePoints.Build(_gpuHistory, 100);
    }

    /// <summary>Storage subscription callback: append active-time to the history, then refresh.</summary>
    private void OnStorage(StorageSample sample) {
        MetricChannel.PushHistory(_storageHistory, sample.ActivePercent);
        UpdateStorage(sample.ActivePercent);
    }

    /// <summary>Sampler-failure handler for the Storage metric: shows a neutral placeholder.</summary>
    private void OnStorageFailed() {
        StorageValueText = "—";
        StorageSubText = "";
    }

    /// <summary>
    /// Updates the storage card from the latest activity reading: the headline shows Task Manager's
    /// disk "Active time" (0–100 %), the sparkline shows its 60-second history, and the caption shows
    /// system-drive capacity.
    /// </summary>
    private void UpdateStorage(double value) {
        StorageValueText = Math.Round(value).ToString(CultureInfo.InvariantCulture);
        StoragePoints = SparklinePoints.Build(_storageHistory, 100);
        UpdateStorageCapacity();
    }

    /// <summary>
    /// Reads the system drive's capacity via <see cref="DriveInfo"/> and updates the "used / total"
    /// caption. DriveInfo is a cheap syscall, so this runs on every tick; any failure clears the
    /// caption.
    /// </summary>
    private void UpdateStorageCapacity() {
        try {
            var root = Path.GetPathRoot(Environment.SystemDirectory);
            var drive = string.IsNullOrEmpty(root) ? null : new DriveInfo(root);
            if (drive is null || !drive.IsReady || drive.TotalSize <= 0) {
                StorageSubText = "";
                return;
            }

            var total = drive.TotalSize;
            var used = total - drive.TotalFreeSpace;
            StorageSubText = FormatCapacity(used, total);
        } catch {
            StorageSubText = "";
        }
    }

    /// <summary>Formats used/total bytes as "1.36 / 2.0 TB" (or GB when total is under 1 TB).</summary>
    private static string FormatCapacity(long usedBytes, long totalBytes) {
        const double tb = 1L << 40;
        const double gb = 1L << 30;
        return totalBytes >= tb
            ? $"{(usedBytes / tb).ToString("F2", CultureInfo.InvariantCulture)} / {(totalBytes / tb).ToString("F1", CultureInfo.InvariantCulture)} TB"
            : $"{Math.Round(usedBytes / gb).ToString(CultureInfo.InvariantCulture)} / {Math.Round(totalBytes / gb).ToString(CultureInfo.InvariantCulture)} GB";
    }

    /// <summary>Memory subscription callback: append load% to the history, then refresh the surface.</summary>
    private void OnMemory(MemorySample sample) {
        MetricChannel.PushHistory(_memoryHistory, sample.LoadPercent);
        UpdateMemory(sample);
    }

    /// <summary>Sampler-failure handler for the Memory metric: shows a neutral placeholder.</summary>
    private void OnMemoryFailed() {
        MemoryValueText = "—";
        MemorySubText = "";
    }

    private void UpdateMemory(MemorySample sample) {
        var usedGb = sample.UsedBytes / (double)(1L << 30);
        var totalGb = sample.TotalBytes / (double)(1L << 30);
        var rounded = Math.Round(sample.LoadPercent);

        MemoryValueText = usedGb.ToString("F1", CultureInfo.InvariantCulture);
        MemorySubText = totalGb > 0
            ? $"{rounded.ToString(CultureInfo.InvariantCulture)}% of {totalGb.ToString("F0", CultureInfo.InvariantCulture)} GB"
            : "";
        MemoryUtilizationText = totalGb > 0
            ? $"{usedGb.ToString("F1", CultureInfo.InvariantCulture)} / {totalGb.ToString("F0", CultureInfo.InvariantCulture)} GB"
            : "";
        MemoryPoints = SparklinePoints.Build(_memoryHistory, 100);
    }

    /// <summary>Sampler-failure handler for the Network metric: shows a neutral placeholder.</summary>
    private void OnNetworkFailed() {
        NetworkDownText = "—";
        NetworkUpText = "—";
    }

    /// <summary>Network subscription callback: append the download + upload rates to their buffers, then
    /// refresh the readouts.</summary>
    private void OnNetwork(NetworkSample sample) {
        MetricChannel.PushHistory(_downHistory, sample.DownMbps);
        MetricChannel.PushHistory(_upHistory, sample.UpMbps);
        UpdateNetwork(sample);
    }

    /// <summary>
    /// Updates the throughput readouts and both sparkline series. Download and upload share one
    /// vertical scale (<see cref="NetworkYMax"/>) so their heights are directly comparable; the scale
    /// auto-fits to the busiest of the two 60-second windows, with headroom and a floor.
    /// </summary>
    private void UpdateNetwork(NetworkSample sample) {
        // Each readout auto-scales to its OWN value so a small flow shows kbps even beside a large one.
        // Scaling from the actual value — never the floored axis scale — is what lets the unit switch.
        (NetworkDownText, NetworkDownUnit) = DataRateFormatter.Split(sample.DownMbps);
        (NetworkUpText, NetworkUpUnit) = DataRateFormatter.Split(sample.UpMbps);
        NetworkSubText = $"↑ {NetworkUpText} {NetworkUpUnit}";

        // Both series share ONE axis (the peak of both windows, with headroom and floor) so equal pixel
        // height means equal throughput.
        NetworkYMax = ChartScale.FitAxis(_downHistory, _upHistory, MinNetworkScaleMbps);
        NetworkDownPoints = SparklinePoints.Build(_downHistory, NetworkYMax);
        NetworkUpPoints = SparklinePoints.Build(_upHistory, NetworkYMax);
    }

    /// <summary>Unsubscribes from the shared metrics and stops the uptime timer. The samplers are owned
    /// (and disposed) by the shared service. Safe to call more than once.</summary>
    public void Dispose() {
        foreach (var subscription in _subscriptions)
            subscription.Dispose();
        _uptimeTimer.Stop();
        _uptimeTimer.Tick -= OnUptimeTick;
    }
}
