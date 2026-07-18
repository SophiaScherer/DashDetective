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
/// Wired live so far: <b>CPU</b>. Memory / Disk / GPU / Ethernet still show static mock data until their
/// own phases. Implements <see cref="IRefreshablePage"/> (toolbar Refresh), <see cref="ILiveSamplingPage"/>
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

        // Memory / Disk / GPU / Ethernet — static mock data from the design comp (perfDefs + statMap),
        // wired to real samplers in later phases. Each mock series is a deterministic wave.
        Resources.Add(new ResourceRow("Memory", "19.5 / 32 GB", "DDR5-6000 · 2 slots",
                                      "61", "%", Brush("#c58fff"), MockPoints(61, 6, 11),
                                      Stats("In use", "19.5 GB", "Available", "12.5 GB",
                                            "Cached", "5.8 GB", "Committed", "24 / 38 GB"), Select));
        Resources.Add(new ResourceRow("Disk 0 (C:)", "NVMe SSD", "Samsung 990 Pro 2TB",
                                      "4", "%", Brush("#ffcf4d"), MockPoints(9, 18, 5),
                                      Stats("Active", "4 %", "Read", "48 MB/s",
                                            "Write", "12 MB/s", "Response", "0.4 ms"), Select));
        Resources.Add(new ResourceRow("GPU", "RTX 4080", "16 GB GDDR6X · 52°C",
                                      "12", "%", Brush("#6ccb5f"), MockPoints(14, 12, 23),
                                      Stats("3D", "12 %", "VRAM", "4.2 / 16 GB",
                                            "Temp", "52 °C", "Power", "142 W"), Select));
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

        // Load static CPU hardware info off the UI thread; the sub/spec labels fill in when ready.
        _ = LoadCpuInfoAsync();
    }

    /// <summary>Toolbar Refresh: an immediate re-sample of every live metric, even while paused.</summary>
    public void Refresh() {
        OnCpuTick(this, EventArgs.Empty);
        _ = LoadCpuInfoAsync();
    }

    /// <summary>Pauses or resumes all live sampling by stopping/starting the metric timers. Drives the
    /// shell's Live toggle; <see cref="Refresh"/> still works while paused.</summary>
    public void SetLive(bool live) {
        if (live)
            _cpuTimer.Start();
        else
            _cpuTimer.Stop();
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

    /// <summary>Stops the sampling timers. Safe to call more than once. The CPU sampler is fully
    /// managed (no unmanaged handle), so it needs no disposal.</summary>
    public void Dispose() {
        _cpuTimer.Stop();
        _cpuTimer.Tick -= OnCpuTick;
    }
}
