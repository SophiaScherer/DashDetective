using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using DashDetective.Services.Network;
using DashDetective.Services.SystemMetrics;
using DashDetective.Shared;
using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DashDetective.Tabs.Dashboard;

/// <summary>
/// View model for the Dashboard page. Drives the live CPU / Memory / GPU / Storage / Network surfaces.
/// Each metric runs on its own <see cref="MetricChannel"/> (sampler + timer + rolling history) so a
/// failure in one never disturbs the others.
/// </summary>
public partial class DashboardViewModel : ViewModelBase, IRefreshablePage, ILiveSamplingPage, IDisposable {
    /// <summary>Width of the rolling CPU history, in seconds (one sample per second).</summary>
    private const int WindowSeconds = 60;

    /// <summary>
    /// Floor for the network throughput chart's shared vertical scale, in Mbps. Keeps an idle graph
    /// pinned flat near the bottom (rather than amplifying counter noise) and avoids a zero span.
    /// </summary>
    private const double MinNetworkScaleMbps = 1.0;

    private readonly CpuUsageSampler _cpuSampler = new();
    private readonly MetricChannel _cpuChannel;

    private readonly MemoryUsageSampler _memorySampler = new();
    private readonly MetricChannel<MemorySample> _memoryChannel;

    private readonly GpuUsageSampler _gpuSampler = new();
    private readonly MetricChannel _gpuChannel;

    private readonly StorageUsageSampler _storageSampler = new();
    private readonly MetricChannel<StorageSample> _storageChannel;

    private readonly NetworkUsageSampler _networkSampler = new();
    // The channel owns the download history; upload is a second rolling buffer pushed alongside it.
    private readonly double[] _upHistory = new double[WindowSeconds];
    private readonly MetricChannel<NetworkSample> _networkChannel;

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

    public DashboardViewModel() {
        // Each metric owns its own channel (sampler + 1 Hz timer + 60-sample history). The history
        // starts all-zero, so seeding via the Update* method draws a full-width flat graph from the
        // first frame; real samples then shift in from the right, one per second. Channels do not
        // auto-start, so we seed then Start() — mirroring the old per-metric constructors.
        _cpuChannel = new MetricChannel(TimeSpan.FromSeconds(1), WindowSeconds,
            () => _cpuSampler.Sample(), UpdateCpu, OnCpuFailed);
        UpdateCpu(0);
        _cpuChannel.Start();

        _memoryChannel = new MetricChannel<MemorySample>(TimeSpan.FromSeconds(1), WindowSeconds,
            () => _memorySampler.Sample(), static s => s.LoadPercent, UpdateMemory, OnMemoryFailed);
        UpdateMemory(_memorySampler.Sample());
        _memoryChannel.Start();

        _gpuChannel = new MetricChannel(TimeSpan.FromSeconds(1), WindowSeconds,
            () => _gpuSampler.Sample(), UpdateGpu, OnGpuFailed);
        UpdateGpu(0);
        _gpuChannel.Start();

        _storageChannel = new MetricChannel<StorageSample>(TimeSpan.FromSeconds(1), WindowSeconds,
            () => _storageSampler.Sample(), static s => s.ActivePercent,
            s => UpdateStorage(s.ActivePercent), OnStorageFailed);
        UpdateStorage(0);
        _storageChannel.Start();

        // The adapter label is chosen once, at construction, from the busiest active adapter.
        if (!string.IsNullOrWhiteSpace(_networkSampler.AdapterName))
            NetworkAdapterName = _networkSampler.AdapterName;
        _networkChannel = new MetricChannel<NetworkSample>(TimeSpan.FromSeconds(1), WindowSeconds,
            () => _networkSampler.Sample(), static s => s.DownMbps, OnNetworkSample, OnNetworkFailed);
        UpdateNetwork(new NetworkSample(0, 0));
        _networkChannel.Start();

        // Uptime updates on a coarse 30 s cadence — the smallest displayed unit is minutes, so a
        // faster tick would be wasted work. It has no sampler/history, so it stays a plain timer. Seed
        // once so it's correct on the first frame.
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
        _cpuChannel.SampleNow();
        _memoryChannel.SampleNow();
        _gpuChannel.SampleNow();
        _storageChannel.SampleNow();
        _networkChannel.SampleNow();
        UpdateUptime();

        _ = LoadCpuInfoAsync();
        _ = LoadMemoryInfoAsync();
        _ = LoadGpuInfoAsync();
        _ = LoadSystemInfoAsync();
    }

    /// <summary>Toolbar Refresh for the Dashboard: an immediate re-sample of every metric.</summary>
    public void Refresh() => RefreshNow();

    /// <summary>
    /// Pauses or resumes all live sampling by stopping/starting the five metric channels plus the
    /// uptime timer. Drives the shell's Live toggle; <see cref="RefreshNow"/> still works while
    /// paused. A channel previously auto-stopped after a sampler failure will simply fail and
    /// re-stop on resume — harmless.
    /// </summary>
    public void SetLive(bool live) {
        if (live) {
            _cpuChannel.Start();
            _memoryChannel.Start();
            _gpuChannel.Start();
            _storageChannel.Start();
            _networkChannel.Start();
            _uptimeTimer.Start();
        } else {
            _cpuChannel.Stop();
            _memoryChannel.Stop();
            _gpuChannel.Stop();
            _storageChannel.Stop();
            _networkChannel.Stop();
            _uptimeTimer.Stop();
        }
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
            CpuModelShort = ShortenCpuName(info.Name);
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
            GpuModelShort = ShortenGpuName(info.Name);
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

    /// <summary>
    /// Trims vendor decoration ("NVIDIA", "AMD", "(R)", "(TM)") from an adapter name so it fits the
    /// compact StatCard caption, e.g. "NVIDIA GeForce RTX 3060" → "GeForce RTX 3060",
    /// "AMD Radeon(TM) Graphics" → "Radeon Graphics".
    /// </summary>
    private static string ShortenGpuName(string raw) {
        if (string.IsNullOrWhiteSpace(raw))
            return "Unknown GPU";

        var name = raw.Replace("(R)", "").Replace("(r)", "")
                      .Replace("(TM)", "").Replace("(tm)", "");

        foreach (var vendor in new[] { "NVIDIA ", "AMD ", "Intel " })
            if (name.StartsWith(vendor, StringComparison.OrdinalIgnoreCase))
                name = name[vendor.Length..];

        return Regex.Replace(name, @"\s+", " ").Trim();
    }

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
        var name = ShortenCpuName(info.Name);
        return info.MaxClockMhz > 0
            ? $"{name} @ {info.MaxClockMhz / 1000.0:F2}GHz"
            : name;
    }

    /// <summary>Physical/logical core counts, e.g. "6 cores · 12 threads".</summary>
    private static string FormatCpuCores(CpuStaticInfo info) =>
        info.PhysicalCores > 0
            ? $"{info.PhysicalCores} cores · {info.LogicalCores} threads"
            : $"{info.LogicalCores} threads";

    /// <summary>Sampler-failure handler for the CPU channel: shows a neutral placeholder.</summary>
    private void OnCpuFailed() {
        CpuValueText = "—";
        CpuPercentText = "—";
    }

    private void UpdateCpu(double value) {
        var rounded = Math.Round(value);
        CpuPercent = value;
        CpuValueText = rounded.ToString(CultureInfo.InvariantCulture);
        CpuPercentText = $"{rounded}%";
        CpuPoints = BuildCpuPoints();
    }

    /// <summary>
    /// Trims WMI decoration ("(R)", "(TM)", "N-Core Processor", "CPU @ …GHz") from a
    /// processor name so it fits the compact StatCard caption, e.g.
    /// "AMD Ryzen 5 7600X 6-Core Processor" → "AMD Ryzen 5 7600X".
    /// </summary>
    private static string ShortenCpuName(string raw) {
        if (string.IsNullOrWhiteSpace(raw))
            return "Unknown CPU";

        var name = raw.Replace("(R)", "").Replace("(r)", "")
                      .Replace("(TM)", "").Replace("(tm)", "");

        var atIndex = name.IndexOf(" @", StringComparison.Ordinal);
        if (atIndex >= 0)
            name = name[..atIndex];

        name = Regex.Replace(name, @"\s+\d+-Core Processor", "");
        name = name.Replace(" Processor", "").Replace(" CPU", "");
        return Regex.Replace(name, @"\s+", " ").Trim();
    }

    /// <summary>
    /// Renders the history as a Sparkline "x,y" string. x is the sample index; y is
    /// <c>100 − value</c> so higher utilisation sits at the top (smaller y = top), paired
    /// with a fixed 0–100 axis on the Sparkline.
    /// </summary>
    private string BuildCpuPoints() => BuildPercentPoints(_cpuChannel.History);

    /// <summary>Sampler-failure handler for the GPU channel: shows a neutral placeholder.</summary>
    private void OnGpuFailed() => GpuValueText = "—";

    private void UpdateGpu(double value) {
        var rounded = Math.Round(value);
        GpuPercent = value;
        GpuValueText = rounded.ToString(CultureInfo.InvariantCulture);
        GpuPoints = BuildGpuPoints();
    }

    /// <summary>
    /// Renders the GPU history as a Sparkline "x,y" string, matching <see cref="BuildCpuPoints"/>:
    /// x is the sample index; y is <c>100 − value</c> so higher usage sits at the top, paired with a
    /// fixed 0–100 axis on the Sparkline.
    /// </summary>
    private string BuildGpuPoints() => BuildPercentPoints(_gpuChannel.History);

    /// <summary>Sampler-failure handler for the Storage channel: shows a neutral placeholder.</summary>
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
        StoragePoints = BuildStoragePoints();
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

    /// <summary>
    /// Renders the storage-activity history as a Sparkline "x,y" string, matching
    /// <see cref="BuildCpuPoints"/>: x is the sample index; y is <c>100 − value</c> so higher
    /// activity sits at the top, paired with a fixed 0–100 axis on the Sparkline.
    /// </summary>
    private string BuildStoragePoints() => BuildPercentPoints(_storageChannel.History);

    /// <summary>Sampler-failure handler for the Memory channel: shows a neutral placeholder.</summary>
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
        MemoryPoints = BuildMemoryPoints();
    }

    /// <summary>
    /// Renders the memory history as a Sparkline "x,y" string, matching <see cref="BuildCpuPoints"/>:
    /// x is the sample index; y is <c>100 − load</c> so higher usage sits at the top, paired with a
    /// fixed 0–100 axis on the Sparkline.
    /// </summary>
    private string BuildMemoryPoints() => BuildPercentPoints(_memoryChannel.History);

    /// <summary>
    /// Renders a 0–100 metric history as a Sparkline "x,y" string: x is the sample index; y is
    /// <c>100 − value</c> so higher values sit at the top (smaller y = top), paired with a fixed
    /// 0–100 axis on the Sparkline. Shared by the CPU/Memory/GPU/Storage charts.
    /// </summary>
    private static string BuildPercentPoints(ReadOnlySpan<double> history) {
        var sb = new StringBuilder(history.Length * 8);
        for (var i = 0; i < history.Length; i++) {
            if (i > 0)
                sb.Append(' ');
            sb.Append(i.ToString(CultureInfo.InvariantCulture));
            sb.Append(',');
            sb.Append((100 - history[i]).ToString("0.##", CultureInfo.InvariantCulture));
        }

        return sb.ToString();
    }

    /// <summary>Sampler-failure handler for the Network channel: shows a neutral placeholder.</summary>
    private void OnNetworkFailed() {
        NetworkDownText = "—";
        NetworkUpText = "—";
    }

    /// <summary>
    /// Network channel callback: the channel has already pushed the newest download rate into its
    /// history, so append the matching upload rate to the second buffer, then refresh the readouts.
    /// </summary>
    private void OnNetworkSample(NetworkSample sample) {
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

        // The two series still share ONE axis (the unfloored peak, floored only for rendering) so equal
        // pixel height means equal throughput.
        NetworkYMax = ComputeNetworkScale(NetworkPeak());
        NetworkDownPoints = BuildNetworkPoints(_networkChannel.History, NetworkYMax);
        NetworkUpPoints = BuildNetworkPoints(_upHistory, NetworkYMax);
    }

    /// <summary>Largest sample across both rolling windows (unfloored): drives the display unit and,
    /// once floored, the shared axis scale.</summary>
    private double NetworkPeak() {
        var down = _networkChannel.History;
        var max = 0.0;
        for (var i = 0; i < WindowSeconds; i++) {
            if (down[i] > max) max = down[i];
            if (_upHistory[i] > max) max = _upHistory[i];
        }

        return max;
    }

    /// <summary>
    /// Shared upper bound for both series: the peak plus ~15 % headroom so the peak line doesn't touch
    /// the top edge, clamped to <see cref="MinNetworkScaleMbps"/>.
    /// </summary>
    private static double ComputeNetworkScale(double peak) {
        var scaled = peak * 1.15;
        return scaled > MinNetworkScaleMbps ? scaled : MinNetworkScaleMbps;
    }

    /// <summary>
    /// Renders a throughput history as a Sparkline "x,y" string against the shared <paramref name="yMax"/>:
    /// x is the sample index; y is <c>yMax − value</c> so higher throughput sits at the top (smaller y =
    /// top), paired with a fixed 0–<paramref name="yMax"/> axis on the Sparkline.
    /// </summary>
    private static string BuildNetworkPoints(ReadOnlySpan<double> history, double yMax) {
        var sb = new StringBuilder(history.Length * 8);
        for (var i = 0; i < history.Length; i++) {
            if (i > 0)
                sb.Append(' ');
            sb.Append(i.ToString(CultureInfo.InvariantCulture));
            sb.Append(',');
            sb.Append((yMax - history[i]).ToString("0.##", CultureInfo.InvariantCulture));
        }

        return sb.ToString();
    }

    /// <summary>Stops the sampling channels and the uptime timer, and disposes samplers that own
    /// unmanaged handles. Safe to call more than once.</summary>
    public void Dispose() {
        _cpuChannel.Dispose();
        _memoryChannel.Dispose();
        _gpuChannel.Dispose();
        _storageChannel.Dispose();
        _networkChannel.Dispose();
        _uptimeTimer.Stop();
        _uptimeTimer.Tick -= OnUptimeTick;
        // Unlike the CPU/Memory samplers, the GPU and Storage samplers own PDH query handles.
        // The network sampler is fully managed, so it needs no disposal.
        _gpuSampler.Dispose();
        _storageSampler.Dispose();
    }
}
