using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using DashDetective.Services.Network;
using DashDetective.Services.SystemMetrics;
using DashDetective.Shared;
using DashDetective.Shared.Charts;
using DashDetective.Tabs.Storage;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
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

    // ---- Top stat-card row (collection-bound; one card per detected device) ----

    /// <summary>The Dashboard's top stat cards, in grouped order: CPU → Memory → GPU → Disks → Network. Disks
    /// are inserted once enumerated, so several drives each get their own card (up to five per row, wrapping).</summary>
    public ObservableCollection<DashboardCard> Cards { get; } = new();

    private readonly DashboardCard _cpuCard = new(DeviceCategory.Cpu, "CPU", "%");
    private readonly DashboardCard _memoryCard = new(DeviceCategory.Memory, "MEMORY", "GB");
    private readonly DashboardCard _networkCard = new(DeviceCategory.Network, "NETWORK", "Mbps");

    // Per-disk cards + rolling active-time histories keyed by disk number, and the page-local sampler/timer that
    // drives them (like the Storage tab). A disk card's value + chart show Task Manager's disk "Active time";
    // its caption shows capacity used.
    private readonly Dictionary<int, DashboardCard> _diskCards = new();
    private readonly Dictionary<int, double[]> _diskHistories = new();
    private readonly PhysicalDiskThroughputSampler _throughputSampler = new();
    private readonly DispatcherTimer _throughputTimer;

    // Per-GPU cards + rolling utilisation histories keyed by adapter LUID, driven by a page-local per-adapter
    // sampler on the same throughput timer (the shared GPU feed reports only one combined figure). One card per
    // physical GPU, inserted after Memory; its value + chart show the adapter's busiest-engine utilisation.
    private readonly Dictionary<string, DashboardCard> _gpuCards = new(StringComparer.Ordinal);
    private readonly Dictionary<string, double[]> _gpuHistories = new(StringComparer.Ordinal);
    private readonly GpuUsageSampler _gpuSampler = new();

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

    // Overall GPU % (busiest adapter) and the joined adapter names — used by the text report and the System
    // Information "GPU" row; the live per-GPU cards are collection-bound instead.
    [ObservableProperty] private string _gpuValueText = "0";
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
            service.SubscribeStorage(OnStorage, OnStorageFailed),
            service.SubscribeNetwork(OnNetwork, OnNetworkFailed),
        };

        // Seed the top stat row: the singleton cards show live data immediately; disk cards insert once
        // enumerated (before the Network card, keeping the CPU→Memory→GPU→Disks→Network grouping).
        Cards.Add(_cpuCard);
        Cards.Add(_memoryCard);
        Cards.Add(_networkCard);

        // Uptime has no sampler/history, so it stays a plain 30 s timer. Seed once for the first frame.
        UpdateUptime();

        _uptimeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _uptimeTimer.Tick += OnUptimeTick;
        _uptimeTimer.Start();

        // Drive the per-disk card sparklines from the page-local throughput sampler on their own 1 Hz timer.
        _throughputTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _throughputTimer.Tick += OnThroughputTick;
        _throughputTimer.Start();

        // Load static CPU hardware info off the UI thread; results are applied when ready.
        _ = LoadCpuInfoAsync();
        _ = LoadMemoryInfoAsync();
        _ = LoadGpusAsync();
        _ = LoadSystemInfoAsync();
        _ = LoadDisksAsync();
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
        _ = LoadGpusAsync();
        _ = LoadSystemInfoAsync();
        _ = LoadDisksAsync();
        UpdateGpuAdapters();
    }

    /// <summary>Toolbar Refresh for the Dashboard: an immediate re-sample of every metric.</summary>
    public void Refresh() => RefreshNow();

    /// <summary>
    /// Pauses/resumes the Dashboard's own uptime + per-disk throughput timers for the shell's Live toggle. The
    /// shared metric sampling is paused separately by the shell via <see cref="SystemMetricsService.Pause"/>.
    /// </summary>
    public void SetLive(bool live) {
        if (live) {
            _uptimeTimer.Start();
            _throughputTimer.Start();
        } else {
            _uptimeTimer.Stop();
            _throughputTimer.Stop();
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

    /// <summary>
    /// Renders the rolling 60-second metric histories as CSV for the Settings "Export CSV" action.
    /// One row per sample slot, oldest first: <c>offsetSeconds</c> counts back from 0 (now) to
    /// −(window−1); the metric columns are the same buffers the sparklines draw. Values use the
    /// invariant culture so the file parses consistently regardless of the machine's locale.
    /// </summary>
    public string BuildMetricsCsv() {
        var sb = new StringBuilder();
        sb.AppendLine("offsetSeconds,cpu,mem,gpu,disk,netDownMbps,netUpMbps");
        for (var i = 0; i < WindowSeconds; i++) {
            var offset = i - (WindowSeconds - 1);
            sb.Append(offset.ToString(CultureInfo.InvariantCulture)).Append(',')
              .Append(Csv(_cpuHistory[i])).Append(',')
              .Append(Csv(_memoryHistory[i])).Append(',')
              .Append(Csv(_gpuHistory[i])).Append(',')
              .Append(Csv(_storageHistory[i])).Append(',')
              .Append(Csv(_downHistory[i])).Append(',')
              .Append(Csv(_upHistory[i]))
              .Append('\n');
        }
        return sb.ToString();
    }

    /// <summary>Formats a metric value for CSV with two decimals, invariant culture.</summary>
    private static string Csv(double value) => value.ToString("F2", CultureInfo.InvariantCulture);

    private async Task LoadCpuInfoAsync() {
        // GetAsync never throws (it falls back to CpuStaticInfo.Unknown), but guard the whole
        // path so a surprise can't take down the app via an unobserved task exception.
        try {
            var info = await CpuInfoProvider.GetAsync();
            CpuModelShort = HardwareNameFormatter.ShortenCpu(info.Name);
            CpuModelText = FormatCpuModel(info);
            CpuCoresText = FormatCpuCores(info);
            _cpuCard.Sub = CpuModelShort;
        } catch {
            CpuModelShort = "Unknown CPU";
            CpuModelText = "Unknown CPU";
            _cpuCard.Sub = CpuModelShort;
        }
    }

    private async Task LoadMemoryInfoAsync() {
        // GetAsync never throws (it falls back to MemoryStaticInfo.Unknown), but guard the whole
        // path so a surprise can't take down the app via an unobserved task exception.
        try {
            var info = await MemoryInfoProvider.GetAsync();
            MemoryModelText = FormatMemoryModel(info);
        } catch {
            MemoryModelText = "Unknown RAM";
        }
    }

    /// <summary>Enumerates the physical GPUs (off the UI thread) via the shared <see cref="DeviceInventory"/>
    /// and rebuilds the per-GPU cards + the System-Information "GPU" row. Soft-fails to no GPU cards on any
    /// error.</summary>
    private async Task LoadGpusAsync() {
        try {
            var inventory = await DeviceInventory.LoadAsync();
            RebuildGpuCards(inventory.All(DeviceCategory.Gpu));
        } catch {
            // Leave the existing GPU cards in place on a transient failure.
        }
    }

    /// <summary>Reconciles the GPU cards to the current adapter set: drops the old GPU cards, then inserts one
    /// per real adapter just after the Memory card (keeping the CPU→Memory→GPU→Disks→Network grouping). Each
    /// card's caption is its short model; its value + sparkline (busiest-engine %) are seeded here and then
    /// driven by the throughput timer. The System-Information "GPU" row lists every adapter's full name.</summary>
    private void RebuildGpuCards(IReadOnlyList<DeviceInstance> gpus) {
        foreach (var card in _gpuCards.Values)
            Cards.Remove(card);
        _gpuCards.Clear();
        _gpuHistories.Clear();

        var insertAt = Cards.IndexOf(_memoryCard) + 1;
        foreach (var gpu in gpus) {
            var card = new DashboardCard(DeviceCategory.Gpu, gpu.Name.ToUpperInvariant(), "%") { Sub = gpu.Sub };
            Cards.Insert(insertAt++, card);
            _gpuCards[gpu.GpuLuid ?? gpu.Id] = card;
            _gpuHistories[gpu.GpuLuid ?? gpu.Id] = new double[WindowSeconds];
        }

        GpuModelText = gpus.Count > 0 ? string.Join(" / ", gpus.Select(g => g.Spec)) : "Unknown GPU";

        // Seed the new cards' value + charts once so they aren't blank until the next throughput tick.
        UpdateGpuAdapters();
    }

    private async Task LoadSystemInfoAsync() {
        // GetAsync never throws (it falls back to SystemStaticInfo.Unknown), but guard the whole
        // path so a surprise can't take down the app via an unobserved task exception.
        try {
            var info = await SystemInfoProvider.GetAsync();
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
        _cpuCard.Value = "—";
    }

    private void UpdateCpu(double value) {
        var rounded = Math.Round(value);
        CpuPercent = value;
        CpuValueText = rounded.ToString(CultureInfo.InvariantCulture);
        CpuPercentText = $"{rounded}%";
        CpuPoints = SparklinePoints.Build(_cpuHistory, 100);
        _cpuCard.Value = CpuValueText;
        _cpuCard.Points = CpuPoints;
    }

    /// <summary>Samples every physical GPU (busiest-engine %) and refreshes each card's headline value +
    /// sparkline in place, keyed by adapter LUID. Also feeds the single overall history (busiest adapter) that
    /// the CSV export + text report read. GPUs without a current reading are left unchanged.</summary>
    private void UpdateGpuAdapters() {
        var adapters = _gpuSampler.SampleAdapters();
        if (adapters.Count == 0)
            return;

        double overall = 0;
        foreach (var (luid, sample) in adapters) {
            var value = Math.Clamp(sample.Overall, 0, 100);
            if (value > overall)
                overall = value;
            if (!_gpuHistories.TryGetValue(luid, out var history) || !_gpuCards.TryGetValue(luid, out var card))
                continue;
            MetricChannel.PushHistory(history, value);
            card.Value = Math.Round(value).ToString(CultureInfo.InvariantCulture);
            card.Points = SparklinePoints.Build(history, 100);
        }

        MetricChannel.PushHistory(_gpuHistory, overall);
        GpuValueText = Math.Round(overall).ToString(CultureInfo.InvariantCulture);
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
        _memoryCard.Value = "—";
        _memoryCard.Sub = "";
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
        _memoryCard.Value = MemoryValueText;
        _memoryCard.Sub = MemorySubText;
        _memoryCard.Points = MemoryPoints;
    }

    /// <summary>Sampler-failure handler for the Network metric: shows a neutral placeholder.</summary>
    private void OnNetworkFailed() {
        NetworkDownText = "—";
        NetworkUpText = "—";
        _networkCard.Value = "—";
    }

    /// <summary>Network subscription callback: append the download + upload rates to their buffers, then
    /// refresh the readouts.</summary>
    private void OnNetwork(NetworkSample sample) {
        MetricChannel.PushHistory(_downHistory, sample.DownMbps);
        MetricChannel.PushHistory(_upHistory, sample.UpMbps);
        UpdateNetwork(sample);
    }

    /// <summary>Updates the throughput readouts and both sparkline series, which share one auto-fitted
    /// vertical scale (<see cref="NetworkYMax"/>) so their heights are directly comparable.</summary>
    private void UpdateNetwork(NetworkSample sample) {
        // Each readout auto-scales to its own value, so a small flow shows kbps beside a large one.
        (NetworkDownText, NetworkDownUnit) = DataRateFormatter.Split(sample.DownMbps);
        (NetworkUpText, NetworkUpUnit) = DataRateFormatter.Split(sample.UpMbps);
        NetworkSubText = $"↑ {NetworkUpText} {NetworkUpUnit}";

        NetworkYMax = ChartScale.FitAxis(_downHistory, _upHistory, MinNetworkScaleMbps);
        NetworkDownPoints = SparklinePoints.Build(_downHistory, NetworkYMax);
        NetworkUpPoints = SparklinePoints.Build(_upHistory, NetworkYMax);

        _networkCard.Value = NetworkDownText;
        _networkCard.Unit = NetworkDownUnit;
        _networkCard.Sub = NetworkSubText;
        _networkCard.Points = NetworkDownPoints;
    }

    /// <summary>
    /// Enumerates the physical disks + volumes once (off the UI thread) and rebuilds the per-disk stat cards.
    /// Both providers soft-fail to empty lists, so any failure just leaves the existing cards in place.
    /// </summary>
    private async Task LoadDisksAsync() {
        try {
            var disksTask = PhysicalDiskProvider.GetAsync();
            var volumesTask = VolumeProvider.GetAsync();
            await Task.WhenAll(disksTask, volumesTask);
            RebuildDiskCards(StorageComposer.Compose(disksTask.Result, volumesTask.Result));
        } catch {
            // Leave the existing disk cards in place on a transient failure.
        }
    }

    /// <summary>Reconciles the disk cards to the current drive set: drops the old disk cards, then inserts one
    /// per drive just before the Network card (keeping the CPU→Memory→GPU→Disks→Network grouping). A disk
    /// card's caption is its capacity used; its value + sparkline (Active time) are seeded here and then driven
    /// by the throughput timer.</summary>
    private void RebuildDiskCards(IReadOnlyList<DriveCardData> drives) {
        foreach (var card in _diskCards.Values)
            Cards.Remove(card);
        _diskCards.Clear();
        _diskHistories.Clear();

        var insertAt = Cards.IndexOf(_networkCard);
        foreach (var drive in drives) {
            var card = new DashboardCard(DeviceCategory.Disk, drive.Name.ToUpperInvariant(), "%") {
                Sub = FormatCapacity(drive.UsedBytes, drive.UsedBytes + drive.FreeBytes),
            };
            Cards.Insert(insertAt++, card);
            _diskCards[drive.DiskNumber] = card;
            _diskHistories[drive.DiskNumber] = new double[WindowSeconds];
        }

        // Seed the new cards' value + charts once so they aren't blank until the next throughput tick.
        UpdateDiskThroughput();
    }

    private void OnThroughputTick(object? sender, EventArgs e) {
        UpdateDiskThroughput();
        UpdateGpuAdapters();
    }

    /// <summary>Samples each disk's active time and refreshes its card's headline value + sparkline (Task
    /// Manager's disk "Active time", 0–100 %). Disks without a current reading are left unchanged.</summary>
    private void UpdateDiskThroughput() {
        foreach (var sample in _throughputSampler.Sample()) {
            if (!_diskHistories.TryGetValue(sample.DiskNumber, out var history)
                || !_diskCards.TryGetValue(sample.DiskNumber, out var card))
                continue;
            MetricChannel.PushHistory(history, sample.ActivePercent);
            card.Value = Math.Round(sample.ActivePercent).ToString(CultureInfo.InvariantCulture);
            card.Points = SparklinePoints.Build(history, 100);
        }
    }

    /// <summary>Unsubscribes from the shared metrics and tears down the uptime + throughput timers and the
    /// per-disk sampler. The shared feed's samplers are owned (and disposed) by the service. Safe to call more
    /// than once.</summary>
    public void Dispose() {
        foreach (var subscription in _subscriptions)
            subscription.Dispose();
        _uptimeTimer.Stop();
        _uptimeTimer.Tick -= OnUptimeTick;
        _throughputTimer.Stop();
        _throughputTimer.Tick -= OnThroughputTick;
        _throughputSampler.Dispose();
        _gpuSampler.Dispose();
    }
}
