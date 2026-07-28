using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashDetective.Services.Identity;
using DashDetective.Shared;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace DashDetective.Shell.Navigation;

/// <summary>
/// Backs the shell's navigation bar: owns the nav items and the single-selection state, and raises
/// <see cref="SelectionChanged"/> so the shell can host the selected page. Kept separate from
/// <c>MainWindowViewModel</c> so the bar's layout state (dock edge, collapse) lives as one cohesive
/// unit. Orientation and collapse drive the bar's layout entirely through computed properties, so no
/// value converters are needed. Both persist via <c>SettingsStore</c>, which observes them through
/// <c>MainWindowViewModel</c>; the purely visual flags (<see cref="IsDragging"/>) do not.
/// </summary>
public partial class NavigationViewModel : ViewModelBase {
    [ObservableProperty] private NavItem _selectedNav = null!;

    /// <summary>Whether the bar is collapsed to an icons-only rail. Persisted.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RailWidth), nameof(RailHeight), nameof(ShowLabels),
        nameof(ShowBrandText), nameof(ShowFullFooter),
        nameof(ChevronPointing), nameof(ChevronIcon),
        nameof(ControlsDock), nameof(FooterAvatarDock))]
    private bool _isCollapsed;

    /// <summary>Which window edge the bar docks to. Persisted.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsHorizontal), nameof(Dock), nameof(BrandDock), nameof(FooterDock),
        nameof(ItemsOrientation), nameof(ItemsVAlign), nameof(RailWidth), nameof(RailHeight),
        nameof(HairlineThickness), nameof(ScrollV), nameof(ScrollH), nameof(ShowBrandText),
        nameof(ShowFullFooter), nameof(ChevronIcon),
        nameof(ControlsDock), nameof(FooterAvatarDock),
        nameof(ChevronPointing), nameof(ChevronWidth), nameof(ChevronHeight),
        nameof(ChevronHAlign), nameof(ChevronVAlign),
        nameof(ChevronMargin), nameof(ChevronCornerRadius))]
    private NavOrientation _orientation = NavOrientation.Left;

    /// <summary>The navigation entries shown on the bar, in display order.</summary>
    public ObservableCollection<NavItem> NavItems { get; } = new();

    /// <summary>The four dock-position choices, shared by the on-bar flyout and the Settings control.</summary>
    public ObservableCollection<NavPositionOption> Positions { get; }

    /// <summary>Raised whenever the selected item changes (including the initial selection), so the
    /// shell can route the item's page into the content host.</summary>
    public event Action<NavItem>? SelectionChanged;

    /// <summary>Raised each time a dock position is chosen from the on-bar picker (even when it is the
    /// already-selected edge), so the view can dismiss the picker flyout. UI-only; carries no state.</summary>
    public event Action? PositionPicked;

    /// <summary>Raised when the on-bar Help button is pressed, so the shell can open the Help modal
    /// (the modal covers the whole window, so the shell owns it). UI-only; carries no state.</summary>
    public event Action? HelpRequested;

    /// <summary>Whether a drag-to-dock gesture is in progress, which dims the bar in place so it reads
    /// as being moved. UI-only and never persisted — the view sets it around the gesture.</summary>
    [ObservableProperty] private bool _isDragging;

    public NavigationViewModel() {
        Positions = new ObservableCollection<NavPositionOption> {
            new("Left", NavOrientation.Left, SelectPosition),
            new("Top", NavOrientation.Top, SelectPosition),
            new("Right", NavOrientation.Right, SelectPosition),
            new("Bottom", NavOrientation.Bottom, SelectPosition),
        };
        SyncPositions();

        var user = CurrentUserProvider.Load();
        UserName = user.DisplayName;
        UserInitials = user.Initials;
        UserRole = user.Role;
    }

    // ----- Footer identity (the interactive Windows user; read once at construction) -----

    /// <summary>The logged-in user's login name shown in the footer card.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UserTooltip))]
    private string _userName = "";

    /// <summary>Up to two letters shown in the footer avatar badge.</summary>
    [ObservableProperty] private string _userInitials = "";

    /// <summary>The account's privilege level ("Administrator" / "Standard User").</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UserTooltip))]
    private string _userRole = "";

    /// <summary>Name and role combined for the compact-avatar tooltip, e.g. "sophiasch — Administrator".</summary>
    public string UserTooltip => $"{UserName} — {UserRole}";

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

    /// <summary>How thick the bar is when docked on the given axis, at the current collapsed state.
    /// Takes the axis as an argument rather than reading <see cref="IsHorizontal"/> so the drag preview
    /// can size a drop band for an edge the bar is not on yet.</summary>
    public double RailThickness(bool horizontal) =>
        horizontal ? (IsCollapsed ? 54 : 64) : (IsCollapsed ? 64 : 236);

    /// <summary>Rail width. <see cref="double.NaN"/> (auto) when horizontal so it stretches to the
    /// docked edge; a fixed rail (full or collapsed) when vertical.</summary>
    public double RailWidth => IsHorizontal ? double.NaN : RailThickness(horizontal: false);

    /// <summary>Rail height. A fixed bar (full or collapsed) when horizontal; <see cref="double.NaN"/>
    /// (auto) when vertical so it stretches to the docked edge.</summary>
    public double RailHeight => IsHorizontal ? RailThickness(horizontal: true) : double.NaN;

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

    /// <summary>Where the footer's Help button sits relative to the avatar: beneath it on a collapsed
    /// vertical rail (64px is too narrow to fit both side by side), to its right otherwise.</summary>
    public Dock ControlsDock => IsCollapsed && !IsHorizontal ? Dock.Bottom : Dock.Right;

    /// <summary>Where the footer's avatar sits: the start of the same axis <see cref="ControlsDock"/>
    /// ends, so the two bracket the user's name when it is shown.</summary>
    public Dock FooterAvatarDock => IsCollapsed && !IsHorizontal ? Dock.Top : Dock.Left;

    // ----- Collapse/expand puck (the hover-revealed semi-circle on the bar's outer edge) -----

    /// <summary>Puck thickness across the bar's edge; half of it overhangs into the content area.</summary>
    private const double PuckThickness = 18;

    /// <summary>Puck length along the bar's edge.</summary>
    private const double PuckLength = 40;

    /// <summary>Which way the puck's chevron points: toward the docked edge when the bar is expanded (it
    /// will collapse), away from it when collapsed (it will expand).</summary>
    public ChevronDirection ChevronPointing => Orientation switch {
        NavOrientation.Left => IsCollapsed ? ChevronDirection.Right : ChevronDirection.Left,
        NavOrientation.Right => IsCollapsed ? ChevronDirection.Left : ChevronDirection.Right,
        NavOrientation.Top => IsCollapsed ? ChevronDirection.Down : ChevronDirection.Up,
        _ => IsCollapsed ? ChevronDirection.Up : ChevronDirection.Down,
    };

    /// <summary>The chevron glyph shown on the puck.</summary>
    public Geometry ChevronIcon => Icons.Chevron(ChevronPointing);

    /// <summary>Puck width: the thin axis on a vertical rail, the long axis on a horizontal bar.</summary>
    public double ChevronWidth => IsHorizontal ? PuckLength : PuckThickness;

    /// <summary>Puck height: the long axis on a vertical rail, the thin axis on a horizontal bar.</summary>
    public double ChevronHeight => IsHorizontal ? PuckThickness : PuckLength;

    /// <summary>Pins the puck to the bar's content-facing edge, centred on the other axis.</summary>
    public HorizontalAlignment ChevronHAlign => Orientation switch {
        NavOrientation.Left => HorizontalAlignment.Right,
        NavOrientation.Right => HorizontalAlignment.Left,
        _ => HorizontalAlignment.Center,
    };

    /// <summary>Pins the puck to the bar's content-facing edge, centred on the other axis.</summary>
    public VerticalAlignment ChevronVAlign => Orientation switch {
        NavOrientation.Top => VerticalAlignment.Bottom,
        NavOrientation.Bottom => VerticalAlignment.Top,
        _ => VerticalAlignment.Center,
    };

    /// <summary>Pulls the puck out by half its thickness so it straddles the edge, half over the content
    /// area. The bar is drawn above the content (ZIndex in the shell) so the overhang is visible.</summary>
    public Thickness ChevronMargin => Orientation switch {
        NavOrientation.Left => new Thickness(0, 0, -PuckThickness / 2, 0),
        NavOrientation.Right => new Thickness(-PuckThickness / 2, 0, 0, 0),
        NavOrientation.Top => new Thickness(0, 0, 0, -PuckThickness / 2),
        _ => new Thickness(0, -PuckThickness / 2, 0, 0),
    };

    /// <summary>Rounds only the overhanging half, so the puck reads as a semi-circle growing out of the
    /// bar's edge. The radius is oversized and clamps to the puck's half-thickness.</summary>
    public CornerRadius ChevronCornerRadius => Orientation switch {
        NavOrientation.Left => new CornerRadius(0, PuckLength, PuckLength, 0),
        NavOrientation.Right => new CornerRadius(PuckLength, 0, 0, PuckLength),
        NavOrientation.Top => new CornerRadius(0, 0, PuckLength, PuckLength),
        _ => new CornerRadius(PuckLength, PuckLength, 0, 0),
    };

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

    /// <summary>Asks the shell to open the Help modal.</summary>
    [RelayCommand]
    private void ShowHelp() => HelpRequested?.Invoke();

    private void SelectPosition(NavPositionOption option) {
        Orientation = option.Value;
        PositionPicked?.Invoke();
    }

    /// <summary>Docks the bar to an edge chosen by a drag gesture. Same effect as the picker, so the
    /// Settings control and on-bar flyout stay in sync via <see cref="OnOrientationChanged"/>.</summary>
    public void DockTo(NavOrientation orientation) => Orientation = orientation;

    private void SyncPositions() {
        foreach (var position in Positions)
            position.IsSelected = position.Value == Orientation;
    }

    // The computed layout properties are fanned out via [NotifyPropertyChangedFor] on the source fields;
    // this hook only carries the non-property side effect (keeping the position picker's selection in sync).
    partial void OnOrientationChanged(NavOrientation value) => SyncPositions();

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
