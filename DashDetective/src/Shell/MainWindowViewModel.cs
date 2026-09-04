using Avalonia.Automation;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashDetective.Services.Accessibility;
using DashDetective.Services.Diagnostics;
using DashDetective.Services.Notifications;
using DashDetective.Services.Search;
using DashDetective.Services.Settings;
using DashDetective.Services.Startup;
using DashDetective.Services.SystemMetrics;
using DashDetective.Services.Theming;
using DashDetective.Shared;
using DashDetective.Shared.Layout;
using DashDetective.Shared.Shortcuts;
using DashDetective.Shell.Help;
using DashDetective.Shell.Navigation;
using DashDetective.Shell.Search;
using DashDetective.Shell.Search.Providers;
using DashDetective.Tabs.Dashboard;
using DashDetective.Tabs.FileExplorer;
using DashDetective.Tabs.Hardware;
using DashDetective.Tabs.Network;
using DashDetective.Tabs.Performance;
using DashDetective.Tabs.Processes;
using DashDetective.Tabs.Settings;
using DashDetective.Tabs.Storage;
using DashDetective.Tabs.Toolkit;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;

namespace DashDetective.Shell;

public partial class MainWindowViewModel : ViewModelBase, IDisposable {
    /// <summary>The window minimum at 100%: the width an expanded nav bar plus a usable page needs.</summary>
    private const double BaseMinWindowWidth = 640;
    private const double BaseMinWindowHeight = 480;

    private static readonly IBrush LiveDot = SemanticBrushes.StatusGood;
    private static readonly IBrush PausedDot = SemanticBrushes.StatusIdle;

    private readonly SystemMetricsService _metrics;
    private readonly ResourceAlertWatcher _alerts;
    private readonly SettingsStore _store;
    private readonly ThemeService _theme = new();
    private readonly AccessibilityService _accessibility;
    private readonly NoticeService _notices = new();
    private readonly DashboardViewModel _dashboard;
    private readonly FileExplorerViewModel _fileExplorer = new();
    private readonly ProcessesViewModel _processes;
    private readonly PerformanceViewModel _performance;
    private readonly NetworkViewModel _network = new();
    private readonly StorageViewModel _storage;
    private readonly HardwareViewModel _hardware = new();
    private readonly ToolkitViewModel _toolkit = new();
    private readonly SettingsViewModel _settings;
    private readonly DispatcherTimer _clockTimer;
    private ClockFormat _clockFormat = ClockFormat.TwentyFourHour;

    // Resource-alert banner state: the breach the watcher currently reports, and whether the user
    // dismissed it. The banner shows only while there is one, unignored, and alerts are on.
    private ResourceAlert? _alert;
    private bool _alertDismissed;

    // Whether the window is on screen. Hidden to the tray it is not, and no page should be sampling —
    // the process is meant to be idle there, not merely invisible.
    private bool _windowVisible = true;

    // Whether the user has been told that closing the window leaves the app running. Persisted, so the
    // notice appears exactly once per install rather than on every close.
    private bool _trayNoticeShown;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentPageSelfScrolls), nameof(ScrollingPage), nameof(SelfScrollingPage),
                              nameof(RefreshToolTip))]
    private ViewModelBase _currentPage;

    /// <summary>Live wall clock shown at the right of the toolbar, in the chosen clock format.</summary>
    [ObservableProperty] private string _clock = "";

    /// <summary>Whether the resource-alert banner is currently shown in the shell.</summary>
    [ObservableProperty] private bool _alertBannerVisible;

    /// <summary>The resource-alert banner message, naming the resource and device that breached.</summary>
    [ObservableProperty] private string _alertText = "";

    /// <summary>Whether the confirmation banner is currently shown in the shell.</summary>
    [ObservableProperty] private bool _noticeBannerVisible;

    /// <summary>The confirmation message: what the action that was just taken did.</summary>
    [ObservableProperty] private string _noticeText = "";

    /// <summary>Whether live sampling is running. Drives the toolbar's Live pill.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LiveLabel), nameof(LiveDotBrush), nameof(RefreshToolTip))]
    private bool _isLive = true;

    /// <summary>The navigation bar: owns the nav items and selection; the shell hosts the page it
    /// selects (see <see cref="OnNavSelected"/>) and the toolbar reads its title/subtitle.</summary>
    public NavigationViewModel Nav { get; } = new();

    /// <summary>The live keyboard bindings: the catalog's defaults with the user's rebinds applied. One
    /// instance, shared by the key handler, Help, universal search and the Settings page, so all four
    /// describe and act on the same thing.</summary>
    public ShortcutBindings Shortcuts { get; } = new();

    /// <summary>The Help modal. Owned here rather than by the nav bar because the overlay covers the
    /// whole window, navigation bar included.</summary>
    public HelpViewModel Help { get; }

    /// <summary>The toolbar's universal search. Built here because this is the one class that already
    /// holds every page instance, so a result's "go there and reveal it" callback is a closure over the
    /// page it targets — no routing layer, and no page needs to know about search.</summary>
    public UniversalSearchViewModel Search { get; }

    /// <summary>What an empty search box offers. Persisted alongside the other settings.</summary>
    private readonly RecentSearches _recents = new();

    /// <summary>Raised when the Export shortcut fires, so the window can run the save dialog — it owns
    /// that because the picker needs the window's <c>TopLevel</c>. UI-only; carries no state.</summary>
    public event Action? ExportRequested;

    public string LiveLabel => IsLive ? "Live" : "Paused";
    public IBrush LiveDotBrush => IsLive ? LiveDot : PausedDot;

    /// <summary>What the toolbar's Refresh button promises on the current page — see
    /// <see cref="RefreshHint"/> for why it is worded rather than disabled.</summary>
    public string RefreshToolTip => RefreshHint.For(CurrentPage, IsLive);

    /// <summary>Whether the app should hide to the tray (rather than exit) when the window is closed.
    /// Gated on <see cref="TrayIntegration.HidesOnClose"/>, so a desktop with no tray closes normally
    /// however the setting is left — hiding behind an icon that never appears would strand the app.</summary>
    public bool ShowInTray => _settings.ShowInTray && TrayIntegration.HidesOnClose;

    /// <summary>How a screen reader treats the alert banner. Assertive because it reports a condition
    /// the user has not asked about and may need to act on.</summary>
    public AutomationLiveSetting AlertLiveSetting =>
        _accessibility.AnnounceUpdates ? AutomationLiveSetting.Assertive : AutomationLiveSetting.Off;

    /// <summary>How a screen reader treats the confirmation banner. Polite: it reports something the user
    /// just did, so it can wait for a pause.</summary>
    public AutomationLiveSetting NoticeLiveSetting =>
        _accessibility.AnnounceUpdates ? AutomationLiveSetting.Polite : AutomationLiveSetting.Off;

    /// <summary>The window's smallest usable size, scaled with the interface. At 200% the same page
    /// needs twice the room, and a window draggable below that would clip its content rather than
    /// reflow it.</summary>
    public double MinWindowWidth => BaseMinWindowWidth * _accessibility.ScaleFactor;
    public double MinWindowHeight => BaseMinWindowHeight * _accessibility.ScaleFactor;

    /// <summary>Whether the current page manages its own scrolling (e.g. File Explorer): such pages
    /// fill the viewport and scroll their own panes, so the shell hosts them in a bounded,
    /// non-scrolling container instead of the page-scrolling <c>ScrollViewer</c>.</summary>
    public bool CurrentPageSelfScrolls => CurrentPage is ISelfScrollingPage;

    /// <summary>The current page routed to the scrolling host (null when it self-scrolls). Routing to
    /// null keeps the inactive host empty so the view is only ever instantiated once.</summary>
    public ViewModelBase? ScrollingPage => CurrentPage is ISelfScrollingPage ? null : CurrentPage;

    /// <summary>The current page routed to the bounded, self-scrolling host (null otherwise).</summary>
    public ViewModelBase? SelfScrollingPage => CurrentPage is ISelfScrollingPage ? CurrentPage : null;

    public MainWindowViewModel(SystemMetricsService metrics, SettingsStore store, AppSettings settings) {
        // The shared metrics service is injected by the composition root and passed to the pages that
        // sample (Dashboard, Performance, Processes); the rest are self-contained.
        _metrics = metrics;
        _store = store;
        _accessibility = new AccessibilityService(_theme);
        _alerts = new ResourceAlertWatcher(metrics);
        Help = new HelpViewModel(Shortcuts);
        _dashboard = new DashboardViewModel(metrics);
        _processes = new ProcessesViewModel(metrics);
        _performance = new PerformanceViewModel(metrics, _theme);
        _storage = new StorageViewModel(metrics);

        // Apply the persisted appearance + layout through the seams that own them, before wiring the
        // controls that observe them. ThemeService stays the only code that writes to the application.
        ApplySettings(settings);

        // Build the Settings page with the shared theming seam + nav, the metrics service (refresh
        // interval), the loaded settings (toggle/interval seed) and the report/CSV builders.
        _settings = new SettingsViewModel(_theme, _accessibility, Nav, metrics, settings,
                                          IStartupRegistration.ForCurrentPlatform(), Shortcuts,
                                          BuildReport, BuildMetricsCsv, ResetWidgetOrders);

        // The two pages that confirm something report to the banner this owns. Set here rather than
        // taken by ctor: Toolkit's ctor is a test seam a dozen tests build directly, and a page's
        // confirmations are not something it needs to be constructed with.
        _settings.Notify = Notify;
        _toolkit.Notify = Notify;
        _network.Notify = Notify;

        // Persist whenever a control changes. The store debounces, so calling Persist freely is fine.
        _settings.Changed += OnSettingChanged;
        Nav.HelpRequested += Help.Open;
        Nav.PropertyChanged += OnNavPropertyChanged;
        _fileExplorer.PropertyChanged += OnFileExplorerPropertyChanged;
        _performance.ScopeChanged += Persist;
        _performance.DetailChanged += Persist;
        _processes.PreferencesChanged += Persist;
        foreach (var order in SavedOrders)
            order.Changed += Persist;
        _alerts.AlertChanged += OnAlertChanged;
        _notices.Changed += OnNoticeChanged;
        _accessibility.Changed += OnAccessibilityChanged;

        // Build the nav items pointing their select callback at the nav VM, then let it own selection.
        Nav.Initialize(new[] {
            new NavItem("Dashboard", "Dashboard", "Real-time system overview",
                        Icons.Dashboard, _dashboard, Nav.Navigate),
            new NavItem("File Explorer", "File Explorer", "Browse files and folders",
                        Icons.FileExplorer, _fileExplorer, Nav.Navigate),
            new NavItem("Processes", "Processes", "Live processes & resource usage",
                        Icons.Processes, _processes, Nav.Navigate),
            new NavItem("Performance", "Performance", "Live resource utilization",
                        Icons.Performance, _performance, Nav.Navigate),
            new NavItem("Network", "Network", "Adapters, connections & diagnostics",
                        Icons.Network, _network, Nav.Navigate),
            new NavItem("Storage", "Storage", "Drives, partitions & health",
                        Icons.Storage, _storage, Nav.Navigate),
            new NavItem("Hardware", "Hardware", "Installed components & specs",
                        Icons.Hardware, _hardware, Nav.Navigate),
            new NavItem("Toolkit", "Toolkit", "Common commands & diagnostics",
                        Icons.Toolkit, _toolkit, Nav.Navigate),
            new NavItem("Settings", "Settings", "Application preferences",
                        Icons.Settings, _settings, Nav.Navigate),
        });
        _currentPage = Nav.SelectedNav.Page;
        Nav.SelectionChanged += OnNavSelected;

        // Start the page the bar selected; every other page stays idle until it is navigated to.
        UpdatePageActivity();

        // Built after the bar so the page provider can read the live nav items rather than a copy. Each
        // provider's "go there" callback is a closure over the page it targets, which is why search is
        // assembled here: this is the one class already holding every page instance.
        Search = new UniversalSearchViewModel([
            new PageSearchProvider(Nav.NavItems, Nav.Navigate),
            new SettingSearchProvider(RevealSetting, Icons.Settings),
            new ShortcutSearchProvider(Shortcuts, Help.Open, Icons.Help),
            new HelpSearchProvider(RevealHelp, Icons.Help),
            new ToolkitSearchProvider(() => _toolkit.AllEntries, RevealToolkit, Icons.Toolkit),
            new ProcessSearchProvider(() => _processes.Snapshot, RevealProcess, Icons.Processes),
            new FileSearchProvider(
                new WindowsSearchIndex(), new FileSystemFallbackSearch(),
                () => _fileExplorer.CurrentPath, RevealFile,
                Icons.Document, Icons.FileExplorer),
        ], _recents);
        _recents.Changed += Persist;
        _toolkit.PinsChanged += Persist;
        _toolkit.CommandsChanged += Persist;

        // A Toolkit folder row opening in the app's own File Explorer is the same jump universal search
        // makes, so it reuses it rather than teaching the page about another tab.
        _toolkit.FileExplorerRevealRequested += RevealFile;

        // The Performance rail names real devices, so its detail header offers a jump to the tab that owns
        // the selected one. Same arrangement as the Toolkit row above: the page raises what it is looking
        // at, and only the shell knows which tab that means.
        // And the same arrangement inbound: a Dashboard card or chart names its device, and this is what
        // knows that Performance is the tab showing it.
        _dashboard.PerformanceRevealRequested += RevealResource;

        _performance.StorageRevealRequested += RevealDrive;
        _performance.NetworkRevealRequested += RevealAdapter;
        _performance.HardwareRevealRequested += ShowHardware;

        // Seed once so the clock is correct on the first frame, then tick every second.
        UpdateClock();
        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick += (_, _) => UpdateClock();
        _clockTimer.Start();
    }

    /// <summary>Applies persisted appearance + layout through the owning seams: theme/accent via
    /// <see cref="ThemeService"/>, dock/collapse via <see cref="Nav"/>, and show-hidden via the File
    /// Explorer. The refresh interval and toggles are applied by <see cref="SettingsViewModel"/>.</summary>
    /// <summary>The interface size moved, so the window's floor moves with it.</summary>
    private void OnAccessibilityChanged() {
        OnPropertyChanged(nameof(MinWindowWidth));
        OnPropertyChanged(nameof(MinWindowHeight));
        OnPropertyChanged(nameof(AlertLiveSetting));
        OnPropertyChanged(nameof(NoticeLiveSetting));
    }

    private void ApplySettings(AppSettings settings) {
        Shortcuts.Load(ShortcutOverrideCodec.Decode(settings.ShortcutOverrides));

        _theme.ApplyTheme(settings.Theme);
        _accessibility.Apply(settings);
        var accent = FindAccent(settings.AccentName);
        if (accent is { } preset)
            _theme.ApplyAccent(preset);
        else
            _theme.ApplyDefaultAppearance();

        Nav.Orientation = settings.NavOrientation;
        Nav.IsCollapsed = settings.NavCollapsed;
        _fileExplorer.ShowHidden = settings.ShowHiddenFiles;
        _trayNoticeShown = settings.TrayNoticeShown;

        // Commands before pins: a pin naming one of the user's own commands has nothing to find until
        // that command is on the page.
        _toolkit.LoadCommands(settings.CustomCommands);
        _toolkit.LoadPins(settings.PinnedCommands);
        _performance.ShowAllDevices = settings.PerformanceShowAllDevices;
        _performance.GpuDetailedView = settings.GpuDetailedView;
        _performance.CpuDetailedView = settings.CpuDetailedView;
        ApplyNvidiaGpuMetrics(settings.NvidiaGpuMetrics);
        ApplyClockFormat(settings.ClockFormat);
        ApplyAlertSettings(settings);
        _recents.Load(settings.RecentSearches);

        _processes.ColumnOrder = ProcessColumnOrder.Decode(settings.ProcessColumns);

        // The remember toggles are applied before what they gate, so a saved fold or sort only lands
        // when the user actually asked for it to be kept.
        _processes.RememberCollapsedGroups = settings.ProcessesRememberCollapsed;
        _processes.RememberSort = settings.ProcessesRememberSort;
        if (settings.ProcessesRememberCollapsed)
            _processes.CollapsedGroups = EnumListCodec.Decode<ProcessCategory>(settings.ProcessesCollapsedSections);
        if (settings.ProcessesRememberSort &&
            ProcessSortState.TryDecode(settings.ProcessesSort, out var sortKey, out var sortAscending)) {
            _processes.SortKey = sortKey;
            _processes.SortAscending = sortAscending;
        }

        var orders = WidgetOrders.Decode(settings.WidgetOrders);
        foreach (var saved in SavedOrders)
            if (orders.TryGetValue(saved.Key, out var order))
                saved.Order = order;
    }

    /// <summary>The pages whose widget order is persisted.</summary>
    private IEnumerable<IReorderablePage> ReorderablePages {
        get {
            yield return _dashboard;
            yield return _network;
            yield return _storage;
            yield return _hardware;
            yield return _processes;
            yield return _performance;
        }
    }

    /// <summary>Every saved order in the app. A page can hold more than one, so this is what the
    /// shell subscribes to, restores and encodes — never the page itself.</summary>
    private IEnumerable<SavedOrder> SavedOrders => ReorderablePages.SelectMany(page => page.SavedOrders);

    /// <summary>Every widget order, keyed by what it saves under, for the settings snapshot.</summary>
    private string EncodeWidgetOrders() {
        var orders = new Dictionary<string, IReadOnlyList<string>>();
        foreach (var saved in SavedOrders)
            if (saved.Order.Count > 0)
                orders[saved.Key] = saved.Order;

        return WidgetOrders.Encode(orders);
    }

    /// <summary>Puts every page's widgets and cards back in the order its markup declares. An empty
    /// SavedOrder is what a panel reads as a reset; the encoded string collapses to "" with it, and the
    /// ctor's own Changed subscription persists it exactly as a drag would.</summary>
    private void ResetWidgetOrders() {
        foreach (var saved in SavedOrders)
            saved.Order = [];
    }

    /// <summary>Resolves a persisted accent name to its preset, or <c>null</c> for the default look
    /// (an unknown name also falls back to default).</summary>
    private static AccentPreset? FindAccent(string? name) {
        if (string.IsNullOrEmpty(name))
            return null;
        foreach (var preset in AccentPreset.All)
            if (preset.Name == name)
                return preset;
        return null;
    }

    /// <summary>Captures the live state of every persisted seam into an immutable snapshot.</summary>
    private AppSettings CaptureCurrent() => new() {
        Theme = _theme.CurrentTheme,
        AccentName = _theme.CurrentAccent?.Name,
        NavOrientation = Nav.Orientation,
        NavCollapsed = Nav.IsCollapsed,
        ClockFormat = _settings.ClockFormat,
        UiScalePercent = _accessibility.ScalePercent,
        HighContrast = _accessibility.HighContrast,
        DistinguishWithoutColor = _accessibility.DistinguishWithoutColor,
        ColorVision = _accessibility.ColorVision,
        AnnounceUpdates = _accessibility.AnnounceUpdates,
        RefreshIntervalSeconds = _settings.SelectedIntervalSeconds,
        ShowHiddenFiles = _fileExplorer.ShowHidden,
        PinnedCommands = _toolkit.EncodePins(),
        CustomCommands = _toolkit.EncodeCommands(),
        LaunchAtStartup = _settings.LaunchAtStartup,
        ShowInTray = _settings.ShowInTray,
        TrayNoticeShown = _trayNoticeShown,
        ResourceAlerts = _settings.ResourceAlerts,
        AlertCpuEnabled = _settings.CpuAlert.IsEnabled,
        AlertMemoryEnabled = _settings.MemoryAlert.IsEnabled,
        AlertGpuEnabled = _settings.GpuAlert.IsEnabled,
        AlertDiskActiveEnabled = _settings.DiskActiveAlert.IsEnabled,
        AlertLowDiskFreeEnabled = _settings.LowDiskFreeAlert.IsEnabled,
        AlertCpuPercent = _settings.CpuAlert.Value,
        AlertMemoryPercent = _settings.MemoryAlert.Value,
        AlertGpuPercent = _settings.GpuAlert.Value,
        AlertDiskActivePercent = _settings.DiskActiveAlert.Value,
        AlertLowDiskFreePercent = _settings.LowDiskFreeAlert.Value,
        AlertSustainSeconds = _settings.AlertSustain.Value,
        NvidiaGpuMetrics = _settings.NvidiaGpuMetrics,
        PerformanceShowAllDevices = _performance.ShowAllDevices,
        GpuDetailedView = _performance.GpuDetailedView,
        CpuDetailedView = _performance.CpuDetailedView,
        RecentSearches = _recents.Encode(),
        WidgetOrders = EncodeWidgetOrders(),
        CollapsedWidgets = _settings.Collapse.Encode(),
        ProcessColumns = ProcessColumnOrder.Encode(_processes.ColumnOrder),
        ProcessesRememberCollapsed = _processes.RememberCollapsedGroups,
        ProcessesRememberSort = _processes.RememberSort,
        // Nothing is written for a toggle that is off, so switching one on later starts from the
        // page's own default rather than from whatever it happened to be showing months ago.
        ProcessesCollapsedSections = _processes.RememberCollapsedGroups
            ? EnumListCodec.Encode(_processes.CollapsedGroups)
            : "",
        ProcessesSort = _processes.RememberSort
            ? ProcessSortState.Encode(_processes.SortKey, _processes.SortAscending)
            : "",
        ShortcutOverrides = ShortcutOverrideCodec.Encode(Shortcuts.Overrides),
    };

    /// <summary>Debounced save of the current settings snapshot.</summary>
    private void Persist() => _store.Save(CaptureCurrent());

    /// <summary>A Settings control changed: persist, refresh the tray-close flag, and re-evaluate the
    /// alert banner (the "Resource alerts" toggle gates it).</summary>
    private void OnSettingChanged() {
        Persist();
        OnPropertyChanged(nameof(ShowInTray));
        UpdateAlertBanner();
        ApplyNvidiaGpuMetrics(_settings.NvidiaGpuMetrics);
        ApplyClockFormat(_settings.ClockFormat);
        ApplyAlertSettings(CaptureCurrent());
    }

    /// <summary>Pushes the alert thresholds and the master switch onto the watcher. Thresholds first: the
    /// setter clears every streak, so applying them after enabling would discard the first samples.
    /// The watcher holds the CPU and memory feeds open on its own, which is why it follows the setting
    /// rather than running unconditionally.</summary>
    private void ApplyAlertSettings(AppSettings settings) {
        // Folded into the watcher's zero-means-off contract here: the service has no reason to know that a
        // threshold and a switch are two controls on a page, and this keeps a disabled row's number.
        _alerts.Options = new ResourceAlertOptions {
            CpuPercent = Watched(settings.AlertCpuEnabled, settings.AlertCpuPercent),
            MemoryPercent = Watched(settings.AlertMemoryEnabled, settings.AlertMemoryPercent),
            GpuPercent = Watched(settings.AlertGpuEnabled, settings.AlertGpuPercent),
            DiskActivePercent = Watched(settings.AlertDiskActiveEnabled, settings.AlertDiskActivePercent),
            LowDiskFreePercent = Watched(settings.AlertLowDiskFreeEnabled, settings.AlertLowDiskFreePercent),
            SustainSeconds = settings.AlertSustainSeconds,
        };
        _alerts.Enabled = settings.ResourceAlerts;
    }

    /// <summary>A threshold as the watcher wants it: the number when the resource is watched, else zero.</summary>
    private static int Watched(bool enabled, int percent) => enabled ? percent : 0;

    /// <summary>Mirrors the clock-format preference onto the toolbar clock and the Toolkit log — the two
    /// places that show a wall-clock time. Pushed rather than read, matching the NVIDIA opt-in above, and
    /// the clock is re-stamped at once so the change is visible without waiting for the next tick.</summary>
    private void ApplyClockFormat(ClockFormat format) {
        _clockFormat = format;
        _toolkit.ClockFormat = format;
        UpdateClock();
    }

    /// <summary>Mirrors the NVIDIA opt-in onto both pages that own a GPU sampler. Pushed rather than read,
    /// matching the other per-page toggles; the cards pick it up on their next tick, and the adapter list
    /// itself does not depend on it.</summary>
    private void ApplyNvidiaGpuMetrics(bool enabled) {
        _dashboard.NvidiaGpuMetrics = enabled;
        _performance.NvidiaGpuMetrics = enabled;
    }

    private void OnNavPropertyChanged(object? sender, PropertyChangedEventArgs e) {
        if (e.PropertyName is nameof(NavigationViewModel.Orientation) or nameof(NavigationViewModel.IsCollapsed))
            Persist();
    }

    private void OnFileExplorerPropertyChanged(object? sender, PropertyChangedEventArgs e) {
        if (e.PropertyName == nameof(FileExplorerViewModel.ShowHidden))
            Persist();
    }

    /// <summary>The watcher changed what it reports. A different resource clears any dismissal too, not
    /// just recovery — the user dismissed the message they were shown, not every message to come.</summary>
    private void OnAlertChanged(ResourceAlert? alert) {
        _alert = alert;
        _alertDismissed = false;
        if (alert is { } breach)
            AlertText = DescribeAlert(breach);
        UpdateAlertBanner();
    }

    /// <summary>The banner copy. Names the device, because "a GPU is busy" on a two-GPU machine is not
    /// something anyone can act on, and reports the threshold that was actually crossed rather than a
    /// literal, so the text cannot drift from the setting.</summary>
    private string DescribeAlert(ResourceAlert alert) => alert.Metric switch {
        AlertMetric.DiskSpace =>
            $"Low disk space — {alert.DeviceName} is {alert.Value:F0}% free, at or below the {alert.Threshold}% warning level.",
        _ =>
            $"High resource usage — {alert.DeviceName} has stayed at or above {alert.Threshold}% for {_settings.AlertSustain.Value} seconds.",
    };

    /// <summary>The banner shows only while a breach is active, the "Resource alerts" setting is on, and
    /// the user hasn't dismissed the current breach.</summary>
    private void UpdateAlertBanner() =>
        AlertBannerVisible = _alert is not null && _settings.ResourceAlerts && !_alertDismissed;

    /// <summary>Dismisses the current alert banner (until usage recovers and breaches again).</summary>
    [RelayCommand]
    private void DismissAlert() {
        _alertDismissed = true;
        UpdateAlertBanner();
    }

    /// <summary>The notice service raised or expired a confirmation. The text is kept while it is
    /// showing only — clearing it on the way out would blank the banner mid-fade.</summary>
    private void OnNoticeChanged(string? message) {
        if (message is not null)
            NoticeText = message;
        NoticeBannerVisible = message is not null;
    }

    /// <summary>Takes the confirmation down early — the banner's × or Esc.</summary>
    [RelayCommand]
    private void DismissNotice() => _notices.Dismiss();

    /// <summary>Raises a confirmation from view code-behind, which has no injection point of its own and
    /// calls the view model it already resolved from its DataContext.</summary>
    public void Notify(string message) => _notices.Show(message);

    private void UpdateClock() => Clock = TimeOfDayFormatter.Format(DateTime.Now, _clockFormat);

    /// <summary>Pauses/resumes all live metric sampling on every page that samples (Dashboard,
    /// Network, …), routed through the <see cref="ILiveSamplingPage"/> marker so no per-page wiring
    /// is needed here.</summary>
    [RelayCommand]
    private void ToggleLive() {
        IsLive = !IsLive;
        if (IsLive)
            _metrics.Resume();
        else
            _metrics.Pause();
        _alerts.SetLive(IsLive);
        foreach (var item in Nav.NavItems)
            (item.Page as ILiveSamplingPage)?.SetLive(IsLive);
    }

    /// <summary>Refreshes whichever page is current: the Dashboard re-samples its metrics, the File
    /// Explorer reloads its current folder. Pages that don't implement <see cref="IRefreshablePage"/>
    /// (e.g. Settings) simply ignore it.</summary>
    [RelayCommand]
    private void Refresh() => (CurrentPage as IRefreshablePage)?.Refresh();

    // ----- Keyboard shortcuts -----

    /// <summary>
    /// Runs a keyboard shortcut, returning whether it was consumed (an unconsumed key falls through to
    /// the rest of the app). The priority chain lives here rather than in the window so it is testable
    /// without a UI: an open modal owns the keyboard first, then the current page gets a chance at its
    /// own shortcuts, and anything left over is handled globally.
    /// </summary>
    /// <summary>Which set of bindings is live right now — the current page's, or Global for a page with
    /// no shortcuts of its own. Read by the window before resolving a key.</summary>
    public ShortcutScope ActiveScope =>
        Help.IsOpen ? ShortcutScope.Global
        : Search.IsOpen ? Search.Scope
        : (CurrentPage as IShortcutTarget)?.Scope ?? ShortcutScope.Global;

    public bool HandleShortcut(ShortcutId id) {
        // A capture box on the Settings page is waiting for a key press. This listener tunnels from the
        // window, so it sees the press first; claiming it here would run the shortcut being rebound
        // instead of letting it be captured. Returning false leaves the key to continue down to the box.
        if (_settings.IsCapturingShortcut)
            return false;

        // While the Help modal is up it swallows every shortcut — Esc closes it, and nothing else is
        // allowed to act on the page hidden behind the scrim.
        if (Help.IsOpen) {
            if (id == ShortcutId.Escape)
                Help.Close();
            return true;
        }

        // The search dropdown sits between the modal and the page: while it is open the arrows walk the
        // results and Esc puts it away, but unlike Help it doesn't swallow the rest — Ctrl+1 still
        // switches tabs from a half-typed search.
        if (Search.IsOpen && Search.HandleShortcut(id))
            return true;

        if (CurrentPage is IShortcutTarget target && target.HandleShortcut(id))
            return true;

        return HandleGlobal(id);
    }

    /// <summary>Handles the shortcuts that work anywhere, whatever page is showing.</summary>
    private bool HandleGlobal(ShortcutId id) => id switch {
        // The nine tab jumps are contiguous in the enum, so the offset from the first is the nav
        // position they select.
        >= ShortcutId.NavigateTab1 and <= ShortcutId.NavigateTab9 =>
            NavigateToIndex(id - ShortcutId.NavigateTab1),
        ShortcutId.NextTab => CycleTab(1),
        ShortcutId.PreviousTab => CycleTab(-1),
        ShortcutId.ToggleNavCollapse => Run(Nav.ToggleCollapseCommand),
        ShortcutId.OpenSettings => NavigateToPage(_settings),
        ShortcutId.ToggleLive => Run(ToggleLiveCommand),
        ShortcutId.Refresh => Run(RefreshCommand),
        ShortcutId.Export => Raise(ExportRequested),
        ShortcutId.ShowHelp => Open(Help),
        ShortcutId.FocusSearch => FocusSearch(),
        ShortcutId.ToggleTheme => Run(_settings.ToggleThemeCommand),
        // Nothing higher up the chain claimed Esc, so the only thing left to dismiss is a banner.
        ShortcutId.Escape => DismissBannerIfShowing(),
        _ => false,
    };

    /// <summary>Runs a command and reports it as consumed, so the switch above stays an
    /// expression.</summary>
    private static bool Run(IRelayCommand command) {
        command.Execute(null);
        return true;
    }

    /// <summary>Opens the Help modal and reports the key as consumed.</summary>
    private static bool Open(HelpViewModel help) {
        help.Open();
        return true;
    }

    /// <summary>Puts the caret in the toolbar search box. Always consumed: Ctrl+F is search's alone, so
    /// leaving it unhandled would let it fall through to whatever else was listening.</summary>
    private bool FocusSearch() {
        Search.Focus();
        return true;
    }

    /// <summary>Raises a request event, reporting the key as consumed only if something is listening
    /// (Export does nothing without the window's file picker attached).</summary>
    private static bool Raise(Action? request) {
        if (request is null)
            return false;

        request();
        return true;
    }

    /// <summary>Dismisses a banner, or leaves Esc unconsumed when neither is showing. The confirmation
    /// goes first: it is the newer of the two, and the warning under it is the one worth keeping.</summary>
    private bool DismissBannerIfShowing() {
        if (NoticeBannerVisible) {
            DismissNotice();
            return true;
        }

        if (!AlertBannerVisible)
            return false;

        DismissAlert();
        return true;
    }

    /// <summary>Selects the nav item at the given position. An index past the end of the bar (Ctrl+8
    /// on a shorter bar) is simply ignored.</summary>
    private bool NavigateToIndex(int index) {
        if (index < 0 || index >= Nav.NavItems.Count)
            return false;

        Nav.Navigate(Nav.NavItems[index]);
        return true;
    }

    /// <summary>Selects the nav item hosting the given page. Resolved by page rather than by index so
    /// it survives a reordering of the bar.</summary>
    private bool NavigateToPage(ViewModelBase page) {
        foreach (var item in Nav.NavItems)
            if (item.Page == page)
                return NavigateToIndex(Nav.NavItems.IndexOf(item));

        return false;
    }

    /// <summary>Moves the selection <paramref name="delta"/> places along the bar, wrapping at both
    /// ends so Ctrl+Tab cycles indefinitely.</summary>
    private bool CycleTab(int delta) {
        var count = Nav.NavItems.Count;
        if (count == 0)
            return false;

        var next = (Nav.NavItems.IndexOf(Nav.SelectedNav) + delta + count) % count;
        return NavigateToIndex(next);
    }

    /// <summary>
    /// Builds the plain-text system report for the Export actions (toolbar Export, Settings "Export
    /// report" / "Copy diagnostics"). The Dashboard section leads; a Hardware summary and the primary
    /// network configuration follow so the report is an honest full-system snapshot. Called from the
    /// window / Settings code-behind, which own the save dialog + clipboard (they need the TopLevel).
    /// </summary>
    public DiagnosticsReport BuildReportModel() {
        var sections = new List<ReportSection>(_dashboard.GetReportSections());
        sections.Add(Section("Hardware", _hardware.GetReportRows()));
        sections.Add(Section("Network configuration", _network.GetPrimaryConfigRows()));
        sections.Add(Section("Storage", _storage.GetReportRows()));

        return new DiagnosticsReport("DashDetective — System Report", DateTime.Now, sections);
    }

    /// <summary>Wraps a page's key/value rows as a report section, so the pages keep supplying plain
    /// tuples and know nothing about the report model.</summary>
    private static ReportSection Section(string title, IReadOnlyList<(string Key, string Value)> rows) {
        var mapped = new List<ReportRow>(rows.Count);
        foreach (var (key, value) in rows)
            mapped.Add(new ReportRow(key, value));
        return new ReportSection(title, mapped);
    }

    /// <summary>The system report rendered in one format, for the Export actions.</summary>
    public string BuildReport(DiagnosticsFormat format) =>
        DiagnosticsFormats.Render(BuildReportModel(), format);

    /// <summary>Builds the rolling-history metrics CSV for the Settings "Export CSV" action.</summary>
    public string BuildMetricsCsv() => _dashboard.BuildMetricsCsv();

    /// <summary>Hosts the page for whichever nav item the bar selected, and moves activation with it.</summary>
    private void OnNavSelected(NavItem item) {
        CurrentPage = item.Page;
        UpdatePageActivity();
    }

    /// <summary>Whether the user still has to be told that closing the window leaves the app running in
    /// the tray. Read by the window, which owns the dialog (it needs a <c>TopLevel</c> to show one).</summary>
    public bool NeedsTrayNotice => !_trayNoticeShown;

    /// <summary>Records that the notice has been shown, so it never appears again.</summary>
    public void MarkTrayNoticeShown() {
        if (_trayNoticeShown)
            return;

        _trayNoticeShown = true;
        Persist();
    }

    /// <summary>Reports whether the window is on screen — hiding to the tray idles every page, since a
    /// process nobody can see should not be sampling. Called by the window, which owns hide/show.</summary>
    public void SetWindowVisible(bool visible) {
        if (visible == _windowVisible)
            return;

        _windowVisible = visible;
        UpdatePageActivity();
    }

    /// <summary>Activates the visible page and deactivates every other, routed through the
    /// <see cref="IActivatablePage"/> marker so no per-page wiring lives here. A page opts in by
    /// implementing it; the rest (Hardware, Toolkit, Settings, File Explorer) have nothing to stop.</summary>
    private void UpdatePageActivity() {
        foreach (var item in Nav.NavItems)
            (item.Page as IActivatablePage)?.SetActive(_windowVisible && item.Page == CurrentPage);
    }

    // ----- Search jumps -----
    // Each is "switch to the page, then ask it to reveal the thing". Navigating first matters: a page
    // that isn't current has no visual tree, so it has nothing to scroll or focus yet.

    /// <summary>Opens Settings with the given setting scrolled into view and flashed.</summary>
    private void RevealSetting(SettingId id) {
        NavigateToPage(_settings);
        _settings.Reveal(id);
    }

    /// <summary>Opens Help on a topic's own tab with that topic scrolled into view and flashed.</summary>
    private void RevealHelp(HelpTab tab, string topicKey) {
        Help.Open();
        Help.Reveal(tab, topicKey);
    }

    /// <summary>Opens the Toolkit with the given command scrolled into view and flashed.</summary>
    private void RevealToolkit(string command) {
        NavigateToPage(_toolkit);
        _toolkit.Reveal(command);
    }

    /// <summary>Opens Processes filtered to the given process, with its row selected.</summary>
    private void RevealProcess(int pid) {
        NavigateToPage(_processes);
        _processes.Reveal(pid);
    }

    /// <summary>Opens the File Explorer at a path: into a folder, or at a file's folder with the file
    /// selected.</summary>
    private void RevealFile(string path) {
        NavigateToPage(_fileExplorer);
        _fileExplorer.Reveal(path);
    }

    /// <summary>Opens Storage with the given physical disk's card selected.</summary>
    private void RevealDrive(int diskNumber) {
        NavigateToPage(_storage);
        _storage.Reveal(diskNumber);
    }

    /// <summary>Opens Network with the named adapter's row flashed in the Adapters panel.</summary>
    private void RevealAdapter(string adapterName) {
        NavigateToPage(_network);
        _network.Reveal(adapterName);
    }

    /// <summary>Opens Performance with the named device's rail row selected.</summary>
    private void RevealResource(string deviceId) {
        NavigateToPage(_performance);
        _performance.Reveal(deviceId);
    }

    /// <summary>Opens Hardware. No reveal: it is a static spec sheet with nothing to select.</summary>
    private void ShowHardware() => NavigateToPage(_hardware);

    /// <summary>Disposes the page view models, the shared metrics service and the settings store on
    /// shutdown, flushing any pending save. Driven by the composition root.</summary>
    public void Dispose() {
        _clockTimer.Stop();
        Search.Dispose();
        _alerts.AlertChanged -= OnAlertChanged;
        _alerts.Dispose();
        foreach (var item in Nav.NavItems)
            (item.Page as IDisposable)?.Dispose();
        _metrics.Dispose();
        _store.Dispose();
    }
}
