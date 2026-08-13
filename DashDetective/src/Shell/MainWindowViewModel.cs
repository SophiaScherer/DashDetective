using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashDetective.Services.Search;
using DashDetective.Services.Settings;
using DashDetective.Services.Startup;
using DashDetective.Services.SystemMetrics;
using DashDetective.Services.Theming;
using DashDetective.Shared;
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
using System.ComponentModel;
using System.Globalization;
using System.Text;

namespace DashDetective.Shell;

public partial class MainWindowViewModel : ViewModelBase, IDisposable {
    private static readonly IBrush LiveDot = new SolidColorBrush(Color.Parse("#6ccb5f"));
    private static readonly IBrush PausedDot = new SolidColorBrush(Color.Parse("#9aa0a6"));

    private const string AlertMessage = "High resource usage — CPU or memory has stayed above 90%.";

    private readonly SystemMetricsService _metrics;
    private readonly SettingsStore _store;
    private readonly ThemeService _theme = new();
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

    // Resource-alert banner state: whether the metrics service reports an active breach, and whether the
    // user dismissed the current one. The banner shows only while active, unignored, and alerts are on.
    private bool _alertActive;
    private bool _alertDismissed;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentPageSelfScrolls), nameof(ScrollingPage), nameof(SelfScrollingPage))]
    private ViewModelBase _currentPage;

    /// <summary>Live wall clock shown at the right of the toolbar (24-hour HH:mm:ss).</summary>
    [ObservableProperty] private string _clock = "";

    /// <summary>Whether the resource-alert banner is currently shown in the shell.</summary>
    [ObservableProperty] private bool _alertBannerVisible;

    /// <summary>The resource-alert banner message.</summary>
    [ObservableProperty] private string _alertText = AlertMessage;

    /// <summary>Whether live sampling is running. Drives the toolbar's Live pill.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LiveLabel), nameof(LiveDotBrush))]
    private bool _isLive = true;

    /// <summary>The navigation bar: owns the nav items and selection; the shell hosts the page it
    /// selects (see <see cref="OnNavSelected"/>) and the toolbar reads its title/subtitle.</summary>
    public NavigationViewModel Nav { get; } = new();

    /// <summary>The Help modal. Owned here rather than by the nav bar because the overlay covers the
    /// whole window, navigation bar included.</summary>
    public HelpViewModel Help { get; } = new();

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

    /// <summary>Whether the app should hide to the tray (rather than exit) when the window is closed.
    /// Gated on <see cref="TrayIntegration.HidesOnClose"/>, so a desktop with no tray closes normally
    /// however the setting is left — hiding behind an icon that never appears would strand the app.</summary>
    public bool ShowInTray => _settings.ShowInTray && TrayIntegration.HidesOnClose;

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
        _dashboard = new DashboardViewModel(metrics);
        _processes = new ProcessesViewModel(metrics);
        _performance = new PerformanceViewModel(metrics);
        _storage = new StorageViewModel(metrics);

        // Apply the persisted appearance + layout through the seams that own them, before wiring the
        // controls that observe them. ThemeService stays the only code that writes to the application.
        ApplySettings(settings);

        // Build the Settings page with the shared theming seam + nav, the metrics service (refresh
        // interval), the loaded settings (toggle/interval seed) and the report/CSV builders.
        _settings = new SettingsViewModel(_theme, Nav, metrics, settings,
                                          IStartupRegistration.ForCurrentPlatform(),
                                          BuildReport, BuildMetricsCsv);

        // Persist whenever a control changes. The store debounces, so calling Persist freely is fine.
        _settings.Changed += OnSettingChanged;
        Nav.HelpRequested += Help.Open;
        Nav.PropertyChanged += OnNavPropertyChanged;
        _fileExplorer.PropertyChanged += OnFileExplorerPropertyChanged;
        _performance.ScopeChanged += Persist;
        _performance.DetailChanged += Persist;
        _metrics.AlertActiveChanged += OnAlertActiveChanged;

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

        // Built after the bar so the page provider can read the live nav items rather than a copy. Each
        // provider's "go there" callback is a closure over the page it targets, which is why search is
        // assembled here: this is the one class already holding every page instance.
        Search = new UniversalSearchViewModel([
            new PageSearchProvider(Nav.NavItems, Nav.Navigate),
            new SettingSearchProvider(RevealSetting, Icons.Settings),
            new ShortcutSearchProvider(Help.Open, Icons.Help),
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

        // Seed once so the clock is correct on the first frame, then tick every second.
        UpdateClock();
        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick += (_, _) => UpdateClock();
        _clockTimer.Start();
    }

    /// <summary>Applies persisted appearance + layout through the owning seams: theme/accent via
    /// <see cref="ThemeService"/>, dock/collapse via <see cref="Nav"/>, and show-hidden via the File
    /// Explorer. The refresh interval and toggles are applied by <see cref="SettingsViewModel"/>.</summary>
    private void ApplySettings(AppSettings settings) {
        _theme.ApplyTheme(settings.Theme);
        var accent = FindAccent(settings.AccentName);
        if (accent is { } preset)
            _theme.ApplyAccent(preset);
        else
            _theme.ApplyDefaultAppearance();

        Nav.Orientation = settings.NavOrientation;
        Nav.IsCollapsed = settings.NavCollapsed;
        _fileExplorer.ShowHidden = settings.ShowHiddenFiles;

        // Commands before pins: a pin naming one of the user's own commands has nothing to find until
        // that command is on the page.
        _toolkit.LoadCommands(settings.CustomCommands);
        _toolkit.LoadPins(settings.PinnedCommands);
        _performance.ShowAllDevices = settings.PerformanceShowAllDevices;
        _performance.GpuDetailedView = settings.GpuDetailedView;
        _performance.CpuDetailedView = settings.CpuDetailedView;
        ApplyNvidiaGpuMetrics(settings.NvidiaGpuMetrics);
        _recents.Load(settings.RecentSearches);
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
        RefreshIntervalSeconds = _settings.SelectedIntervalSeconds,
        ShowHiddenFiles = _fileExplorer.ShowHidden,
        PinnedCommands = _toolkit.EncodePins(),
        CustomCommands = _toolkit.EncodeCommands(),
        LaunchAtStartup = _settings.LaunchAtStartup,
        ShowInTray = _settings.ShowInTray,
        ResourceAlerts = _settings.ResourceAlerts,
        NvidiaGpuMetrics = _settings.NvidiaGpuMetrics,
        PerformanceShowAllDevices = _performance.ShowAllDevices,
        GpuDetailedView = _performance.GpuDetailedView,
        CpuDetailedView = _performance.CpuDetailedView,
        RecentSearches = _recents.Encode(),
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

    /// <summary>The metrics service flipped the resource-alert state. Recovery clears any dismissal so a
    /// later breach shows again; then re-evaluate the banner.</summary>
    private void OnAlertActiveChanged(bool active) {
        _alertActive = active;
        if (!active)
            _alertDismissed = false;
        UpdateAlertBanner();
    }

    /// <summary>The banner shows only while a breach is active, the "Resource alerts" setting is on, and
    /// the user hasn't dismissed the current breach.</summary>
    private void UpdateAlertBanner() =>
        AlertBannerVisible = _alertActive && _settings.ResourceAlerts && !_alertDismissed;

    /// <summary>Dismisses the current alert banner (until usage recovers and breaches again).</summary>
    [RelayCommand]
    private void DismissAlert() {
        _alertDismissed = true;
        UpdateAlertBanner();
    }

    private void UpdateClock() =>
        Clock = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);

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
        // Nothing higher up the chain claimed Esc, so the only thing left to dismiss is the banner.
        ShortcutId.Escape => DismissAlertIfShowing(),
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

    /// <summary>Dismisses the resource-alert banner, or leaves Esc unconsumed when it isn't showing.</summary>
    private bool DismissAlertIfShowing() {
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
    public string BuildReport() {
        var sb = new StringBuilder();
        sb.Append(_dashboard.BuildDiagnosticsReport());
        sb.AppendLine();

        sb.AppendLine("Hardware");
        foreach (var (key, value) in _hardware.GetReportRows())
            AppendReportRow(sb, key, value);
        sb.AppendLine();

        sb.AppendLine("Network configuration");
        foreach (var (key, value) in _network.GetPrimaryConfigRows())
            AppendReportRow(sb, key, value);
        sb.AppendLine();

        sb.AppendLine("Storage");
        foreach (var (key, value) in _storage.GetReportRows())
            AppendReportRow(sb, key, value);

        return sb.ToString();
    }

    /// <summary>Appends a left-aligned "key: value" line, matching the Dashboard report's layout.</summary>
    private static void AppendReportRow(StringBuilder sb, string key, string value) =>
        sb.AppendLine($"  {(key + ":").PadRight(14)}{value}");

    /// <summary>Builds the rolling-history metrics CSV for the Settings "Export CSV" action.</summary>
    public string BuildMetricsCsv() => _dashboard.BuildMetricsCsv();

    /// <summary>Hosts the page for whichever nav item the bar selected.</summary>
    private void OnNavSelected(NavItem item) => CurrentPage = item.Page;

    // ----- Search jumps -----
    // Each is "switch to the page, then ask it to reveal the thing". Navigating first matters: a page
    // that isn't current has no visual tree, so it has nothing to scroll or focus yet.

    /// <summary>Opens Settings with the given setting scrolled into view and flashed.</summary>
    private void RevealSetting(SettingId id) {
        NavigateToPage(_settings);
        _settings.Reveal(id);
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

    /// <summary>Disposes the page view models, the shared metrics service and the settings store on
    /// shutdown, flushing any pending save. Driven by the composition root.</summary>
    public void Dispose() {
        _clockTimer.Stop();
        Search.Dispose();
        _metrics.AlertActiveChanged -= OnAlertActiveChanged;
        foreach (var item in Nav.NavItems)
            (item.Page as IDisposable)?.Dispose();
        _metrics.Dispose();
        _store.Dispose();
    }
}
