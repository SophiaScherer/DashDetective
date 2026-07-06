using System;
using System.Globalization;
using System.Text;
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
    [ObservableProperty] private string _cpuPercentText = "0%";
    [ObservableProperty] private string _cpuPoints = "";

    public DashboardViewModel() {
        // The history array starts all-zero, so the chart is full-width (flat at 0%) from
        // the first frame; real samples then shift in from the right, one per second.
        UpdateCpu(0);

        _cpuTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _cpuTimer.Tick += OnCpuTick;
        _cpuTimer.Start();
    }

    private void OnCpuTick(object? sender, EventArgs e) {
        var value = _cpuSampler.Sample();

        // Shift the window left by one and append the newest sample at the end.
        Array.Copy(_cpuHistory, 1, _cpuHistory, 0, _cpuHistory.Length - 1);
        _cpuHistory[^1] = value;

        UpdateCpu(value);
    }

    private void UpdateCpu(double value) {
        CpuPercent = value;
        CpuPercentText = $"{Math.Round(value)}%";
        CpuPoints = BuildCpuPoints();
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
