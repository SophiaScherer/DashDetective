using System;
using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashDetective.Services.Theming;
using DashDetective.Shared;
using DashDetective.Shell.Navigation;
using DashDetective.Tabs.Dashboard;
using DashDetective.Tabs.FileExplorer;
using DashDetective.Tabs.Settings;

namespace DashDetective.Shell;

public partial class MainWindowViewModel : ViewModelBase {
    private static readonly IBrush LiveDot = new SolidColorBrush(Color.Parse("#6ccb5f"));
    private static readonly IBrush PausedDot = new SolidColorBrush(Color.Parse("#9aa0a6"));

    private readonly ThemeService _theme = new();
    private readonly DashboardViewModel _dashboard = new();
    private readonly FileExplorerViewModel _fileExplorer = new();
    private readonly SettingsViewModel _settings;
    private readonly DispatcherTimer _clockTimer;

    [ObservableProperty] private ViewModelBase _currentPage;
    [ObservableProperty] private NavItem _selectedNav;

    /// <summary>Live wall clock shown at the right of the toolbar (24-hour HH:mm:ss).</summary>
    [ObservableProperty] private string _clock = "";

    /// <summary>Whether live sampling is running. Drives the toolbar's Live pill.</summary>
    [ObservableProperty] private bool _isLive = true;

    public ObservableCollection<NavItem> NavItems { get; }

    public string LiveLabel => IsLive ? "Live" : "Paused";
    public IBrush LiveDotBrush => IsLive ? LiveDot : PausedDot;

    /// <summary>Whether the current page manages its own scrolling (e.g. File Explorer): such pages
    /// fill the viewport and scroll their own panes, so the shell hosts them in a bounded,
    /// non-scrolling container instead of the page-scrolling <c>ScrollViewer</c>.</summary>
    public bool CurrentPageSelfScrolls => CurrentPage is ISelfScrollingPage;

    /// <summary>The current page routed to the scrolling host (null when it self-scrolls). Routing to
    /// null keeps the inactive host empty so the view is only ever instantiated once.</summary>
    public ViewModelBase? ScrollingPage => CurrentPage is ISelfScrollingPage ? null : CurrentPage;

    /// <summary>The current page routed to the bounded, self-scrolling host (null otherwise).</summary>
    public ViewModelBase? SelfScrollingPage => CurrentPage is ISelfScrollingPage ? CurrentPage : null;

    public MainWindowViewModel() {
        // Apply the default appearance (Dark + Blue) through the single theming seam,
        // then hand the same service to the Settings page so its controls drive it.
        _theme.ApplyDefaults();
        _settings = new SettingsViewModel(_theme);

        NavItems = new ObservableCollection<NavItem> {
            new NavItem("Dashboard", "Dashboard", "Real-time system overview",
                        Icons.Dashboard, _dashboard, Navigate),
            new NavItem("File Explorer", "File Explorer", "Browse files and folders",
                        Icons.FileExplorer, _fileExplorer, Navigate),
            new NavItem("Settings", "Settings", "Application preferences",
                        Icons.Settings, _settings, Navigate),
        };

        _selectedNav = NavItems[0];
        _selectedNav.IsSelected = true;
        _currentPage = _selectedNav.Page;

        // Seed once so the clock is correct on the first frame, then tick every second.
        UpdateClock();
        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick += (_, _) => UpdateClock();
        _clockTimer.Start();
    }

    private void UpdateClock() =>
        Clock = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);

    partial void OnCurrentPageChanged(ViewModelBase value) {
        OnPropertyChanged(nameof(CurrentPageSelfScrolls));
        OnPropertyChanged(nameof(ScrollingPage));
        OnPropertyChanged(nameof(SelfScrollingPage));
    }

    partial void OnIsLiveChanged(bool value) {
        OnPropertyChanged(nameof(LiveLabel));
        OnPropertyChanged(nameof(LiveDotBrush));
    }

    /// <summary>Pauses/resumes all live metric sampling on the Dashboard.</summary>
    [RelayCommand]
    private void ToggleLive() {
        IsLive = !IsLive;
        _dashboard.SetLive(IsLive);
    }

    /// <summary>Refreshes whichever page is current: the Dashboard re-samples its metrics, the File
    /// Explorer reloads its current folder. Pages that don't implement <see cref="IRefreshablePage"/>
    /// (e.g. Settings) simply ignore it.</summary>
    [RelayCommand]
    private void Refresh() => (CurrentPage as IRefreshablePage)?.Refresh();

    /// <summary>
    /// Builds the plain-text diagnostics report for the Export action. Called from the window
    /// code-behind, which owns the file-save dialog (it needs the window's <c>TopLevel</c>).
    /// </summary>
    public string BuildReport() => _dashboard.BuildDiagnosticsReport();

    private void Navigate(NavItem item) {
        if (item == SelectedNav)
            return;

        SelectedNav.IsSelected = false;
        SelectedNav = item;
        item.IsSelected = true;
        CurrentPage = item.Page;
    }
}
