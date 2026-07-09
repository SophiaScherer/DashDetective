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
using DashDetective.Tabs.Settings;

namespace DashDetective.Shell;

public partial class MainWindowViewModel : ViewModelBase {
    private static readonly IBrush LiveDot = new SolidColorBrush(Color.Parse("#6ccb5f"));
    private static readonly IBrush PausedDot = new SolidColorBrush(Color.Parse("#9aa0a6"));

    private readonly ThemeService _theme = new();
    private readonly DashboardViewModel _dashboard = new();
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

    public MainWindowViewModel() {
        // Apply the default appearance (Dark + Blue) through the single theming seam,
        // then hand the same service to the Settings page so its controls drive it.
        _theme.ApplyDefaults();
        _settings = new SettingsViewModel(_theme);

        NavItems = new ObservableCollection<NavItem> {
            new NavItem("Dashboard", "Dashboard", "Real-time system overview",
                        Icons.Dashboard, _dashboard, Navigate),
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

    /// <summary>Forces an immediate re-read of every Dashboard metric and static info.</summary>
    [RelayCommand]
    private void Refresh() => _dashboard.RefreshNow();

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
