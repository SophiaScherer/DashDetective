using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashDetective.Services.Network;
using DashDetective.Shared;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;

namespace DashDetective.Tabs.Network;

/// <summary>
/// The Network tab: adapters, throughput, connections and diagnostics. Like the Dashboard it is
/// always-on — constructed once by the shell and left running for the app's lifetime — so it
/// implements <see cref="IRefreshablePage"/> (toolbar Refresh), <see cref="ILiveSamplingPage"/>
/// (toolbar Live pill) and <see cref="IDisposable"/>.
///
/// Throughput mirrors the Dashboard's sampler + 1 Hz timer + 60-sample rolling-buffer pattern. The
/// design comp shows download and upload as TWO stacked charts, but they share ONE dynamic scale
/// (<see cref="ThroughputYMax"/>, the peak of both windows) so their heights are directly comparable
/// — a bigger rate always draws taller, whichever direction it's in. Other panels (adapters,
/// connections, ping, DNS) are wired in later phases.
/// </summary>
public partial class NetworkViewModel : ViewModelBase, IRefreshablePage, ILiveSamplingPage, IDisposable {
    /// <summary>Width of the rolling throughput history, in seconds (one sample per second).</summary>
    private const int WindowSeconds = 60;

    /// <summary>Floor for a series' vertical scale so idle traffic isn't drawn as a huge spike.</summary>
    private const double MinScaleMbps = 1.0;

    /// <summary>Fixed rows per connections page; users move through pages with the numbered pager.</summary>
    private const int PageSize = 100;

    /// <summary>Cadence for re-reading adapters + IP config. Adapters change rarely (plug/unplug,
    /// connect/disconnect), so a coarse tick is plenty — like the Dashboard's 30 s uptime timer.</summary>
    private static readonly TimeSpan AdapterInterval = TimeSpan.FromSeconds(5);

    /// <summary>Cadence for the connections table. Netstat-style enumeration is heavier than a byte
    /// counter, so it polls slower than the 1 Hz throughput sampler.</summary>
    private static readonly TimeSpan ConnectionsInterval = TimeSpan.FromSeconds(2.5);

    /// <summary>Cadence for the ping diagnostics (one ping every couple of seconds, like a console
    /// <c>ping -t</c>). Longer than the 1.5 s ping timeout so sends never overlap.</summary>
    private static readonly TimeSpan PingInterval = TimeSpan.FromSeconds(2);

    private readonly NetworkUsageSampler _networkSampler = new();
    private readonly double[] _downHistory = new double[WindowSeconds];
    private readonly double[] _upHistory = new double[WindowSeconds];
    private readonly DispatcherTimer _networkTimer;
    private readonly DispatcherTimer _adapterTimer;
    private readonly DispatcherTimer _connectionsTimer;
    private readonly DispatcherTimer _pingTimer;
    private readonly PingMonitor _pingMonitor = new();
    private bool _connectionsInFlight;
    private bool _pingInFlight;

    /// <summary>The latest full (sorted) snapshot; the UI only ever binds one page-sized slice of it.</summary>
    private readonly List<ConnectionInfo> _allConnections = new();
    /// <summary>True active count from the last snapshot (may exceed <see cref="_allConnections"/> if capped).</summary>
    private int _connectionsTotal;
    /// <summary>Current 1-based page.</summary>
    private int _currentPage = 1;

    [ObservableProperty] private string _downText = "0";
    [ObservableProperty] private string _upText = "0";
    [ObservableProperty] private string _downPoints = "";
    [ObservableProperty] private string _upPoints = "";

    /// <summary>Shared upper bound for BOTH charts, so equal pixel height means equal Mbps.</summary>
    [ObservableProperty] private double _throughputYMax = MinScaleMbps;

    /// <summary>The shared scale as a caption (e.g. "peak 12 Mbps"), so the ceiling is visible.</summary>
    [ObservableProperty] private string _throughputScaleText = "";

    /// <summary>The download readout's unit ("kbps"/"Mbps"/"Gbps"), auto-scaled from its own value.</summary>
    [ObservableProperty] private string _downUnit = "Mbps";

    /// <summary>The upload readout's unit ("kbps"/"Mbps"/"Gbps"), auto-scaled from its own value.</summary>
    [ObservableProperty] private string _upUnit = "Mbps";

    /// <summary>The machine's network adapters (physical + virtual), for the Adapters panel.</summary>
    public ObservableCollection<AdapterInfo> Adapters { get; } = new();

    /// <summary>The primary adapter's IPv4 configuration, for the IP Configuration panel.</summary>
    [ObservableProperty] private IpConfigInfo _ipConfig = IpConfigInfo.Unknown;

    /// <summary>Active TCP/UDP connections for the CURRENT page, for the table. Updated in place.</summary>
    public ObservableCollection<ConnectionRow> Connections { get; } = new();

    /// <summary>Count caption for the connections panel header (e.g. "142 active · page 2 of 3").</summary>
    [ObservableProperty] private string _connectionsSummary = "";

    /// <summary>Google-style pager items (Prev · 1 … 4 5 6 … 20 · Next). Empty when there's one page.</summary>
    public ObservableCollection<PageLink> PageLinks { get; } = new();

    /// <summary>Whether to show the pager row (only when the list spans more than one page).</summary>
    [ObservableProperty] private bool _pagerVisible;

    /// <summary>The ping target, editable in the Ping panel. Applied via <see cref="ApplyPingTargetCommand"/>.</summary>
    [ObservableProperty] private string _pingTarget = PingMonitor.DefaultTarget;

    /// <summary>Console-style ping output (last few reply lines).</summary>
    [ObservableProperty] private string _pingConsole = "";

    /// <summary>Rolling average-RTT / packet-loss summary line.</summary>
    [ObservableProperty] private string _pingSummary = "";

    /// <summary>The DNS lookup host, editable in the DNS panel. Applied via <see cref="LookupDnsCommand"/>.</summary>
    [ObservableProperty] private string _dnsHost = DnsLookupProvider.DefaultHost;

    /// <summary>Console-style DNS output (name + resolved addresses).</summary>
    [ObservableProperty] private string _dnsConsole = "";

    /// <summary>DNS footer line (timing + record type, or a failure note).</summary>
    [ObservableProperty] private string _dnsFooter = "";

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

        // Ping the fixed target continuously; kick one off now so the panel isn't blank on arrival.
        _ = RunPingAsync();

        _pingTimer = new DispatcherTimer { Interval = PingInterval };
        _pingTimer.Tick += OnPingTick;
        _pingTimer.Start();

        // DNS is a one-shot lookup (not a live loop): resolve once now, and again on Refresh.
        _ = LoadDnsAsync();
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
    /// Updates both readouts and both sparkline series. Download and upload share ONE scale — the peak
    /// of both 60-second windows plus headroom (the comp draws them as two stacked charts, but an honest
    /// comparison needs a common axis) — so a larger rate always draws taller regardless of direction.
    /// </summary>
    private void UpdateThroughput(NetworkSample sample) {
        // Each readout auto-scales to its OWN value so a small flow shows kbps even beside a large one
        // (e.g. "40 kbps" up next to "200 Mbps" down). Scaling from the actual value — never the
        // floored axis scale below — is what lets the unit drop to kbps or rise to Gbps.
        (DownText, DownUnit) = DataRateFormatter.Split(sample.DownMbps);
        (UpText, UpUnit) = DataRateFormatter.Split(sample.UpMbps);

        // The two sparklines still share ONE axis (the unfloored peak, floored only for rendering so an
        // idle blip isn't a full-height spike) so equal pixel height means equal throughput; the peak
        // caption reads in that shared unit.
        var peak = Math.Max(Peak(_downHistory), Peak(_upHistory));
        ThroughputYMax = ComputeScale(peak);
        ThroughputScaleText = $"peak {DataRateFormatter.Format(peak)}";

        DownPoints = BuildPoints(_downHistory, ThroughputYMax);
        UpPoints = BuildPoints(_upHistory, ThroughputYMax);
    }

    /// <summary>Largest sample in a rolling window.</summary>
    private static double Peak(double[] history) {
        var max = 0.0;
        for (var i = 0; i < history.Length; i++)
            if (history[i] > max) max = history[i];
        return max;
    }

    /// <summary>Upper bound for the shared axis: the peak plus ~15 % headroom so the top line doesn't
    /// touch the edge, clamped to <see cref="MinScaleMbps"/>.</summary>
    private static double ComputeScale(double peak) {
        var scaled = peak * 1.15;
        return scaled > MinScaleMbps ? scaled : MinScaleMbps;
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
    /// Reads the connections snapshot off the UI thread, stores the full sorted list, and rebuilds the
    /// current page. Only one page-sized slice is ever bound (via <see cref="RebuildPage"/>), so the UI
    /// stays light no matter how many sockets exist. Guarded against overlap (a slow enumeration must
    /// not pile up ticks) and never throws.
    /// </summary>
    private async Task LoadConnectionsAsync() {
        if (_connectionsInFlight)
            return;
        _connectionsInFlight = true;
        try {
            var snapshot = await ConnectionsProvider.GetAsync();
            // Awaited on the UI thread, so the continuation resumes there — safe to touch the collections.
            _allConnections.Clear();
            _allConnections.AddRange(snapshot.Rows);
            _connectionsTotal = snapshot.Total;
            RebuildPage();
        } catch {
            _allConnections.Clear();
            _connectionsTotal = 0;
            Connections.Clear();
            PageLinks.Clear();
            PagerVisible = false;
            ConnectionsSummary = "Connections unavailable";
        } finally {
            _connectionsInFlight = false;
        }
    }

    /// <summary>Raised when the user navigates to a different connections page (not on the periodic
    /// refresh), so the view can reset the list back to the top rather than keeping the old offset.</summary>
    public event Action? ConnectionsPageChanged;

    /// <summary>Pager callback: navigates to a page and re-pages immediately (so it feels instant rather
    /// than waiting for the next poll), then signals the view to scroll the list to the top.</summary>
    private void GoToPage(int page) {
        _currentPage = page < 1 ? 1 : page;
        RebuildPage();
        ConnectionsPageChanged?.Invoke();
    }

    /// <summary>Slices the full list to the current page, reconciles that slice into <see cref="Connections"/>,
    /// and rebuilds the header caption + pager. Clamps the page if the list shrank underneath us.</summary>
    private void RebuildPage() {
        var available = _allConnections.Count;
        var totalPages = Math.Max(1, (available + PageSize - 1) / PageSize);
        _currentPage = Math.Clamp(_currentPage, 1, totalPages);

        var start = (_currentPage - 1) * PageSize;
        var count = Math.Clamp(available - start, 0, PageSize);
        var slice = count > 0 ? _allConnections.GetRange(start, count) : (IReadOnlyList<ConnectionInfo>)Array.Empty<ConnectionInfo>();
        ReconcileConnections(slice);

        ConnectionsSummary = BuildConnectionsSummary(_connectionsTotal, totalPages);
        RebuildPageLinks(totalPages);
    }

    /// <summary>Header caption: the true active count, plus the page position when there's more than one page.</summary>
    private string BuildConnectionsSummary(int total, int totalPages) {
        if (total == 0)
            return "No active connections";
        var count = total.ToString(CultureInfo.InvariantCulture);
        if (totalPages <= 1)
            return $"{count} active";
        return $"{count} active · page {_currentPage.ToString(CultureInfo.InvariantCulture)} " +
               $"of {totalPages.ToString(CultureInfo.InvariantCulture)}";
    }

    /// <summary>Rebuilds the numbered pager (1, 2, 3, …). Every page fits on one row (the list is
    /// capped at ten pages), so all numbers are shown with no ellipsis or arrows. Hidden entirely when
    /// there's only one page.</summary>
    private void RebuildPageLinks(int totalPages) {
        PageLinks.Clear();
        PagerVisible = totalPages > 1;
        if (!PagerVisible)
            return;

        for (var p = 1; p <= totalPages; p++)
            PageLinks.Add(new PageLink(p, isCurrent: p == _currentPage, GoToPage));
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

    private void OnPingTick(object? sender, EventArgs e) => _ = RunPingAsync();

    /// <summary>Sends one ping off the UI thread and publishes the console + summary text. Guarded so
    /// sends never overlap (a <see cref="PingMonitor"/> can't run two at once) and never throws.</summary>
    private async Task RunPingAsync() {
        if (_pingInFlight)
            return;
        _pingInFlight = true;
        try {
            await _pingMonitor.SendAsync();
            // SendAsync was awaited on the UI thread, so the continuation resumes there — safe to bind.
            PingConsole = _pingMonitor.ConsoleText;
            PingSummary = _pingMonitor.SummaryText;
        } catch {
            // SendAsync already soft-fails; nothing further to do.
        } finally {
            _pingInFlight = false;
        }
    }

    /// <summary>Reads the DNS lookup off the UI thread and publishes the console + footer text. The
    /// provider never throws, but the fire-and-forget is guarded like the Dashboard's info loads.</summary>
    private async Task LoadDnsAsync() {
        try {
            var result = await DnsLookupProvider.GetAsync(DnsHost);
            // Awaited on the UI thread, so the continuation resumes there — safe to bind.
            DnsConsole = result.Console;
            DnsFooter = result.Footer;
        } catch {
            DnsConsole = $"Name:    {DnsHost}";
            DnsFooter = $"Could not resolve {DnsHost}";
        }
    }

    /// <summary>Re-runs the DNS lookup for the host currently in the field (Enter / the Look up button).</summary>
    [RelayCommand]
    private void LookupDns() => _ = LoadDnsAsync();

    /// <summary>Applies the ping target in the field: switches the monitor (resetting its rolling
    /// window), clears the console readout, and sends one ping so the panel updates immediately.</summary>
    [RelayCommand]
    private void ApplyPingTarget() {
        _pingMonitor.SetTarget(PingTarget);
        // Reflect any normalisation (trim/blank-ignored) back into the field.
        PingTarget = _pingMonitor.Target;
        PingConsole = "";
        PingSummary = "";
        _ = RunPingAsync();
    }

    /// <summary>Toolbar Refresh: an immediate re-sample, adapter re-read, connections re-read, ping and
    /// DNS re-lookup. Runs even while paused (a manual refresh should still update once), like the Dashboard.</summary>
    public void Refresh() {
        OnNetworkTick(this, EventArgs.Empty);
        _ = LoadAdaptersAsync();
        _ = LoadConnectionsAsync();
        _ = RunPingAsync();
        _ = LoadDnsAsync();
    }

    /// <summary>Pauses/resumes all of the tab's live polling. Drives the shell's Live pill;
    /// <see cref="Refresh"/> still works while paused.</summary>
    public void SetLive(bool live) {
        if (live) {
            _networkTimer.Start();
            _adapterTimer.Start();
            _connectionsTimer.Start();
            _pingTimer.Start();
        } else {
            _networkTimer.Stop();
            _adapterTimer.Stop();
            _connectionsTimer.Stop();
            _pingTimer.Stop();
        }
    }

    /// <summary>Stops the timers and disposes the ping monitor. Safe to call more than once. The
    /// network sampler is fully managed, so it needs no disposal.</summary>
    public void Dispose() {
        _networkTimer.Stop();
        _networkTimer.Tick -= OnNetworkTick;
        _adapterTimer.Stop();
        _adapterTimer.Tick -= OnAdapterTick;
        _connectionsTimer.Stop();
        _connectionsTimer.Tick -= OnConnectionsTick;
        _pingTimer.Stop();
        _pingTimer.Tick -= OnPingTick;
        _pingMonitor.Dispose();
    }
}
