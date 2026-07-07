using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using DashDetective.Shared;

namespace DashDetective.Tabs.Dashboard;

/// <summary>
/// View model for the Dashboard page. Currently drives the live CPU surfaces; the other
/// metrics remain static placeholders in the view until they are implemented.
/// </summary>
public partial class DashboardViewModel : ViewModelBase, IDisposable {
    /// <summary>Width of the rolling CPU history, in seconds (one sample per second).</summary>
    private const int WindowSeconds = 60;

    private readonly CpuUsageSampler _cpuSampler = new();
    private readonly double[] _cpuHistory = new double[WindowSeconds];
    private readonly DispatcherTimer _cpuTimer;

    private readonly MemoryUsageSampler _memorySampler = new();
    private readonly double[] _memoryHistory = new double[WindowSeconds];
    private readonly DispatcherTimer _memoryTimer;

    [ObservableProperty] private double _cpuPercent;
    [ObservableProperty] private string _cpuValueText = "0";
    [ObservableProperty] private string _cpuPercentText = "0%";
    [ObservableProperty] private string _cpuPoints = "";
    [ObservableProperty] private string _cpuModelShort = "";
    [ObservableProperty] private string _cpuModelText = "";
    [ObservableProperty] private string _cpuCoresText = "";

    [ObservableProperty] private string _memoryValueText = "0";
    [ObservableProperty] private string _memorySubText = "";
    [ObservableProperty] private string _memoryPoints = "";

    public DashboardViewModel() {
        // The history array starts all-zero, so the chart is full-width (flat at 0%) from
        // the first frame; real samples then shift in from the right, one per second.
        UpdateCpu(0);

        _cpuTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _cpuTimer.Tick += OnCpuTick;
        _cpuTimer.Start();

        // Memory runs on its own timer so it stays independent of the CPU sampler: either can
        // fail and stop without taking the other down.
        UpdateMemory(_memorySampler.Sample());

        _memoryTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _memoryTimer.Tick += OnMemoryTick;
        _memoryTimer.Start();

        // Load static CPU hardware info off the UI thread; results are applied when ready.
        _ = LoadCpuInfoAsync();
    }

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

    private void OnCpuTick(object? sender, EventArgs e) {
        double value;
        try {
            value = _cpuSampler.Sample();
        } catch {
            // Sampling is unavailable (e.g. a non-Windows host). Show a neutral placeholder
            // and stop polling rather than throwing on the UI thread every second.
            CpuValueText = "—";
            CpuPercentText = "—";
            _cpuTimer.Stop();
            return;
        }

        // Shift the window left by one and append the newest sample at the end.
        Array.Copy(_cpuHistory, 1, _cpuHistory, 0, _cpuHistory.Length - 1);
        _cpuHistory[^1] = value;

        UpdateCpu(value);
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
    private string BuildCpuPoints() {
        var sb = new StringBuilder(_cpuHistory.Length * 8);
        for (var i = 0; i < _cpuHistory.Length; i++) {
            if (i > 0)
                sb.Append(' ');
            sb.Append(i.ToString(CultureInfo.InvariantCulture));
            sb.Append(',');
            sb.Append((100 - _cpuHistory[i]).ToString("0.##", CultureInfo.InvariantCulture));
        }

        return sb.ToString();
    }

    private void OnMemoryTick(object? sender, EventArgs e) {
        MemorySample sample;
        try {
            sample = _memorySampler.Sample();
        } catch {
            // Sampling is unavailable (e.g. a non-Windows host). Show a neutral placeholder and
            // stop polling rather than throwing on the UI thread every second.
            MemoryValueText = "—";
            MemorySubText = "";
            _memoryTimer.Stop();
            return;
        }

        // Shift the window left by one and append the newest load percentage at the end.
        Array.Copy(_memoryHistory, 1, _memoryHistory, 0, _memoryHistory.Length - 1);
        _memoryHistory[^1] = sample.LoadPercent;

        UpdateMemory(sample);
    }

    private void UpdateMemory(MemorySample sample) {
        var usedGb = sample.UsedBytes / (double)(1L << 30);
        var totalGb = sample.TotalBytes / (double)(1L << 30);
        var rounded = Math.Round(sample.LoadPercent);

        MemoryValueText = usedGb.ToString("F1", CultureInfo.InvariantCulture);
        MemorySubText = totalGb > 0
            ? $"{rounded.ToString(CultureInfo.InvariantCulture)}% of {totalGb.ToString("F0", CultureInfo.InvariantCulture)} GB"
            : "";
        MemoryPoints = BuildMemoryPoints();
    }

    /// <summary>
    /// Renders the memory history as a Sparkline "x,y" string, matching <see cref="BuildCpuPoints"/>:
    /// x is the sample index; y is <c>100 − load</c> so higher usage sits at the top, paired with a
    /// fixed 0–100 axis on the Sparkline.
    /// </summary>
    private string BuildMemoryPoints() {
        var sb = new StringBuilder(_memoryHistory.Length * 8);
        for (var i = 0; i < _memoryHistory.Length; i++) {
            if (i > 0)
                sb.Append(' ');
            sb.Append(i.ToString(CultureInfo.InvariantCulture));
            sb.Append(',');
            sb.Append((100 - _memoryHistory[i]).ToString("0.##", CultureInfo.InvariantCulture));
        }

        return sb.ToString();
    }

    /// <summary>Stops the sampling timers. Safe to call more than once.</summary>
    public void Dispose() {
        _cpuTimer.Stop();
        _cpuTimer.Tick -= OnCpuTick;
        _memoryTimer.Stop();
        _memoryTimer.Tick -= OnMemoryTick;
    }
}
