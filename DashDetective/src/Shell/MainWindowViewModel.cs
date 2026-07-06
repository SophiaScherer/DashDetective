using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DashDetective.Shared;
using DashDetective.Shell.Navigation;
using DashDetective.Tabs.Dashboard;
using DashDetective.Tabs.Settings;

namespace DashDetective.Shell;

public partial class MainWindowViewModel : ViewModelBase {
    private readonly DashboardViewModel _dashboard = new();
    private readonly SettingsViewModel _settings = new();

    [ObservableProperty] private ViewModelBase _currentPage;
    [ObservableProperty] private NavItem _selectedNav;

    public ObservableCollection<NavItem> NavItems { get; }

    public MainWindowViewModel() {
        NavItems = new ObservableCollection<NavItem> {
            new NavItem("Dashboard", "Dashboard", "Real-time system overview",
                        Icons.Dashboard, _dashboard, Navigate),
            new NavItem("Settings", "Settings", "Application preferences",
                        Icons.Settings, _settings, Navigate),
        };

        _selectedNav = NavItems[0];
        _selectedNav.IsSelected = true;
        _currentPage = _selectedNav.Page;
    }

    private void Navigate(NavItem item) {
        if (item == SelectedNav)
            return;

        SelectedNav.IsSelected = false;
        SelectedNav = item;
        item.IsSelected = true;
        CurrentPage = item.Page;
    }
}
