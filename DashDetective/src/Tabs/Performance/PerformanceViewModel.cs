using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using DashDetective.Services.SystemMetrics;
using DashDetective.Shared;
using DashDetective.Shared.Charts;
using DashDetective.Tabs.Dashboard;

namespace DashDetective.Tabs.Performance;

/// <summary>
/// The Performance tab: a Task-Manager-style live resource drill-down. A left resource-selector rail
/// swaps a right detail pane (one large utilization chart + a stat-tile strip).
///
/// Live sampling mirrors <see cref="DashboardViewModel"/>: one <see cref="DispatcherTimer"/> per metric
/// (1 Hz) feeds a <c>double[60]</c> rolling history that renders to the shared <c>Sparkline</c> via
/// <see cref="SparklinePoints"/>, and results are pushed into the selected resource's <see cref="ResourceRow"/>.
/// The tab keeps its own sampler instances (like the Processes tab) rather than sharing the Dashboard's.
///
/// Wired live so far: <b>CPU</b>, <b>Memory</b>, <b>Disk</b>, <b>GPU</b>. Ethernet still shows static mock
/// data until its phase. Implements <see cref="IRefreshablePage"/> (toolbar Refresh), <see cref="ILiveSamplingPage"/>
/// (toolbar Live/Pause) and <see cref="IDisposable"/>; <see cref="ISelfScrollingPage"/> keeps the shell
/// hosting it in the bounded, non-scrolling container (it manages its own panes, like File Explorer).
/// </summary>
public partial class PerformanceViewModel : ViewModelBase,
        IRefreshablePage, ILiveSamplingPage, ISelfScrollingPage, IDisposable {
    /// <summary>Width of every rolling metric history, in seconds (one sample per second).</summary>
    private const int WindowSeconds = 60;

    // Fixed semantic per-metric legend colours (theme/accent-independent by design), matching the design
    // comp's palette — parsed like MainWindowViewModel's live dots.
    private static IBrush Brush(string hex) => new SolidColorBrush(Color.Parse(hex));

    // The four label/value readouts for a still-mock resource's detail stat strip (design comp's statMap).
    private static StatTile[] Stats(string l1, string v1, string l2, string v2,
                                    string l3, string v3, string l4, string v4) =>
        new[] { new StatTile(l1, v1), new StatTile(l2, v2), new StatTile(l3, v3), new StatTile(l4, v4) };

    /// <summary>The resource rows shown in the left rail, in display order.</summary>
    public ObservableCollection<ResourceRow> Resources { get; } = new();

    /// <summary>The currently selected resource, whose detail the right pane shows.</summary>
    [ObservableProperty] private ResourceRow _selectedResource = null!;

    // ---- CPU (live) ----
    private readonly CpuUsageSampler _cpuSampler = new();
    private readonly double[] _cpuHistory = new double[WindowSeconds];
    private readonly DispatcherTimer _cpuTimer;
    private readonly ResourceRow _cpuRow;
    private readonly StatTile _cpuUtilTile;
    private readonly StatTile _cpuProcessesTile;
    private readonly StatTile _cpuUptimeTile;

    // ---- Memory (live) ----
    private readonly MemoryUsageSampler _memorySampler = new();
    private readonly double[] _memoryHistory = new double[WindowSeconds];
    private readonly DispatcherTimer _memoryTimer;
    private readonly ResourceRow _memoryRow;
    private readonly StatTile _memInUseTile;
    private readonly StatTile _memAvailableTile;
    private readonly StatTile _memCommittedTile;

    // ---- Disk (live) ----
    private readonly StorageUsageSampler _storageSampler = new();
    private readonly double[] _storageHistory = new double[WindowSeconds];
    private readonly DispatcherTimer _storageTimer;
    private readonly ResourceRow _diskRow;
    private readonly StatTile _diskActiveTile;
    private readonly StatTile _diskReadTile;
    private readonly StatTile _diskWriteTile;
    private readonly StatTile _diskResponseTile;

    // ---- GPU (live) ----
    private readonly GpuUsageSampler _gpuSampler = new();
    private readonly double[] _gpuHistory = new double[WindowSeconds];
    private readonly DispatcherTimer _gpuTimer;
    private readonly ResourceRow _gpuRow;
    private readonly StatTile _gpu3dTile;

    public PerformanceViewModel() {
        // CPU — live. Tiles: Utilization / Processes / Up time update every tick; Speed is blanked to
        // "—" (no reliable current-clock source, and the base clock already appears in the sub-label).
        _cpuUtilTile = new StatTile("Utilization", "0 %");
        _cpuProcessesTile = new StatTile("Processes", "0");
        _cpuUptimeTile = new StatTile("Up time", "0m");
        _cpuRow = new ResourceRow("CPU", "", "", "0", "%", Brush("#4cc2ff"),
                                  SparklinePoints.Build(_cpuHistory, 100),
                                  new[] {
                                      _cpuUtilTile, new StatTile("Speed", "—"),
                                      _cpuProcessesTile, _cpuUptimeTile,
                                  }, Select);
        Resources.Add(_cpuRow);

        // Memory — live. Tiles: In use / Available / Committed update every tick; Cached is blanked to
        // "—" (no reliable source without adding a PDH counter to the pure-Win32 memory sampler).
        _memInUseTile = new StatTile("In use", "0 GB");
        _memAvailableTile = new StatTile("Available", "0 GB");
        _memCommittedTile = new StatTile("Committed", "0 / 0 GB");
        _memoryRow = new ResourceRow("Memory", "", "", "0", "%", Brush("#c58fff"),
                                     SparklinePoints.Build(_memoryHistory, 100),
                                     new[] {
                                         _memInUseTile, _memAvailableTile,
                                         new StatTile("Cached", "—"), _memCommittedTile,
                                     }, Select);
        Resources.Add(_memoryRow);

        // Disk — live. Rail value + chart show Task Manager's disk "Active time"; tiles show Active /
        // Read / Write / Response, all sampled from the aggregate _Total PhysicalDisk counters.
        _diskActiveTile = new StatTile("Active", "0 %");
        _diskReadTile = new StatTile("Read", "0 MB/s");
        _diskWriteTile = new StatTile("Write", "0 MB/s");
        _diskResponseTile = new StatTile("Response", "0 ms");
        _diskRow = new ResourceRow("Disk 0 (C:)", "", "", "0", "%", Brush("#ffcf4d"),
                                   SparklinePoints.Build(_storageHistory, 100),
                                   new[] {
                                       _diskActiveTile, _diskReadTile, _diskWriteTile, _diskResponseTile,
                                   }, Select);
        Resources.Add(_diskRow);

        // GPU — live. Rail value + chart show the busiest GPU engine's utilization; 3D tile mirrors it.
        // VRAM / Temp / Power are blanked to "—": no reliable standard Windows source (GPU temperature
        // is already deferred out of scope in the project).
        _gpu3dTile = new StatTile("3D", "0 %");
        _gpuRow = new ResourceRow("GPU", "", "", "0", "%", Brush("#6ccb5f"),
                                  SparklinePoints.Build(_gpuHistory, 100),
                                  new[] {
                                      _gpu3dTile, new StatTile("VRAM", "—"),
                                      new StatTile("Temp", "—"), new StatTile("Power", "—"),
                                  }, Select);
        Resources.Add(_gpuRow);

        // Ethernet — static mock data from the design comp (perfDefs + statMap), wired to a real sampler
        // in the next phase. The mock series is a deterministic wave.
        Resources.Add(new ResourceRow("Ethernet", "2.5 GbE", "Intel I225-V",
                                      "48", "Mbps", Brush("#4cc2ff"), MockPoints(40, 26, 7),
                                      Stats("Receive", "48 Mbps", "Send", "3.8 Mbps",
                                            "Link", "2.5 Gbps", "Errors", "0"), Select));

        SelectedResource = Resources[0];
        SelectedResource.IsSelected = true;

        // Seed the CPU surfaces once so they're correct on the first frame, then sample every second.
        UpdateCpu(0);
        UpdateCpuProcesses();
        UpdateCpuUptime();

        _cpuTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _cpuTimer.Tick += OnCpuTick;
        _cpuTimer.Start();

        // Memory runs on its own timer so it stays independent of the CPU sampler: either can fail and
        // stop without taking the other down.
        UpdateMemory(_memorySampler.Sample());

        _memoryTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _memoryTimer.Tick += OnMemoryTick;
        _memoryTimer.Start();

        // Disk runs on its own timer for the same reason: independent of the other samplers.
        UpdateStorage(_storageSampler.Sample());

        _storageTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _storageTimer.Tick += OnStorageTick;
        _storageTimer.Start();

        // GPU runs on its own timer for the same reason: independent of the other samplers.
        UpdateGpu(0);

        _gpuTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _gpuTimer.Tick += OnGpuTick;
        _gpuTimer.Start();

        // Load static hardware info off the UI thread; the sub/spec labels fill in when ready.
        _ = LoadCpuInfoAsync();
        _ = LoadMemoryInfoAsync();
        _ = LoadDiskInfoAsync();
        _ = LoadGpuInfoAsync();
    }

    /// <summary>Toolbar Refresh: an immediate re-sample of every live metric, even while paused.</summary>
    public void Refresh() {
        OnCpuTick(this, EventArgs.Empty);
        OnMemoryTick(this, EventArgs.Empty);
        OnStorageTick(this, EventArgs.Empty);
        OnGpuTick(this, EventArgs.Empty);
        _ = LoadCpuInfoAsync();
        _ = LoadMemoryInfoAsync();
        _ = LoadDiskInfoAsync();
        _ = LoadGpuInfoAsync();
    }

    /// <summary>Pauses or resumes all live sampling by stopping/starting the metric timers. Drives the
    /// shell's Live toggle; <see cref="Refresh"/> still works while paused.</summary>
    public void SetLive(bool live) {
        if (live) {
            _cpuTimer.Start();
            _memoryTimer.Start();
            _storageTimer.Start();
            _gpuTimer.Start();
        } else {
            _cpuTimer.Stop();
            _memoryTimer.Stop();
            _storageTimer.Stop();
            _gpuTimer.Stop();
        }
    }

    private void OnCpuTick(object? sender, EventArgs e) {
        double value;
        try {
            value = _cpuSampler.Sample();
        } catch {
            // Sampling is unavailable (e.g. a non-Windows host). Show a neutral placeholder and stop
            // polling rather than throwing on the UI thread every second.
            _cpuRow.ValueText = "—";
            _cpuUtilTile.Value = "—";
            _cpuTimer.Stop();
            return;
        }

        // Shift the window left by one and append the newest sample at the end.
        Array.Copy(_cpuHistory, 1, _cpuHistory, 0, _cpuHistory.Length - 1);
        _cpuHistory[^1] = value;

        UpdateCpu(value);
        UpdateCpuProcesses();
        UpdateCpuUptime();
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
            // Constructed on the UI thread, so the continuation resumes there — safe to bind.
            _cpuRow.Sub = FormatCpuSub(info);
            _cpuRow.Spec = ShortenCpuName(info.Name);
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

    /// <summary>
    /// Trims WMI decoration ("(R)", "(TM)", "N-Core Processor", "CPU @ …GHz") from a processor name for
    /// the compact spec header, e.g. "AMD Ryzen 5 7600X 6-Core Processor" → "AMD Ryzen 5 7600X".
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

    private void OnMemoryTick(object? sender, EventArgs e) {
        MemorySample sample;
        try {
            sample = _memorySampler.Sample();
        } catch {
            // Sampling is unavailable (e.g. a non-Windows host). Show neutral placeholders and stop
            // polling rather than throwing on the UI thread every second.
            _memoryRow.ValueText = "—";
            _memInUseTile.Value = "—";
            _memAvailableTile.Value = "—";
            _memCommittedTile.Value = "—";
            _memoryTimer.Stop();
            return;
        }

        // Shift the window left by one and append the newest load percentage at the end.
        Array.Copy(_memoryHistory, 1, _memoryHistory, 0, _memoryHistory.Length - 1);
        _memoryHistory[^1] = sample.LoadPercent;

        UpdateMemory(sample);
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
            // Constructed on the UI thread, so the continuation resumes there — safe to bind.
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

    private void OnStorageTick(object? sender, EventArgs e) {
        StorageSample sample;
        try {
            sample = _storageSampler.Sample();
        } catch {
            // Sampling is unavailable (e.g. a non-Windows host or missing PhysicalDisk counters). Show
            // neutral placeholders and stop polling rather than throwing on the UI thread every second.
            _diskRow.ValueText = "—";
            _diskActiveTile.Value = "—";
            _diskReadTile.Value = "—";
            _diskWriteTile.Value = "—";
            _diskResponseTile.Value = "—";
            _storageTimer.Stop();
            return;
        }

        // Shift the window left by one and append the newest activity sample at the end.
        Array.Copy(_storageHistory, 1, _storageHistory, 0, _storageHistory.Length - 1);
        _storageHistory[^1] = sample.ActivePercent;

        UpdateStorage(sample);
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
            // Constructed on the UI thread, so the continuation resumes there — safe to bind.
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

    private void OnGpuTick(object? sender, EventArgs e) {
        double value;
        try {
            value = _gpuSampler.Sample();
        } catch {
            // Sampling is unavailable (e.g. a non-Windows host or missing GPU Engine counters). Show a
            // neutral placeholder and stop polling rather than throwing on the UI thread every second.
            _gpuRow.ValueText = "—";
            _gpu3dTile.Value = "—";
            _gpuTimer.Stop();
            return;
        }

        // Shift the window left by one and append the newest sample at the end.
        Array.Copy(_gpuHistory, 1, _gpuHistory, 0, _gpuHistory.Length - 1);
        _gpuHistory[^1] = value;

        UpdateGpu(value);
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
            // Constructed on the UI thread, so the continuation resumes there — safe to bind.
            _gpuRow.Sub = ShortenGpuName(info.Name);
            _gpuRow.Spec = info.Name;
        } catch {
            _gpuRow.Sub = "";
            _gpuRow.Spec = "Unknown GPU";
        }
    }

    /// <summary>
    /// Trims vendor decoration ("NVIDIA", "AMD", "(R)", "(TM)") from an adapter name for the compact
    /// rail sub-label, e.g. "NVIDIA GeForce RTX 3060" → "GeForce RTX 3060".
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

    /// <summary>
    /// Builds a 60-point mock utilization series as a Sparkline "x,y x,y …" string for a fixed 0–100
    /// axis. Values oscillate deterministically around <paramref name="level"/> (± ~<paramref
    /// name="amp"/>); each y is flipped to <c>100 − value</c> so higher utilization draws at the top.
    /// Placeholder for the resources not yet wired to live samplers.
    /// </summary>
    private static string MockPoints(double level, double amp, double seed) {
        var sb = new StringBuilder();
        for (var i = 0; i < 60; i++) {
            var value = level
                        + amp * Math.Sin((i + seed) * 0.40)
                        + amp * 0.5 * Math.Sin((i + seed) * 0.13);
            value = Math.Clamp(value, 0, 100);
            if (i > 0)
                sb.Append(' ');
            sb.Append(i.ToString(CultureInfo.InvariantCulture));
            sb.Append(',');
            sb.Append((100 - value).ToString("0.##", CultureInfo.InvariantCulture));
        }
        return sb.ToString();
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

    /// <summary>Stops the sampling timers and disposes samplers that own unmanaged handles. Safe to call
    /// more than once. The CPU and Memory samplers are fully managed, so they need no disposal; the
    /// Storage and GPU samplers own PDH query handles and are disposed here.</summary>
    public void Dispose() {
        _cpuTimer.Stop();
        _cpuTimer.Tick -= OnCpuTick;
        _memoryTimer.Stop();
        _memoryTimer.Tick -= OnMemoryTick;
        _storageTimer.Stop();
        _storageTimer.Tick -= OnStorageTick;
        _gpuTimer.Stop();
        _gpuTimer.Tick -= OnGpuTick;
        _storageSampler.Dispose();
        _gpuSampler.Dispose();
    }
}
