using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashDetective.Shared;

namespace DashDetective.Shell.Navigation;

/// <summary>
/// Backs the shell's navigation bar: owns the nav items and the single-selection state, and raises
/// <see cref="SelectionChanged"/> so the shell can host the selected page. Kept separate from
/// <c>MainWindowViewModel</c> so the bar's layout state (dock edge, collapse) lives as one cohesive
/// unit. Orientation and collapse drive the bar's layout entirely through computed properties, so no
/// value converters are needed. All state is session-only, matching the Theming conventions.
/// </summary>
public partial class NavigationViewModel : ViewModelBase {
    [ObservableProperty] private NavItem _selectedNav = null!;

    /// <summary>Whether the bar is collapsed to an icons-only rail. Session-only, like Theming.</summary>
    [ObservableProperty] private bool _isCollapsed;

    /// <summary>Which window edge the bar docks to. Session-only, like Theming.</summary>
    [ObservableProperty] private NavOrientation _orientation = NavOrientation.Left;

    /// <summary>The navigation entries shown on the bar, in display order.</summary>
    public ObservableCollection<NavItem> NavItems { get; } = new();

    /// <summary>The four dock-position choices, shared by the on-bar flyout and the Settings control.</summary>
    public ObservableCollection<NavPositionOption> Positions { get; }

    /// <summary>Raised whenever the selected item changes (including the initial selection), so the
    /// shell can route the item's page into the content host.</summary>
    public event Action<NavItem>? SelectionChanged;

    public NavigationViewModel() {
        Positions = new ObservableCollection<NavPositionOption> {
            new("Left", NavOrientation.Left, SelectPosition),
            new("Top", NavOrientation.Top, SelectPosition),
            new("Right", NavOrientation.Right, SelectPosition),
            new("Bottom", NavOrientation.Bottom, SelectPosition),
        };
        SyncPositions();
    }

    // ----- Computed layout (no converters; consumed by NavigationView bindings/styles) -----

    /// <summary>Whether the bar runs horizontally (docked to the top or bottom edge).</summary>
    public bool IsHorizontal => Orientation is NavOrientation.Top or NavOrientation.Bottom;

    /// <summary>Which edge of the window the bar docks to.</summary>
    public Dock Dock => Orientation switch {
        NavOrientation.Left => Dock.Left,
        NavOrientation.Right => Dock.Right,
        NavOrientation.Top => Dock.Top,
        _ => Dock.Bottom,
    };

    /// <summary>The edge the brand/toggle dock to inside the bar (start of the running axis).</summary>
    public Dock BrandDock => IsHorizontal ? Dock.Left : Dock.Top;

    /// <summary>The edge the footer docks to inside the bar (end of the running axis).</summary>
    public Dock FooterDock => IsHorizontal ? Dock.Right : Dock.Bottom;

    /// <summary>The axis the nav items flow along: horizontal for a top/bottom bar, else vertical.</summary>
    public Orientation ItemsOrientation =>
        IsHorizontal ? Avalonia.Layout.Orientation.Horizontal : Avalonia.Layout.Orientation.Vertical;

    /// <summary>How the item list sits on the cross axis: centred in a short horizontal bar, top-
    /// aligned (just under the brand) in a tall vertical rail.</summary>
    public VerticalAlignment ItemsVAlign => IsHorizontal ? VerticalAlignment.Center : VerticalAlignment.Top;

    /// <summary>Rail width. <see cref="double.NaN"/> (auto) when horizontal so it stretches to the
    /// docked edge; a fixed rail (full or collapsed) when vertical.</summary>
    public double RailWidth => IsHorizontal ? double.NaN : (IsCollapsed ? 64 : 236);

    /// <summary>Rail height. A fixed bar (full or collapsed) when horizontal; <see cref="double.NaN"/>
    /// (auto) when vertical so it stretches to the docked edge.</summary>
    public double RailHeight => IsHorizontal ? (IsCollapsed ? 54 : 64) : double.NaN;

    /// <summary>The bar's separator hairline, drawn only on the edge that faces the content area.</summary>
    public Thickness HairlineThickness => Orientation switch {
        NavOrientation.Left => new Thickness(0, 0, 1, 0),
        NavOrientation.Right => new Thickness(1, 0, 0, 0),
        NavOrientation.Top => new Thickness(0, 0, 0, 1),
        _ => new Thickness(0, 1, 0, 0),
    };

    /// <summary>Vertical scrollbar policy for the item list (only vertical bars scroll vertically).</summary>
    public ScrollBarVisibility ScrollV => IsHorizontal ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto;

    /// <summary>Horizontal scrollbar policy for the item list (only horizontal bars scroll sideways).</summary>
    public ScrollBarVisibility ScrollH => IsHorizontal ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled;

    /// <summary>Whether nav-item text labels are shown (hidden when collapsed to icons-only).</summary>
    public bool ShowLabels => !IsCollapsed;

    /// <summary>Whether the brand wordmark (beside the logo tile) is shown. Hidden when collapsed or
    /// when horizontal (the short bar shows the logo only).</summary>
    public bool ShowBrandText => !IsCollapsed && !IsHorizontal;

    /// <summary>Whether the footer shows the full user card (vs. a compact avatar). Full only on an
    /// expanded vertical bar.</summary>
    public bool ShowFullFooter => !IsCollapsed && !IsHorizontal;

    /// <summary>The panel-split glyph shown on the collapse toggle, its rail indicating the way the bar
    /// will move.</summary>
    public Geometry CollapseIcon => Icons.PanelGlyph(Orientation, IsCollapsed);

    /// <summary>Toggles the collapsed (icons-only) state of the bar.</summary>
    [RelayCommand]
    private void ToggleCollapse() => IsCollapsed = !IsCollapsed;

    /// <summary>Expands the bar (used by the Settings control).</summary>
    [RelayCommand]
    private void Expand() => IsCollapsed = false;

    /// <summary>Collapses the bar to icons-only (used by the Settings control).</summary>
    [RelayCommand]
    private void Collapse() => IsCollapsed = true;

    /// <summary>Docks the bar to the given window edge.</summary>
    [RelayCommand]
    private void SetOrientation(NavOrientation orientation) => Orientation = orientation;

    private void SelectPosition(NavPositionOption option) => Orientation = option.Value;

    private void SyncPositions() {
        foreach (var position in Positions)
            position.IsSelected = position.Value == Orientation;
    }

    partial void OnIsCollapsedChanged(bool value) {
        OnPropertyChanged(nameof(RailWidth));
        OnPropertyChanged(nameof(RailHeight));
        OnPropertyChanged(nameof(ShowLabels));
        OnPropertyChanged(nameof(ShowBrandText));
        OnPropertyChanged(nameof(ShowFullFooter));
        OnPropertyChanged(nameof(CollapseIcon));
    }

    partial void OnOrientationChanged(NavOrientation value) {
        OnPropertyChanged(nameof(IsHorizontal));
        OnPropertyChanged(nameof(Dock));
        OnPropertyChanged(nameof(BrandDock));
        OnPropertyChanged(nameof(FooterDock));
        OnPropertyChanged(nameof(ItemsOrientation));
        OnPropertyChanged(nameof(ItemsVAlign));
        OnPropertyChanged(nameof(RailWidth));
        OnPropertyChanged(nameof(RailHeight));
        OnPropertyChanged(nameof(HairlineThickness));
        OnPropertyChanged(nameof(ScrollV));
        OnPropertyChanged(nameof(ScrollH));
        OnPropertyChanged(nameof(ShowBrandText));
        OnPropertyChanged(nameof(ShowFullFooter));
        OnPropertyChanged(nameof(CollapseIcon));
        SyncPositions();
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
