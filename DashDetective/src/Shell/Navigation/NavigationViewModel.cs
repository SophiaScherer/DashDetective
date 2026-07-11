using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using DashDetective.Shared;

namespace DashDetective.Shell.Navigation;

/// <summary>
/// Backs the shell's navigation bar: owns the nav items and the single-selection state, and raises
/// <see cref="SelectionChanged"/> so the shell can host the selected page. Kept separate from
/// <c>MainWindowViewModel</c> so the bar's layout state (dock edge, collapse) lives as one cohesive
/// unit; the collapse and orientation options are added in later phases.
/// </summary>
public partial class NavigationViewModel : ViewModelBase {
    [ObservableProperty] private NavItem _selectedNav = null!;

    /// <summary>The navigation entries shown on the bar, in display order.</summary>
    public ObservableCollection<NavItem> NavItems { get; } = new();

    /// <summary>Raised whenever the selected item changes (including the initial selection), so the
    /// shell can route the item's page into the content host.</summary>
    public event Action<NavItem>? SelectionChanged;

    /// <summary>Which window edge the bar docks to. Fixed to the left for now; user-selectable
    /// orientation is added in a later phase.</summary>
    public Dock Dock => Dock.Left;

    /// <summary>Populates the bar and selects the first item. Items must be created with
    /// <see cref="Navigate"/> as their select callback so clicks route back here.</summary>
    public void Initialize(IEnumerable<NavItem> items) {
        foreach (var item in items)
            NavItems.Add(item);

        SelectedNav = NavItems[0];
        SelectedNav.IsSelected = true;
        SelectionChanged?.Invoke(SelectedNav);
    }

    /// <summary>Selects a nav item (single-select) and notifies the shell to host its page.
    /// No-ops when the item is already selected.</summary>
    public void Navigate(NavItem item) {
        if (item == SelectedNav)
            return;

        SelectedNav.IsSelected = false;
        SelectedNav = item;
        item.IsSelected = true;
        SelectionChanged?.Invoke(item);
    }
}
