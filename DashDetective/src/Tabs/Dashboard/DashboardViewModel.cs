using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using DashDetective.Services.Diagnostics;
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
public partial class DashboardViewModel : ViewModelBase, IRefreshablePage, ILiveSamplingPage, IActivatablePage, IDisposable, IReorderablePage {
    /// <summary>Key this page's widget order is persisted under.</summary>
    public string PageKey => "dashboard";

    private IReadOnlyList<string> _widgetOrder = [];

    /// <summary>The widget ids in display order, bound two-way to the page's WidgetBoard.</summary>
    public IReadOnlyList<string> WidgetOrder {
        get => _widgetOrder;
        set {
            if (ReferenceEquals(_widgetOrder, value))
                return;
            _widgetOrder = value ?? [];
            OnPropertyChanged();
            WidgetOrderChanged?.Invoke();
        }
    }

    /// <summary>Raised when a drag changes the order, so the shell can persist it.</summary>
    public event Action? WidgetOrderChanged;

    /// <summary>Width of the rolling CPU history, in seconds (one sample per second).</summary>
    private const int WindowSeconds = 60;

    /// <summary>
    /// Floor for the network throughput chart's shared vertical scale, in Mbps. Keeps an idle graph
    /// pinned flat near the bottom (rather than amplifying counter noise) and avoids a zero span.
    /// </summary>
    private const double MinNetworkScaleMbps = 1.0;

    private readonly SystemMetricsService _service;
    private readonly HardwareProviders _providers;
    private readonly MetricSubscriptions _subscriptions;
    private readonly SamplingGate _gate;

    // Per-view rolling histories (the samplers are shared; the histories are not).
    private readonly MetricHistory _cpuHistory = new MetricHistory(WindowSeconds);
    private readonly MetricHistory _memoryHistory = new MetricHistory(WindowSeconds);
    private readonly MetricHistory _gpuHistory = new MetricHistory(WindowSeconds);
    private readonly MetricHistory _storageHistory = new MetricHistory(WindowSeconds);
    private readonly MetricHistory _downHistory = new MetricHistory(WindowSeconds);
    private readonly MetricHistory _upHistory = new MetricHistory(WindowSeconds);

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
    private readonly Dictionary<int, MetricHistory> _diskHistories = new();
    private readonly IPhysicalDiskThroughputSampler _throughputSampler =
        IPhysicalDiskThroughputSampler.ForCurrentPlatform();
    private readonly DispatcherTimer _throughputTimer;

    /// <summary>Physical disk hosting Windows, resolved with the drive cards; −1 until then. The report and
    /// CSV describe this one disk rather than the <c>_Total</c> instance, which averages idle time across
    /// every disk on the machine.</summary>
    private int _systemDiskNumber = -1;

    // Per-GPU cards + rolling utilisation histories keyed by adapter LUID, driven by a page-local per-adapter
    // sampler on the same throughput timer (the shared GPU feed reports only one combined figure). One card per
    // physical GPU, inserted after Memory; its value + chart show the adapter's busiest-engine utilisation.
    private readonly Dictionary<string, DashboardCard> _gpuCards = new(StringComparer.Ordinal);
    private readonly Dictionary<string, MetricHistory> _gpuHistories = new(StringComparer.Ordinal);

    // PCI vendor per adapter, kept only to word the note on a card that reports no utilisation.
    private readonly Dictionary<string, uint?> _gpuVendors = new(StringComparer.Ordinal);
    private readonly IGpuUsageSampler _gpuSampler;

    /// <summary>Mints a sampler; kept so the inventory load can build one of its own. It must never be
    /// handed <see cref="_gpuSampler"/>: that load disposes what it is given, which on Windows closes this
    /// page's PDH query and leaves every GPU readout dead for the rest of the session.</summary>
    private readonly Func<IGpuUsageSampler> _gpuSamplerFactory;

    /// <summary>Mirrors the "NVIDIA GPU utilization" setting onto this page's sampler. Only the Linux arm
    /// acts on it; everywhere else it is inert. Pushed by the shell on load and whenever it changes.</summary>
    public bool NvidiaGpuMetrics {
        get => _gpuSampler.NvidiaMetricsEnabled;
        set => _gpuSampler.NvidiaMetricsEnabled = value;
    }

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
    /// <summary>The busiest adapter's utilisation, for the text report and the CSV export. Starts at the
    /// neutral placeholder and stays there on a machine where no adapter can report one — a Linux box whose
    /// only GPU is an NVIDIA or Intel part, say — rather than reading a confident 0%.</summary>
    [ObservableProperty] private string _gpuValueText = Placeholders.NoReading;
    [ObservableProperty] private string _gpuModelText = "";

    // The system drive's activity + capacity. Not shown on a card of its own (the per-disk cards cover the
    // visible surface); these feed the text report and the CSV export's disk column.
    [ObservableProperty] private string _storageValueText = "0";
    [ObservableProperty] private string _storageSubText = "";

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

    // ---- Chart captions, axes and cold-start state ----

    /// <summary>What each chart plots and over how long, restated whenever the Settings refresh interval
    /// moves the window. The Dashboard's charts used to carry no caption at all, so the same graph was
    /// explained on the Performance tab and bare here.</summary>
    [ObservableProperty] private string _cpuChartCaption = "";
    [ObservableProperty] private string _memoryChartCaption = "";
    [ObservableProperty] private string _networkChartCaption = "";

    /// <summary>The oldest end of every chart's time axis, e.g. "−60s". Shared: all three cover the
    /// same span.</summary>
    [ObservableProperty] private string _chartRangeStart = "";

    /// <summary>Each chart's cold-start line, cleared as soon as it has a trace to show. Starts set,
    /// since no chart has a sample before the first tick.</summary>
    [ObservableProperty] private string _cpuChartStatus = ChartStatus.Collecting;
    [ObservableProperty] private string _memoryChartStatus = ChartStatus.Collecting;
    [ObservableProperty] private string _networkChartStatus = ChartStatus.Collecting;

    /// <summary>The throughput chart's value labels. Live, unlike the percentage charts' fixed 100/50/0,
    /// because its ceiling follows the traffic.</summary>
    [ObservableProperty] private string _networkAxisMax = "";
    [ObservableProperty] private string _networkAxisMid = "";

    [ObservableProperty] private string _osText = "";
    [ObservableProperty] private string _deviceText = "";
    [ObservableProperty] private string _biosText = "";
    [ObservableProperty] private string _buildText = "";
    [ObservableProperty] private string _motherboardText = "";
    [ObservableProperty] private string _uptimeText = "";

    public DashboardViewModel(SystemMetricsService service)
        : this(service, HardwareProviders.ForCurrentPlatform()) { }

    /// <summary>Test seam: the same page over an explicit provider set, and optionally an explicit GPU
    /// sampler source — the one dependency the page resolves for itself, so without this a test cannot reach
    /// the cards' no-reading path. The public ctor resolves both, so the shell builds this exactly as before.
    ///
    /// <paramref name="gpuSamplerFactory"/> must mint a fresh sampler per call: this page keeps the first
    /// and the inventory load disposes one of its own.</summary>
    internal DashboardViewModel(
        SystemMetricsService service, HardwareProviders providers,
        Func<IGpuUsageSampler>? gpuSamplerFactory = null) {
        _providers = providers;
        _gpuSamplerFactory = gpuSamplerFactory ?? IGpuUsageSampler.ForCurrentPlatform;
        _gpuSampler = _gpuSamplerFactory();

        _service = service;

        // The adapter label is chosen once from the busiest active adapter.
        if (!string.IsNullOrWhiteSpace(service.NetworkAdapterName))
            NetworkAdapterName = service.NetworkAdapterName;

        // The shared-metric subscriptions, established on activation rather than here: the feeds are
        // ref-counted, so a page that stays subscribed off screen keeps them sampling. Each subscription
        // replays the latest cached sample when it attaches, seeding the surfaces with real data.
        _subscriptions = new MetricSubscriptions(
            () => service.SubscribeCpu(OnCpu, OnCpuFailed),
            () => service.SubscribeMemory(OnMemory, OnMemoryFailed),
            () => service.SubscribeNetwork(OnNetwork, OnNetworkFailed));

        // Seed the top stat row: the singleton cards show live data immediately; disk cards insert once
        // enumerated (before the Network card, keeping the CPU→Memory→GPU→Disks→Network grouping).
        Cards.Add(_cpuCard);
        Cards.Add(_memoryCard);
        Cards.Add(_networkCard);

        // Uptime has no sampler/history, so it stays a plain 30 s timer. Seed once for the first frame.
        UpdateUptime();
        ApplyChartWindow();

        _uptimeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _uptimeTimer.Tick += OnUptimeTick;

        // Drive the per-disk and per-GPU card sparklines from the page-local samplers, at the same cadence as
        // the shared feeds so every sparkline on the page covers the same span of time.
        _throughputTimer = new DispatcherTimer { Interval = service.Interval };
        _throughputTimer.Tick += OnThroughputTick;
        service.IntervalChanged += OnIntervalChanged;

        // Neither timer is started here: the gate runs them only while the page is on screen and the Live
        // pill is on.
        _gate = new SamplingGate(ApplySampling);

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
        UpdateDiskThroughput();
    }

    /// <summary>Toolbar Refresh for the Dashboard: an immediate re-sample of every metric.</summary>
    public void Refresh() => RefreshNow();

    /// <summary>
    /// Pauses/resumes the Dashboard's sampling for the shell's Live toggle. The shared feeds are also paused
    /// service-wide by the shell via <see cref="SystemMetricsService.Pause"/>; this page still drops its own
    /// subscriptions so the feeds stop for good when nothing else wants them.
    /// </summary>
    public void SetLive(bool live) => _gate.Live = live;

    /// <summary>Starts/stops the page's sampling as it comes on and off screen.</summary>
    public void SetActive(bool active) => _gate.Active = active;

    /// <summary>Runs or halts everything the page samples — the shared subscriptions plus its own uptime and
    /// per-disk/GPU timers. The gate's composed answer, so it reflects the Live pill and visibility at once.</summary>
    private void ApplySampling(bool running) {
        if (running) {
            _subscriptions.Attach();
            _uptimeTimer.Start();
            _throughputTimer.Start();
        } else {
            _subscriptions.Detach();
            _uptimeTimer.Stop();
            _throughputTimer.Stop();
        }
    }

    /// <summary>
    /// The Dashboard's contribution to the system report, from the current on-screen values (no
    /// re-sampling). Returns sections rather than text so every export format renders the same content;
    /// the shell composes these with the other tabs' rows and hands the whole thing to a formatter.
    /// </summary>
    public IReadOnlyList<ReportSection> GetReportSections() => [
        new ReportSection("System", [
            new ReportRow("OS", OsText),
            new ReportRow("Device", DeviceText),
            new ReportRow("Motherboard", MotherboardText),
            new ReportRow("BIOS", BiosText),
            new ReportRow("Build", BuildText),
            new ReportRow("Uptime", UptimeText),
        ]),
        new ReportSection("Live metrics", [
            new ReportRow("CPU", $"{CpuValueText}%  ({CpuModelText})"),
            new ReportRow("Memory", $"{MemoryUtilizationText}  ({MemoryModelText})"),
            // The only live metric that can have no reading at all, so the "%" has to be conditional —
            // "—%" would read as a measured zero.
            new ReportRow("GPU", GpuValueText == Placeholders.NoReading
                ? $"{Placeholders.NoReading}  ({GpuModelText})"
                : $"{GpuValueText}%  ({GpuModelText})"),
            new ReportRow("Storage", $"{StorageValueText}% active  ({StorageSubText})"),
            new ReportRow("Network", $"↓ {NetworkDownText} {NetworkDownUnit} / ↑ {NetworkUpText} {NetworkUpUnit}  ({NetworkAdapterName})"),
        ]),
    ];

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
              .Append(Csv(_cpuHistory.Values[i])).Append(',')
              .Append(Csv(_memoryHistory.Values[i])).Append(',')
              .Append(Csv(_gpuHistory.Values[i])).Append(',')
              .Append(Csv(_storageHistory.Values[i])).Append(',')
              .Append(Csv(_downHistory.Values[i])).Append(',')
              .Append(Csv(_upHistory.Values[i]))
              .Append('\n');
        }
        return sb.ToString();
    }

    /// <summary>Formats a metric value for CSV with two decimals, invariant culture.</summary>
    private static string Csv(double value) => value.ToString("F2", CultureInfo.InvariantCulture);

    /// <summary>Internal rather than private so a test can await the read the ctor fires and forgets.</summary>
    internal async Task LoadCpuInfoAsync() {
        // GetAsync never throws (it falls back to CpuStaticInfo.Unknown), but guard the whole
        // path so a surprise can't take down the app via an unobserved task exception.
        // The token is read BEFORE the await, not after: once sampling stops the gate hands out
        // CancellationToken.None, so a lazy read would never observe the cancellation it is checking for.
        var token = _gate.Token;
        try {
            var info = await _providers.Cpu.GetAsync();
            token.ThrowIfCancellationRequested();
            CpuModelShort = HardwareNameFormatter.ShortenCpu(info.Name);
            CpuModelText = FormatCpuModel(info);
            CpuCoresText = FormatCpuCores(info);
            _cpuCard.Sub = CpuModelShort;
        } catch when (token.IsCancellationRequested) {
            // The tab was left mid-read (cancelled, or failed once the user had gone); leave the last good values for its return.
        } catch {
            CpuModelShort = Placeholders.UnknownCpu;
            CpuModelText = Placeholders.UnknownCpu;
            _cpuCard.Sub = CpuModelShort;
        }
    }

    /// <summary>Internal rather than private so a test can await the read the ctor fires and forgets.</summary>
    internal async Task LoadMemoryInfoAsync() {
        // GetAsync never throws (it falls back to MemoryStaticInfo.Unknown), but guard the whole
        // path so a surprise can't take down the app via an unobserved task exception.
        var token = _gate.Token;
        try {
            var info = await _providers.Memory.GetAsync();
            token.ThrowIfCancellationRequested();
            MemoryModelText = FormatMemoryModel(info);
        } catch when (token.IsCancellationRequested) {
            // The tab was left mid-read (cancelled, or failed once the user had gone); leave the last good value for its return.
        } catch {
            MemoryModelText = Placeholders.UnknownRam;
        }
    }

    /// <summary>Enumerates the physical GPUs (off the UI thread) via the shared <see cref="DeviceInventory"/>
    /// and rebuilds the per-GPU cards + the System-Information "GPU" row. Soft-fails to no GPU cards on any
    /// error. Internal rather than private so a test can await the read the ctor fires and forgets.</summary>
    internal async Task LoadGpusAsync() {
        var token = _gate.Token;
        try {
            var inventory = await DeviceInventory.LoadAsync(_providers, _gpuSamplerFactory);
            token.ThrowIfCancellationRequested();
            RebuildGpuCards(inventory.All(DeviceCategory.Gpu));
        } catch when (token.IsCancellationRequested) {
            // The tab was left mid-read (cancelled, or failed once the user had gone); the cards on screen are still the last good ones.
        } catch {
            // Leave the existing GPU cards in place on a transient failure.
        }
    }

    /// <summary>Reconciles the GPU cards to the current adapter set: drops the old GPU cards, then inserts one
    /// per real adapter just after the Memory card (keeping the CPU→Memory→GPU→Disks→Network grouping). Each
    /// card's caption is its short model; its value + sparkline (busiest-engine %) are seeded here and then
    /// driven by the throughput timer. The System-Information "GPU" row lists every adapter's full name.</summary>
    private void RebuildGpuCards(IReadOnlyList<DeviceInstance> gpus) {
        // Build the replacements BEFORE touching anything on screen. The caller's soft-fail promises to
        // "leave the existing GPU cards in place" on a failure, and it could not keep that promise while
        // the clear came first: a throw partway through the loop left the old cards gone, the new ones
        // half-inserted, and _gpuCards/_gpuHistories/_gpuVendors out of step with Cards.
        var rebuilt = new List<(string Key, DashboardCard Card, uint? Vendor)>(gpus.Count);
        foreach (var gpu in gpus)
            rebuilt.Add((
                gpu.GpuLuid ?? gpu.Id,
                new DashboardCard(DeviceCategory.Gpu, gpu.Name.ToUpperInvariant(), "%") { Sub = gpu.Sub },
                gpu.GpuPci?.VendorId));

        var modelText = gpus.Count > 0
            ? string.Join(" / ", gpus.Select(g => g.Spec))
            : Placeholders.UnknownGpu;

        // Everything below is collection writes over values already in hand, so the swap completes.
        foreach (var card in _gpuCards.Values)
            Cards.Remove(card);
        _gpuCards.Clear();
        _gpuHistories.Clear();
        _gpuVendors.Clear();

        var insertAt = Cards.IndexOf(_memoryCard) + 1;
        foreach (var (key, card, vendor) in rebuilt) {
            Cards.Insert(insertAt++, card);
            _gpuCards[key] = card;
            _gpuHistories[key] = new MetricHistory(WindowSeconds);
            _gpuVendors[key] = vendor;
        }

        GpuModelText = modelText;

        // Seed the new cards' value + charts once so they aren't blank until the next throughput tick.
        UpdateGpuAdapters();
    }

    /// <summary>Internal rather than private so a test can await the read the ctor fires and forgets.</summary>
    internal async Task LoadSystemInfoAsync() {
        // GetAsync never throws (it falls back to SystemStaticInfo.Unknown), but guard the whole
        // path so a surprise can't take down the app via an unobserved task exception.
        var token = _gate.Token;
        try {
            var info = await _providers.System.GetAsync();
            token.ThrowIfCancellationRequested();
            OsText = info.Os;
            DeviceText = info.Device;
            BiosText = info.Bios;
            BuildText = info.Build;
            MotherboardText = info.Motherboard;
        } catch when (token.IsCancellationRequested) {
            // The tab was left mid-read (cancelled, or failed once the user had gone); leave the last good values for its return.
        } catch {
            OsText = Placeholders.UnknownOs;
            DeviceText = Environment.MachineName;
            BiosText = Placeholders.UnknownBios;
            BuildText = Placeholders.Unknown;
            MotherboardText = Placeholders.UnknownMotherboard;
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
            return Placeholders.UnknownRam;

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
        _cpuHistory.Push(value);
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
        CpuPoints = _cpuHistory.Points(100);
        CpuChartStatus = ChartStatus.For(_cpuHistory);
        _cpuCard.Value = CpuValueText;
        _cpuCard.Points = CpuPoints;
    }

    /// <summary>Samples every physical GPU (busiest-engine %) and refreshes each card's headline value +
    /// sparkline in place, keyed by adapter LUID. Also feeds the single overall history (busiest adapter) that
    /// the CSV export + text report read. GPUs without a current reading are left unchanged. Internal rather
    /// than private so a test can drive one tick without the timer.</summary>
    internal void UpdateGpuAdapters() {
        var adapters = _gpuSampler.SampleAdapters();
        if (adapters.Count == 0)
            return;

        double? overall = null;
        foreach (var (luid, sample) in adapters) {
            if (!_gpuHistories.TryGetValue(luid, out var history) || !_gpuCards.TryGetValue(luid, out var card))
                continue;

            // An adapter with no readable utilisation still has a card — it shows "—" rather than a 0 that
            // would read as idle. The unit goes with it, so the card says "—" and not "— %".
            if (sample.Overall is not { } reading) {
                card.Value = Placeholders.NoReading;
                card.Unit = "";
                // Says why the dash is there, so a detected-but-silent adapter doesn't read as a broken card.
                card.Note = GpuNoReadingNote.For(
                    _gpuVendors.GetValueOrDefault(luid), _gpuSampler.NvidiaMetricsEnabled);
                continue;
            }

            card.Note = "";
            var value = Math.Clamp(reading, 0, 100);
            if (value > overall || overall is null)
                overall = value;
            history.Push(value);
            card.Value = Math.Round(value).ToString(CultureInfo.InvariantCulture);
            card.Unit = "%";
            card.Points = history.Points(100);
        }

        // The single overall figure the CSV export and text report read. Left untouched when no adapter
        // reported one, so it holds its last value rather than dropping to a false zero.
        if (overall is { } busiest) {
            _gpuHistory.Push(busiest);
            GpuValueText = Math.Round(busiest).ToString(CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// Updates the system drive's activity figures — the headline shows Task Manager's disk "Active time"
    /// (0–100 %) for that one disk and the caption its capacity. These reach the text report and the CSV
    /// export rather than a card of their own; the visible per-disk cards are driven separately above.
    /// </summary>
    private void UpdateSystemDiskActivity(double activePercent) {
        _storageHistory.Push(activePercent);
        StorageValueText = Math.Round(activePercent).ToString(CultureInfo.InvariantCulture);
        UpdateStorageCapacity();
    }

    /// <summary>
    /// Reads the system drive's capacity via <see cref="DriveInfo"/> and updates the "used / total"
    /// caption. DriveInfo is a cheap syscall, so this runs on every tick; any failure clears the
    /// caption. The root comes from <see cref="SystemDrive.Root"/> rather than
    /// <c>Environment.SystemDirectory</c>, which is empty off Windows and would blank this every tick.
    /// </summary>
    private void UpdateStorageCapacity() {
        try {
            var drive = new DriveInfo(SystemDrive.Root);
            if (!drive.IsReady || drive.TotalSize <= 0) {
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
        _memoryHistory.Push(sample.LoadPercent);
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
        MemoryPoints = _memoryHistory.Points(100);
        MemoryChartStatus = ChartStatus.For(_memoryHistory);
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
        _downHistory.Push(sample.DownMbps);
        _upHistory.Push(sample.UpMbps);
        UpdateNetwork(sample);
    }

    /// <summary>Updates the throughput readouts and both sparkline series, which share one auto-fitted
    /// vertical scale (<see cref="NetworkYMax"/>) so their heights are directly comparable.</summary>
    private void UpdateNetwork(NetworkSample sample) {
        // Both readouts share one unit, taken from the larger of the two, so they can be compared against
        // each other and against the shared chart axis below.
        var (down, up, unit) = DataRateFormatter.SplitPair(sample.DownMbps, sample.UpMbps);
        NetworkDownText = down;
        NetworkUpText = up;
        NetworkDownUnit = unit;
        NetworkUpUnit = unit;
        NetworkSubText = $"↑ {up} {unit}";

        NetworkYMax = ChartScale.FitAxis(_downHistory.Values, _upHistory.Values, MinNetworkScaleMbps);
        NetworkDownPoints = _downHistory.Points(NetworkYMax);
        NetworkUpPoints = _upHistory.Points(NetworkYMax);
        NetworkChartStatus = ChartStatus.For(_downHistory);

        // Both series share one ceiling, so one set of value labels describes the pair.
        (NetworkAxisMax, NetworkAxisMid, _) = ChartAxis.RateLabels(NetworkYMax);

        // The one card not drawn on a percentage axis: its chart fills to the live peak, so it takes the
        // same ceiling label as the panel chart rather than the default "100%".
        _networkCard.AxisMaxLabel = NetworkAxisMax;
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
        var token = _gate.Token;
        try {
            var disksTask = _providers.Disks.GetAsync();
            var volumesTask = _providers.Volumes.GetAsync();
            await Task.WhenAll(disksTask, volumesTask);
            token.ThrowIfCancellationRequested();
            _systemDiskNumber = SystemVolume.FindDiskNumber(volumesTask.Result) ?? -1;
            RebuildDiskCards(StorageComposer.Compose(disksTask.Result, volumesTask.Result));
        } catch when (token.IsCancellationRequested) {
            // The tab was left mid-read (cancelled, or failed once the user had gone); the cards on screen are still the last good ones.
        } catch {
            // Leave the existing disk cards in place on a transient failure.
        }
    }

    /// <summary>Reconciles the disk cards to the current drive set: drops the old disk cards, then inserts one
    /// per drive just before the Network card (keeping the CPU→Memory→GPU→Disks→Network grouping). A disk
    /// card's caption is its capacity used; its value + sparkline (Active time) are seeded here and then driven
    /// by the throughput timer.</summary>
    private void RebuildDiskCards(IReadOnlyList<DriveCardData> drives) {
        // Built before anything on screen is touched, for the same reason as RebuildGpuCards: the
        // caller's soft-fail promises to leave the existing cards in place, which it cannot do if the
        // clear has already run.
        var rebuilt = new List<(int DiskNumber, DashboardCard Card)>(drives.Count);
        foreach (var drive in drives)
            rebuilt.Add((
                drive.DiskNumber,
                new DashboardCard(DeviceCategory.Disk, drive.Name.ToUpperInvariant(), "%") {
                    Sub = FormatCapacity(drive.UsedBytes, drive.UsedBytes + drive.FreeBytes),
                }));

        foreach (var card in _diskCards.Values)
            Cards.Remove(card);
        _diskCards.Clear();
        _diskHistories.Clear();

        var insertAt = Cards.IndexOf(_networkCard);
        foreach (var (diskNumber, card) in rebuilt) {
            Cards.Insert(insertAt++, card);
            _diskCards[diskNumber] = card;
            _diskHistories[diskNumber] = new MetricHistory(WindowSeconds);
        }

        // Seed the new cards' value + charts once so they aren't blank until the next throughput tick.
        UpdateDiskThroughput();
    }

    /// <summary>Follows the Settings refresh interval so the page-local sparklines keep pace with the shared
    /// feeds' and cover the same span of time, and restates the window every caption claims.</summary>
    private void OnIntervalChanged(TimeSpan interval) {
        _throughputTimer.Interval = interval;
        ApplyChartWindow();
    }

    /// <summary>Rewrites the chart captions and the time axis for the current window. The buffers are a
    /// fixed slot count, so the span they cover is the refresh interval times that count — a caption that
    /// hardcoded "60 seconds" would be wrong at every cadence but the default.</summary>
    private void ApplyChartWindow() {
        var window = ChartWindow.Describe(WindowSeconds, _service.Interval);
        CpuChartCaption = $"% Utilization over {window}";
        MemoryChartCaption = $"% Utilization over {window}";
        NetworkChartCaption = $"Receive and send over {window}";
        ChartRangeStart = ChartWindow.StartLabel(WindowSeconds, _service.Interval);
    }

    private void OnThroughputTick(object? sender, EventArgs e) {
        UpdateDiskThroughput();
        UpdateGpuAdapters();
    }

    /// <summary>Samples each disk's active time and refreshes its card's headline value + sparkline (Task
    /// Manager's disk "Active time", 0–100 %), and feeds the system drive's own reading to the report/CSV
    /// surfaces. Disks without a current reading are left unchanged.</summary>
    private void UpdateDiskThroughput() {
        foreach (var sample in _throughputSampler.Sample()) {
            if (sample.DiskNumber == _systemDiskNumber)
                UpdateSystemDiskActivity(sample.ActivePercent);

            if (!_diskHistories.TryGetValue(sample.DiskNumber, out var history)
                || !_diskCards.TryGetValue(sample.DiskNumber, out var card))
                continue;
            history.Push(sample.ActivePercent);
            card.Value = Math.Round(sample.ActivePercent).ToString(CultureInfo.InvariantCulture);
            card.Points = history.Points(100);
        }
    }

    /// <summary>Unsubscribes from the shared metrics and tears down the uptime + throughput timers and the
    /// per-disk sampler. The shared feed's samplers are owned (and disposed) by the service. Safe to call more
    /// than once.</summary>
    public void Dispose() {
        _gate.Dispose();
        _subscriptions.Dispose();
        _service.IntervalChanged -= OnIntervalChanged;
        _uptimeTimer.Stop();
        _uptimeTimer.Tick -= OnUptimeTick;
        _throughputTimer.Stop();
        _throughputTimer.Tick -= OnThroughputTick;
        _throughputSampler.Dispose();
        _gpuSampler.Dispose();
    }
}
