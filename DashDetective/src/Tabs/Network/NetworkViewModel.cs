using System;
using System.Globalization;
using System.Text;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using DashDetective.Services.Network;
using DashDetective.Shared;

namespace DashDetective.Tabs.Network;

/// <summary>
/// The Network tab: adapters, throughput, connections and diagnostics. Like the Dashboard it is
/// always-on — constructed once by the shell and left running for the app's lifetime — so it
/// implements <see cref="IRefreshablePage"/> (toolbar Refresh), <see cref="ILiveSamplingPage"/>
/// (toolbar Live pill) and <see cref="IDisposable"/>.
///
/// Throughput mirrors the Dashboard's sampler + 1 Hz timer + 60-sample rolling-buffer pattern, but
/// the design comp shows download and upload as TWO stacked charts, so each series keeps its OWN
/// dynamic scale (<see cref="DownYMax"/>/<see cref="UpYMax"/>) rather than the Dashboard's single
/// shared scale. Other panels (adapters, connections, ping, DNS) are wired in later phases.
/// </summary>
public partial class NetworkViewModel : ViewModelBase, IRefreshablePage, ILiveSamplingPage, IDisposable {
    /// <summary>Width of the rolling throughput history, in seconds (one sample per second).</summary>
    private const int WindowSeconds = 60;

    /// <summary>Floor for a series' vertical scale so idle traffic isn't drawn as a huge spike.</summary>
    private const double MinScaleMbps = 1.0;

    private readonly NetworkUsageSampler _networkSampler = new();
    private readonly double[] _downHistory = new double[WindowSeconds];
    private readonly double[] _upHistory = new double[WindowSeconds];
    private readonly DispatcherTimer _networkTimer;

    [ObservableProperty] private string _downText = "0";
    [ObservableProperty] private string _upText = "0";
    [ObservableProperty] private string _downPoints = "";
    [ObservableProperty] private string _upPoints = "";
    [ObservableProperty] private double _downYMax = MinScaleMbps;
    [ObservableProperty] private double _upYMax = MinScaleMbps;

    public NetworkViewModel() {
        // Zero-filled buffers mean both charts are full-width (flat at 0) from the first frame; real
        // samples then shift in from the right, one per second.
        UpdateThroughput(new NetworkSample(0, 0));

        _networkTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _networkTimer.Tick += OnNetworkTick;
        _networkTimer.Start();
    }

    private void OnNetworkTick(object? sender, EventArgs e) {
        NetworkSample sample;
        try {
            sample = _networkSampler.Sample();
        } catch {
            // Sampling is unavailable (e.g. no readable adapters). Show a neutral placeholder and
            // stop polling rather than throwing on the UI thread every second.
            DownText = "—";
            UpText = "—";
            _networkTimer.Stop();
            return;
        }

        // Shift both windows left by one and append the newest down/up rates at the end.
        Array.Copy(_downHistory, 1, _downHistory, 0, _downHistory.Length - 1);
        _downHistory[^1] = sample.DownMbps;
        Array.Copy(_upHistory, 1, _upHistory, 0, _upHistory.Length - 1);
        _upHistory[^1] = sample.UpMbps;

        UpdateThroughput(sample);
    }

    /// <summary>
    /// Updates both readouts and both sparkline series. Unlike the Dashboard's single shared scale,
    /// download and upload each auto-fit to their OWN 60-second window (the comp draws them as two
    /// separate stacked charts), with headroom and a floor.
    /// </summary>
    private void UpdateThroughput(NetworkSample sample) {
        DownText = FormatMbps(sample.DownMbps);
        UpText = FormatMbps(sample.UpMbps);

        DownYMax = ComputeScale(_downHistory);
        UpYMax = ComputeScale(_upHistory);
        DownPoints = BuildPoints(_downHistory, DownYMax);
        UpPoints = BuildPoints(_upHistory, UpYMax);
    }

    /// <summary>Upper bound for one series: its largest sample plus ~15 % headroom so the peak line
    /// doesn't touch the top edge, clamped to <see cref="MinScaleMbps"/>.</summary>
    private static double ComputeScale(double[] history) {
        var max = 0.0;
        for (var i = 0; i < history.Length; i++)
            if (history[i] > max) max = history[i];

        var scaled = max * 1.15;
        return scaled > MinScaleMbps ? scaled : MinScaleMbps;
    }

    /// <summary>Formats a rate for the readout: whole Mbps at ≥ 10, one decimal below (e.g. "93", "2.7").</summary>
    private static string FormatMbps(double mbps) {
        if (mbps < 0)
            mbps = 0;
        return mbps >= 10
            ? Math.Round(mbps).ToString(CultureInfo.InvariantCulture)
            : mbps.ToString("F1", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Renders a throughput history as a Sparkline "x,y" string against <paramref name="yMax"/>: x is
    /// the sample index; y is <c>yMax − value</c> so higher throughput sits at the top (smaller y =
    /// top), paired with a fixed 0–<paramref name="yMax"/> axis on the Sparkline.
    /// </summary>
    private static string BuildPoints(double[] history, double yMax) {
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

    /// <summary>Toolbar Refresh: an immediate re-sample. Runs even while paused (a manual refresh
    /// should still update once), matching the Dashboard.</summary>
    public void Refresh() => OnNetworkTick(this, EventArgs.Empty);

    /// <summary>Pauses/resumes live throughput sampling. Drives the shell's Live pill;
    /// <see cref="Refresh"/> still works while paused.</summary>
    public void SetLive(bool live) {
        if (live)
            _networkTimer.Start();
        else
            _networkTimer.Stop();
    }

    /// <summary>Stops the sampling timer. Safe to call more than once. The network sampler is fully
    /// managed, so it needs no disposal.</summary>
    public void Dispose() {
        _networkTimer.Stop();
        _networkTimer.Tick -= OnNetworkTick;
    }
}
