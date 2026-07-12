using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
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

    /// <summary>Cadence for re-reading adapters + IP config. Adapters change rarely (plug/unplug,
    /// connect/disconnect), so a coarse tick is plenty — like the Dashboard's 30 s uptime timer.</summary>
    private static readonly TimeSpan AdapterInterval = TimeSpan.FromSeconds(5);

    /// <summary>Cadence for the connections table. Netstat-style enumeration is heavier than a byte
    /// counter, so it polls slower than the 1 Hz throughput sampler.</summary>
    private static readonly TimeSpan ConnectionsInterval = TimeSpan.FromSeconds(2.5);

    private readonly NetworkUsageSampler _networkSampler = new();
    private readonly double[] _downHistory = new double[WindowSeconds];
    private readonly double[] _upHistory = new double[WindowSeconds];
    private readonly DispatcherTimer _networkTimer;
    private readonly DispatcherTimer _adapterTimer;
    private readonly DispatcherTimer _connectionsTimer;
    private bool _connectionsInFlight;

    [ObservableProperty] private string _downText = "0";
    [ObservableProperty] private string _upText = "0";
    [ObservableProperty] private string _downPoints = "";
    [ObservableProperty] private string _upPoints = "";
    [ObservableProperty] private double _downYMax = MinScaleMbps;
    [ObservableProperty] private double _upYMax = MinScaleMbps;

    /// <summary>The machine's network adapters (physical + virtual), for the Adapters panel.</summary>
    public ObservableCollection<AdapterInfo> Adapters { get; } = new();

    /// <summary>The primary adapter's IPv4 configuration, for the IP Configuration panel.</summary>
    [ObservableProperty] private IpConfigInfo _ipConfig = IpConfigInfo.Unknown;

    /// <summary>Active TCP/UDP connections, for the Active Connections table. Updated in place.</summary>
    public ObservableCollection<ConnectionRow> Connections { get; } = new();

    /// <summary>Count caption for the connections panel header (e.g. "42 active").</summary>
    [ObservableProperty] private string _connectionsSummary = "";

    public NetworkViewModel() {
        // Zero-filled buffers mean both charts are full-width (flat at 0) from the first frame; real
        // samples then shift in from the right, one per second.
        UpdateThroughput(new NetworkSample(0, 0));

        _networkTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _networkTimer.Tick += OnNetworkTick;
        _networkTimer.Start();

        // Adapters + IP config load once off the UI thread, then refresh on a coarse timer.
        _ = LoadAdaptersAsync();

        _adapterTimer = new DispatcherTimer { Interval = AdapterInterval };
        _adapterTimer.Tick += OnAdapterTick;
        _adapterTimer.Start();

        // Connections load once, then refresh on their own (slower) timer.
        _ = LoadConnectionsAsync();

        _connectionsTimer = new DispatcherTimer { Interval = ConnectionsInterval };
        _connectionsTimer.Tick += OnConnectionsTick;
        _connectionsTimer.Start();
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

    private void OnAdapterTick(object? sender, EventArgs e) => _ = LoadAdaptersAsync();

    /// <summary>
    /// Reads the adapters + primary IP config off the UI thread and applies the result. The provider
    /// never throws (it falls back to an empty list / <see cref="IpConfigInfo.Unknown"/>), but the
    /// whole path is guarded so a surprise can't take down the app via an unobserved task exception.
    /// The small adapter list is rebuilt wholesale — cheap and flicker-free at this size/cadence.
    /// </summary>
    private async Task LoadAdaptersAsync() {
        try {
            var snapshot = await AdapterInfoProvider.GetAsync();
            // GetAsync was awaited on the UI thread, so the continuation resumes there — safe to bind.
            Adapters.Clear();
            foreach (var adapter in snapshot.Adapters)
                Adapters.Add(adapter);
            IpConfig = snapshot.PrimaryConfig;
        } catch {
            Adapters.Clear();
            IpConfig = IpConfigInfo.Unknown;
        }
    }

    private void OnConnectionsTick(object? sender, EventArgs e) => _ = LoadConnectionsAsync();

    /// <summary>
    /// Reads the connections snapshot off the UI thread and reconciles it into <see cref="Connections"/>
    /// in place: rows that vanished are removed, survivors are updated (and moved to their new sorted
    /// position), and new rows are inserted — so the table doesn't flicker or lose scroll position.
    /// Guarded against overlap (a slow enumeration must not pile up ticks) and never throws.
    /// </summary>
    private async Task LoadConnectionsAsync() {
        if (_connectionsInFlight)
            return;
        _connectionsInFlight = true;
        try {
            var snapshot = await ConnectionsProvider.GetAsync();
            // Awaited on the UI thread, so the continuation resumes there — safe to touch the collection.
            ReconcileConnections(snapshot.Rows);
            ConnectionsSummary = BuildConnectionsSummary(snapshot.Total, snapshot.Rows.Count);
        } catch {
            Connections.Clear();
            ConnectionsSummary = "Connections unavailable";
        } finally {
            _connectionsInFlight = false;
        }
    }

    /// <summary>Header caption: the true active count, noting when the table is capped.</summary>
    private static string BuildConnectionsSummary(int total, int shown) {
        if (total == 0)
            return "No active connections";
        var count = total.ToString(CultureInfo.InvariantCulture);
        return total > shown ? $"{count} active · showing {shown}" : $"{count} active";
    }

    /// <summary>Diffs <paramref name="incoming"/> (already sorted) into the observable collection by key.</summary>
    private void ReconcileConnections(IReadOnlyList<ConnectionInfo> incoming) {
        var incomingKeys = new HashSet<string>(incoming.Count);
        foreach (var info in incoming)
            incomingKeys.Add(info.Key);

        // Drop rows that are no longer present.
        for (var i = Connections.Count - 1; i >= 0; i--)
            if (!incomingKeys.Contains(Connections[i].Key))
                Connections.RemoveAt(i);

        // Index the survivors for O(1) lookup.
        var existing = new Dictionary<string, ConnectionRow>(Connections.Count);
        foreach (var row in Connections)
            existing[row.Key] = row;

        // Walk the incoming order, placing each row at its target index.
        for (var i = 0; i < incoming.Count; i++) {
            var info = incoming[i];
            if (existing.TryGetValue(info.Key, out var row)) {
                row.Update(info);
                var current = Connections.IndexOf(row);
                if (current != i)
                    Connections.Move(current, i);
            } else {
                var created = new ConnectionRow(info);
                existing[info.Key] = created;
                Connections.Insert(i, created);
            }
        }
    }

    /// <summary>Toolbar Refresh: an immediate re-sample, adapter re-read and connections re-read. Runs
    /// even while paused (a manual refresh should still update once), matching the Dashboard.</summary>
    public void Refresh() {
        OnNetworkTick(this, EventArgs.Empty);
        _ = LoadAdaptersAsync();
        _ = LoadConnectionsAsync();
    }

    /// <summary>Pauses/resumes all of the tab's live polling. Drives the shell's Live pill;
    /// <see cref="Refresh"/> still works while paused.</summary>
    public void SetLive(bool live) {
        if (live) {
            _networkTimer.Start();
            _adapterTimer.Start();
            _connectionsTimer.Start();
        } else {
            _networkTimer.Stop();
            _adapterTimer.Stop();
            _connectionsTimer.Stop();
        }
    }

    /// <summary>Stops the timers. Safe to call more than once. The network sampler is fully managed,
    /// so it needs no disposal.</summary>
    public void Dispose() {
        _networkTimer.Stop();
        _networkTimer.Tick -= OnNetworkTick;
        _adapterTimer.Stop();
        _adapterTimer.Tick -= OnAdapterTick;
        _connectionsTimer.Stop();
        _connectionsTimer.Tick -= OnConnectionsTick;
    }
}
