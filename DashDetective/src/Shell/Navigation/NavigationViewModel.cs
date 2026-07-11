using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

    /// <summary>Whether the bar is collapsed to an icons-only rail. Session-only, like Theming.</summary>
    [ObservableProperty] private bool _isCollapsed;

    /// <summary>The navigation entries shown on the bar, in display order.</summary>
    public ObservableCollection<NavItem> NavItems { get; } = new();

    /// <summary>Raised whenever the selected item changes (including the initial selection), so the
    /// shell can route the item's page into the content host.</summary>
    public event Action<NavItem>? SelectionChanged;

    /// <summary>Which window edge the bar docks to. Fixed to the left for now; user-selectable
    /// orientation is added in a later phase.</summary>
    public Dock Dock => Dock.Left;

    // ----- Computed layout (no converters; consumed by NavigationView bindings/styles) -----

    /// <summary>Rail width: the full bar when expanded, a narrow icons-only rail when collapsed.</summary>
    public double RailWidth => IsCollapsed ? 64 : 236;

    /// <summary>Whether nav-item text labels are shown (hidden when collapsed to icons-only).</summary>
    public bool ShowLabels => !IsCollapsed;

    /// <summary>Whether the brand wordmark (beside the logo tile) is shown.</summary>
    public bool ShowBrandText => !IsCollapsed;

    /// <summary>Whether the footer shows the full user card (vs. a compact avatar when collapsed).</summary>
    public bool ShowFullFooter => !IsCollapsed;

    /// <summary>Toggles the collapsed (icons-only) state of the bar.</summary>
    [RelayCommand]
    private void ToggleCollapse() => IsCollapsed = !IsCollapsed;

    partial void OnIsCollapsedChanged(bool value) {
        OnPropertyChanged(nameof(RailWidth));
        OnPropertyChanged(nameof(ShowLabels));
        OnPropertyChanged(nameof(ShowBrandText));
        OnPropertyChanged(nameof(ShowFullFooter));
    }

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
