using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashDetective.Services.Settings;
using DashDetective.Services.SystemMetrics;
using DashDetective.Services.Theming;
using DashDetective.Shared;
using DashDetective.Shell.Navigation;
using DashDetective.Tabs.Dashboard;
using DashDetective.Tabs.FileExplorer;
using DashDetective.Tabs.Hardware;
using DashDetective.Tabs.Network;
using DashDetective.Tabs.Performance;
using DashDetective.Tabs.Processes;
using DashDetective.Tabs.Settings;
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
    private readonly HardwareViewModel _hardware = new();
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

    public string LiveLabel => IsLive ? "Live" : "Paused";
    public IBrush LiveDotBrush => IsLive ? LiveDot : PausedDot;

    /// <summary>Whether the app should hide to the tray (rather than exit) when the window is closed.
    /// Read by the window's close handler; kept in sync with the Settings toggle.</summary>
    public bool ShowInTray => _settings.ShowInTray;

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

        // Apply the persisted appearance + layout through the seams that own them, before wiring the
        // controls that observe them. ThemeService stays the only code that writes to the application.
        ApplySettings(settings);

        // Build the Settings page with the shared theming seam + nav, the metrics service (refresh
        // interval), the loaded settings (toggle/interval seed) and the report/CSV builders.
        _settings = new SettingsViewModel(_theme, Nav, metrics, settings, BuildReport, BuildMetricsCsv);

        // Persist whenever a control changes. The store debounces, so calling Persist freely is fine.
        _settings.Changed += OnSettingChanged;
        Nav.PropertyChanged += OnNavPropertyChanged;
        _fileExplorer.PropertyChanged += OnFileExplorerPropertyChanged;
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
            new NavItem("Hardware", "Hardware", "Installed components & specs",
                        Icons.Hardware, _hardware, Nav.Navigate),
            new NavItem("Settings", "Settings", "Application preferences",
                        Icons.Settings, _settings, Nav.Navigate),
        });
        _currentPage = Nav.SelectedNav.Page;
        Nav.SelectionChanged += OnNavSelected;

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
        LaunchAtStartup = _settings.LaunchAtStartup,
        ShowInTray = _settings.ShowInTray,
        ResourceAlerts = _settings.ResourceAlerts,
    };

    /// <summary>Debounced save of the current settings snapshot.</summary>
    private void Persist() => _store.Save(CaptureCurrent());

    /// <summary>A Settings control changed: persist, refresh the tray-close flag, and re-evaluate the
    /// alert banner (the "Resource alerts" toggle gates it).</summary>
    private void OnSettingChanged() {
        Persist();
        OnPropertyChanged(nameof(ShowInTray));
        UpdateAlertBanner();
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

        return sb.ToString();
    }

    /// <summary>Appends a left-aligned "key: value" line, matching the Dashboard report's layout.</summary>
    private static void AppendReportRow(StringBuilder sb, string key, string value) =>
        sb.AppendLine($"  {(key + ":").PadRight(14)}{value}");

    /// <summary>Builds the rolling-history metrics CSV for the Settings "Export CSV" action.</summary>
    public string BuildMetricsCsv() => _dashboard.BuildMetricsCsv();

    /// <summary>Hosts the page for whichever nav item the bar selected.</summary>
    private void OnNavSelected(NavItem item) => CurrentPage = item.Page;

    /// <summary>Disposes the page view models, the shared metrics service and the settings store on
    /// shutdown, flushing any pending save. Driven by the composition root.</summary>
    public void Dispose() {
        _clockTimer.Stop();
        _metrics.AlertActiveChanged -= OnAlertActiveChanged;
        foreach (var item in Nav.NavItems)
            (item.Page as IDisposable)?.Dispose();
        _metrics.Dispose();
        _store.Dispose();
    }
}
