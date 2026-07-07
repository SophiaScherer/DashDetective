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
public partial class DashboardViewModel : ViewModelBase {
    /// <summary>Width of the rolling CPU history, in seconds (one sample per second).</summary>
    private const int WindowSeconds = 60;

    private readonly CpuUsageSampler _cpuSampler = new();
    private readonly double[] _cpuHistory = new double[WindowSeconds];
    private readonly DispatcherTimer _cpuTimer;

    [ObservableProperty] private double _cpuPercent;
    [ObservableProperty] private string _cpuValueText = "0";
    [ObservableProperty] private string _cpuPercentText = "0%";
    [ObservableProperty] private string _cpuPoints = "";
    [ObservableProperty] private string _cpuModelShort = "";

    public DashboardViewModel() {
        // The history array starts all-zero, so the chart is full-width (flat at 0%) from
        // the first frame; real samples then shift in from the right, one per second.
        UpdateCpu(0);

        _cpuTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _cpuTimer.Tick += OnCpuTick;
        _cpuTimer.Start();

        // Load static CPU hardware info off the UI thread; results are applied when ready.
        _ = LoadCpuInfoAsync();
    }

    private async Task LoadCpuInfoAsync() {
        var info = await CpuInfoProvider.GetAsync();
        // Constructed on the UI thread, so the continuation resumes there — safe to bind.
        CpuModelShort = ShortenCpuName(info.Name);
    }

    private void OnCpuTick(object? sender, EventArgs e) {
        var value = _cpuSampler.Sample();

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
}
