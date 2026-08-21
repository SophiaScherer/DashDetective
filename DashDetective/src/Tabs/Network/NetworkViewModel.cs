using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashDetective.Services.Network;
using DashDetective.Services.SystemMetrics;
using DashDetective.Shared;
using DashDetective.Shared.Charts;
using DashDetective.Shared.Shortcuts;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;

namespace DashDetective.Tabs.Network;

/// <summary>
/// The Network tab: adapters, throughput, connections and diagnostics. Constructed once by the shell,
/// but it polls only while it is the visible tab: it implements <see cref="IRefreshablePage"/> (toolbar
/// Refresh), <see cref="ILiveSamplingPage"/> (toolbar Live pill), <see cref="IActivatablePage"/> (on/off
/// screen) and <see cref="IDisposable"/>. The last two are composed by a <see cref="SamplingGate"/>.
///
/// Throughput mirrors the Dashboard's sampler + 1 Hz timer + 60-sample rolling-buffer pattern. The
/// design comp shows download and upload as TWO stacked charts, but they share ONE dynamic scale
/// (<see cref="ThroughputYMax"/>, the peak of both windows) so their heights are directly comparable
/// — a bigger rate always draws taller, whichever direction it's in. Other panels (adapters,
/// connections, ping, DNS) are wired in later phases.
/// </summary>
public partial class NetworkViewModel : ViewModelBase, IRefreshablePage, ILiveSamplingPage, IActivatablePage, IShortcutTarget, IDisposable {
    /// <summary>Width of the rolling throughput history, in seconds (one sample per second).</summary>
    private const int WindowSeconds = 60;

    /// <summary>Floor for a series' vertical scale so idle traffic isn't drawn as a huge spike.</summary>
    private const double MinScaleMbps = 1.0;

    /// <summary>Fixed rows per connections page; users move through pages with the numbered pager.</summary>
    private const int PageSize = 100;

    /// <summary>Shown in place of the ping stats while the monitor is off, so an empty panel reads as a
    /// choice rather than a failure.</summary>
    private const string PingIdleSummary = "Press Start to ping";

    /// <summary>The DNS panel's counterpart: the lookup is user-initiated, so nothing has resolved yet.</summary>
    private const string DnsIdleFooter = "Press Look up to resolve";

    /// <summary>Cadence for re-reading adapters + IP config. Adapters change rarely (plug/unplug,
    /// connect/disconnect), so a coarse tick is plenty — like the Dashboard's 30 s uptime timer.</summary>
    private static readonly TimeSpan AdapterInterval = TimeSpan.FromSeconds(5);

    /// <summary>Cadence for the connections table. Netstat-style enumeration is heavier than a byte
    /// counter, so it polls slower than the 1 Hz throughput sampler.</summary>
    private static readonly TimeSpan ConnectionsInterval = TimeSpan.FromSeconds(2.5);

    /// <summary>Cadence for the ping diagnostics (one ping every couple of seconds, like a console
    /// <c>ping -t</c>). Longer than the 1.5 s ping timeout so sends never overlap.</summary>
    private static readonly TimeSpan PingInterval = TimeSpan.FromSeconds(2);

    private readonly NetworkProviders _providers;
    private readonly NetworkUsageSampler _networkSampler = new();
    // The channel owns the download history; upload is a second rolling buffer pushed alongside it.
    private readonly MetricHistory _upHistory = new MetricHistory(WindowSeconds);
    private readonly MetricChannel<NetworkSample> _networkChannel;
    private readonly DispatcherTimer _adapterTimer;
    private readonly DispatcherTimer _connectionsTimer;
    private readonly DispatcherTimer _pingTimer;
    private readonly PingMonitor _pingMonitor = new();
    private readonly SamplingGate _gate;
    private readonly Task _pingSeed;
    private bool _connectionsInFlight;
    private bool _pingInFlight;

    /// <summary>The latest full (sorted) snapshot; the UI only ever binds one page-sized slice of it.</summary>
    private readonly List<ConnectionInfo> _allConnections = new();
    /// <summary>True active count from the last snapshot (may exceed <see cref="_allConnections"/> if capped).</summary>
    private int _connectionsTotal;
    /// <summary>Current 1-based page.</summary>
    private int _currentPage = 1;

    // How many pages the current connection list spans; kept in step by RebuildPage so the page-stepping
    // shortcuts know where the end is.
    private int _pageCount = 1;

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

    /// <summary>The ping target, editable in the Ping panel. Seeded with the machine's own gateway.</summary>
    [ObservableProperty] private string _pingTarget = "";

    /// <summary>Whether the user has switched the ping monitor on. Off on every launch — the app must not
    /// send ICMP nobody asked for — and it survives leaving the tab, so returning finds it as it was left.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PingButtonText))]
    private bool _pingEnabled;

    /// <summary>The Start/Stop button's label, which is also the only disclosure that the monitor sends
    /// traffic at all.</summary>
    public string PingButtonText => PingEnabled ? "Stop" : "Start";

    /// <summary>Console-style ping output (last few reply lines).</summary>
    [ObservableProperty] private string _pingConsole = "";

    /// <summary>Rolling average-RTT / packet-loss summary line, or the idle prompt while stopped.</summary>
    [ObservableProperty] private string _pingSummary = PingIdleSummary;

    /// <summary>The DNS lookup host, editable in the DNS panel. Applied via <see cref="LookupDnsCommand"/>.</summary>
    [ObservableProperty] private string _dnsHost = DnsLookupProvider.DefaultHost;

    /// <summary>Console-style DNS output (name + resolved addresses).</summary>
    [ObservableProperty] private string _dnsConsole = "";

    /// <summary>DNS footer line (timing + record type, or a failure note).</summary>
    [ObservableProperty] private string _dnsFooter = DnsIdleFooter;

    /// <summary>Whether a lookup has been run this session, so Refresh re-resolves what the user asked
    /// for rather than reaching out to a host they never requested.</summary>
    private bool _dnsResolved;

    public NetworkViewModel() : this(NetworkProviders.ForCurrentPlatform()) { }

    /// <summary>Test seam: the same page over an explicit provider set (see <see cref="NetworkProviders"/>).
    /// The public ctor resolves the real one, so the shell still builds this with <c>new()</c>.</summary>
    internal NetworkViewModel(NetworkProviders providers) {
        _providers = providers;

        // Zero-filled buffers mean both charts are full-width (flat at 0) from the first frame; real
        // samples then shift in from the right, one per second.
        _networkChannel = new MetricChannel<NetworkSample>(TimeSpan.FromSeconds(1), WindowSeconds,
            () => _networkSampler.Sample(), static s => s.DownMbps, OnNetworkSample, OnNetworkFailed);
        UpdateThroughput(new NetworkSample(0, 0));

        // Adapters + IP config load once off the UI thread, then refresh on a coarse timer. This load
        // stays in the constructor because the shell's exported report reads the IP configuration from
        // here whether or not the tab was ever opened.
        _ = LoadAdaptersAsync();

        _adapterTimer = new DispatcherTimer { Interval = AdapterInterval };
        _adapterTimer.Tick += OnAdapterTick;

        _connectionsTimer = new DispatcherTimer { Interval = ConnectionsInterval };
        _connectionsTimer.Tick += OnConnectionsTick;

        _pingTimer = new DispatcherTimer { Interval = PingInterval };
        _pingTimer.Tick += OnPingTick;

        // Nothing above is started here: the gate runs the timers only while the tab is on screen and
        // the Live pill is on, so a tab that is never opened costs nothing. The ping timer additionally
        // waits for the user to press Start, and the DNS panel for Look up — neither sends anything on
        // its own.
        _gate = new SamplingGate(ApplySampling);

        _pingSeed = SeedPingTargetAsync();
    }

    /// <summary>Test seam: completes once the gateway suggestion has been written (or declined). The seed
    /// is the only other writer of <see cref="PingTarget"/>, so a test that sets the field itself waits on
    /// this first rather than racing it.</summary>
    internal Task PingTargetSeeded => _pingSeed;

    /// <summary>Suggests the machine's own gateway as the ping target — the one host the app can offer
    /// without choosing somebody else's server on the user's behalf. Never written over a value already
    /// typed, and left empty when there is no gateway, so Start simply has nothing to send until a host
    /// is named. Adapter enumeration is slow enough to keep off the UI thread.</summary>
    private async Task SeedPingTargetAsync() {
        var gateway = await Task.Run(NetworkGateway.Primary).ConfigureAwait(true);
        if (gateway is not null && string.IsNullOrWhiteSpace(PingTarget))
            PingTarget = gateway;
    }

    /// <summary>Sampler-failure handler for the throughput channel: shows neutral placeholders.</summary>
    private void OnNetworkFailed() {
        DownText = "—";
        UpText = "—";
    }

    /// <summary>Throughput channel callback: the channel already pushed the download rate into its
    /// history, so append the upload rate to the second buffer, then refresh the readouts.</summary>
    private void OnNetworkSample(NetworkSample sample) {
        _upHistory.Push(sample.UpMbps);
        UpdateThroughput(sample);
    }

    /// <summary>Updates both readouts and sparkline series. Download and upload share one scale (the peak
    /// of both windows) so equal pixel height means equal throughput.</summary>
    private void UpdateThroughput(NetworkSample sample) {
        // One unit for both readouts, taken from the larger of the two: the charts already share a scale,
        // so scaling the numbers independently would let the smaller rate show the bigger figure.
        var (down, up, unit) = DataRateFormatter.SplitPair(sample.DownMbps, sample.UpMbps);
        DownText = down;
        UpText = up;
        DownUnit = unit;
        UpUnit = unit;

        var peak = ChartScale.Peak(_networkChannel.History.Values, _upHistory.Values);
        ThroughputYMax = ChartScale.FitPeak(peak, MinScaleMbps);
        ThroughputScaleText = $"peak {DataRateFormatter.Format(peak)}";

        DownPoints = _networkChannel.History.Points(ThroughputYMax);
        UpPoints = _upHistory.Points(ThroughputYMax);
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
            var snapshot = await _providers.Adapters.GetAsync();
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
            var snapshot = await _providers.Connections.GetAsync();
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
        var totalPages = PagerMath.PageCount(available, PageSize);
        _currentPage = PagerMath.ClampPage(_currentPage, totalPages);
        _pageCount = totalPages;

        var start = PagerMath.PageStart(_currentPage, PageSize);
        var count = PagerMath.SliceCount(available, start, PageSize);
        var slice = count > 0 ? _allConnections.GetRange(start, count) : (IReadOnlyList<ConnectionInfo>)Array.Empty<ConnectionInfo>();
        CollectionReconciler.Reconcile(Connections, slice,
            static row => row.Key, static info => info.Key,
            static (row, info) => row.Update(info), static info => new ConnectionRow(info));

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

    public ShortcutScope Scope => ShortcutScope.Network;

    /// <summary>The page-stepping shortcuts. Only the pager is driven from the keyboard here — scrolling
    /// the list itself stays with PageUp/PageDown, where it belongs.</summary>
    public bool HandleShortcut(ShortcutId id) => id switch {
        ShortcutId.PreviousPage => TryGoToPage(_currentPage - 1),
        ShortcutId.NextPage => TryGoToPage(_currentPage + 1),
        _ => false,
    };

    /// <summary>Steps the pager, reporting a step past either end as unhandled so the key falls through
    /// rather than being swallowed at the last page.</summary>
    private bool TryGoToPage(int page) {
        if (page < 1 || page > _pageCount || page == _currentPage)
            return false;

        GoToPage(page);
        return true;
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
        _dnsResolved = true;
        try {
            var result = await _providers.Dns.GetAsync(DnsHost);
            // Awaited on the UI thread, so the continuation resumes there — safe to bind.
            DnsConsole = result.Console;
            DnsFooter = result.Footer;
        } catch {
            DnsConsole = $"Name:    {DnsHost}";
            DnsFooter = $"Could not resolve {DnsHost}";
        }
    }

    /// <summary>Runs the DNS lookup for the host currently in the field (Enter / the Look up button). The
    /// only thing that ever resolves a name: the panel stays idle until the user asks.</summary>
    [RelayCommand]
    private void LookupDns() => _ = LoadDnsAsync();

    /// <summary>The Ping panel's Start/Stop button.</summary>
    [RelayCommand]
    private void TogglePing() {
        if (PingEnabled)
            StopPing();
        else
            StartPing();
    }

    /// <summary>Applies the target in the field (Enter / the field's key binding) and pings it. Starting
    /// is the only outcome — Enter in a text box stopping the monitor would be a surprise.</summary>
    [RelayCommand]
    private void ApplyPingTarget() => StartPing();

    /// <summary>Points the monitor at the field's target, resetting its rolling window, and sends one ping
    /// so the panel updates immediately. A blank field is left alone: there is nothing to send, and
    /// substituting a host of the app's own choosing is exactly what this panel no longer does.</summary>
    private void StartPing() {
        if (string.IsNullOrWhiteSpace(PingTarget))
            return;

        _pingMonitor.SetTarget(PingTarget);
        // Reflect the trim back into the field.
        PingTarget = _pingMonitor.Target;
        PingConsole = "";
        PingSummary = "";
        PingEnabled = true;

        // Off-screen or paused, the gate keeps the timer stopped; the flag survives either way, so the
        // monitor resumes on its own when the tab comes back.
        if (_gate.IsRunning)
            _pingTimer.Start();
        _ = RunPingAsync();
    }

    /// <summary>Stops the monitor, leaving the last replies on screen with an idle summary.</summary>
    private void StopPing() {
        PingEnabled = false;
        _pingTimer.Stop();
        PingSummary = PingIdleSummary;
    }

    /// <summary>Toolbar Refresh: an immediate re-sample, adapter re-read and connections re-read. The ping
    /// and DNS panels join in only once the user has started them — a refresh must not turn into the first
    /// packet either one ever sent. Runs even while paused, like the Dashboard.</summary>
    public void Refresh() {
        _networkChannel.SampleNow();
        _ = LoadAdaptersAsync();
        _ = LoadConnectionsAsync();
        if (PingEnabled)
            _ = RunPingAsync();
        if (_dnsResolved)
            _ = LoadDnsAsync();
    }

    /// <summary>
    /// The primary adapter's name and IPv4 configuration for the exported system report, from the
    /// values already loaded into the tab. Read-only; the shell's report builder owns formatting.
    /// </summary>
    public IReadOnlyList<(string Key, string Value)> GetPrimaryConfigRows() {
        var adapter = string.IsNullOrWhiteSpace(_networkSampler.AdapterName) ? "—" : _networkSampler.AdapterName;
        return new[] {
            ("Adapter", adapter),
            ("IPv4", IpConfig.Ipv4),
            ("Subnet mask", IpConfig.SubnetMask),
            ("Gateway", IpConfig.Gateway),
            ("DNS", IpConfig.Dns),
            ("MAC", IpConfig.Mac),
            ("DHCP", IpConfig.Dhcp),
        };
    }

    /// <summary>Pauses/resumes all of the tab's live polling. Drives the shell's Live pill;
    /// <see cref="Refresh"/> still works while paused.</summary>
    public void SetLive(bool live) => _gate.Live = live;

    /// <summary>Starts/stops the tab's polling as it comes on and off screen.</summary>
    public void SetActive(bool active) => _gate.Active = active;

    /// <summary>Runs or halts every live timer on the page — the gate's composed answer, so it reflects
    /// the Live pill and the tab's visibility at once.</summary>
    private void ApplySampling(bool running) {
        if (running) {
            _networkChannel.Start();
            _adapterTimer.Start();
            _connectionsTimer.Start();

            // The ping loop is the user's to start, so visibility alone never resumes one they never began.
            if (PingEnabled)
                _pingTimer.Start();

            // Any time away leaves the connections snapshot stale, so re-read it now rather than showing
            // the old page until the first tick.
            _ = LoadConnectionsAsync();
        } else {
            _networkChannel.Stop();
            _adapterTimer.Stop();
            _connectionsTimer.Stop();
            _pingTimer.Stop();
        }
    }

    /// <summary>Stops the timers and disposes the ping monitor. Safe to call more than once. The
    /// network sampler is fully managed, so it needs no disposal.</summary>
    public void Dispose() {
        _networkChannel.Dispose();
        _adapterTimer.Stop();
        _adapterTimer.Tick -= OnAdapterTick;
        _connectionsTimer.Stop();
        _connectionsTimer.Tick -= OnConnectionsTick;
        _pingTimer.Stop();
        _pingTimer.Tick -= OnPingTick;
        _pingMonitor.Dispose();
    }
}
