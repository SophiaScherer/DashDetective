using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashDetective.Services.Diagnostics;
using DashDetective.Services.Identity;
using DashDetective.Services.Threading;
using DashDetective.Shared;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;

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

    /// <summary>The user's collapse preference. Persisted. Layout reads <see cref="IsRailCollapsed"/>
    /// instead, so a narrow window can force the rail in without overwriting this.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRailCollapsed), nameof(RailWidth), nameof(RailHeight), nameof(ShowLabels),
        nameof(ShowBrandText), nameof(ShowFullFooter),
        nameof(ChevronPointing), nameof(ChevronIcon),
        nameof(ControlsDock), nameof(FooterAvatarDock))]
    private bool _isCollapsed;

    /// <summary>Collapsed because the window is too narrow to spare 236px for an expanded rail. Not
    /// persisted, and deliberately separate from <see cref="IsCollapsed"/> so widening the window
    /// restores whatever the user actually chose.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRailCollapsed), nameof(RailWidth), nameof(RailHeight), nameof(ShowLabels),
        nameof(ShowBrandText), nameof(ShowFullFooter),
        nameof(ChevronPointing), nameof(ChevronIcon),
        nameof(ControlsDock), nameof(FooterAvatarDock))]
    private bool _isAutoCollapsed;

    /// <summary>Whether the rail actually renders collapsed — the user's choice or the window forcing
    /// it. Everything that lays the bar out reads this rather than either flag alone.</summary>
    public bool IsRailCollapsed => IsCollapsed || IsAutoCollapsed;

    /// <summary>Shell width below which an expanded rail leaves too little for the page.</summary>
    internal const double AutoCollapseWidth = 820;

    // Tracks the last side of the threshold so auto-collapse fires on a crossing rather than on every
    // resize — that way an explicit toggle sticks until the window crosses back.
    private bool _belowAutoCollapseWidth;

    // The text scale the bar is sized against. 1 until the accessibility state is applied.
    private double _textScale = 1;

    /// <summary>Reports the shell's width so the rail can fold itself away on a narrow window.</summary>
    public void SetShellWidth(double width) {
        if (!double.IsFinite(width) || width <= 0)
            return;

        var below = width < AutoCollapseWidth;
        if (below == _belowAutoCollapseWidth)
            return;

        _belowAutoCollapseWidth = below;
        IsAutoCollapsed = below;
    }

    /// <summary>Which window edge the bar docks to. Persisted.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsHorizontal), nameof(Dock), nameof(BrandDock), nameof(FooterDock),
        nameof(ItemsOrientation), nameof(ItemsVAlign), nameof(RailWidth), nameof(RailHeight),
        nameof(HairlineThickness), nameof(ScrollV), nameof(ScrollH), nameof(ShowBrandText),
        nameof(ShowFullFooter), nameof(ChevronIcon),
        nameof(ControlsDock), nameof(FooterAvatarDock),
        nameof(ChevronPointing), nameof(ChevronWidth), nameof(ChevronHeight),
        nameof(ChevronHAlign), nameof(ChevronVAlign),
        nameof(ChevronCornerRadius))]
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
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowChevron))]
    private bool _isDragging;

    public NavigationViewModel()
        : this(new DispatcherTimerAdapter(), new DispatcherTimerAdapter(),
               IUserPictureProvider.ForCurrentPlatform()) { }

    /// <summary>Test seam: takes the puck's hide timer, the re-dock fade timer and the account-picture
    /// reader explicitly. A real <c>DispatcherTimer</c> only fires while an Avalonia dispatcher is
    /// pumping, so headless tests inject fakes and tick them by hand.</summary>
    internal NavigationViewModel(IUiTimer chevronHide, IUiTimer relocate, IUserPictureProvider picture) {
        Positions = new ObservableCollection<NavPositionOption> {
            new("Left", NavOrientation.Left, SelectPosition),
            new("Top", NavOrientation.Top, SelectPosition),
            new("Right", NavOrientation.Right, SelectPosition),
            new("Bottom", NavOrientation.Bottom, SelectPosition),
        };
        SyncPositions();

        _chevronHide = chevronHide;
        _chevronHide.Interval = ChevronHideDelay;
        _chevronHide.Tick += OnChevronHideElapsed;

        _relocate = relocate;
        _relocate.Interval = RelocateFade;
        _relocate.Tick += OnRelocateElapsed;

        var user = CurrentUserProvider.Load();
        UserName = user.DisplayName;
        UserInitials = user.Initials;
        UserRole = user.Role;
        UserPicture = Decode(picture.Read());
    }

    /// <summary>Decodes the account picture's bytes for the footer avatar. Soft-failing like the reader
    /// that produced them: a file that is not really an image, or a host with no imaging backend (the
    /// headless test runs), yields no picture and the initials badge stays.</summary>
    private static Bitmap? Decode(byte[]? bytes) {
        if (bytes is null)
            return null;

        try {
            using var stream = new MemoryStream(bytes);
            return new Bitmap(stream);
        } catch (Exception e) {
            Log.Warn("Could not decode the account picture", e);
            return null;
        }
    }

    // ----- Footer identity (the interactive Windows user; read once at construction) -----

    /// <summary>The logged-in user's login name shown in the footer card.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UserTooltip))]
    private string _userName = "";

    /// <summary>Up to two letters shown in the footer avatar badge, when there is no account picture.</summary>
    [ObservableProperty] private string _userInitials = "";

    /// <summary>The operating system's account picture for this user, or <c>null</c> when there is none
    /// (no picture set, a denied read, or a platform with no such store).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUserPicture))]
    private Bitmap? _userPicture;

    /// <summary>Whether to show the account picture instead of the initials badge.</summary>
    public bool HasUserPicture => UserPicture is not null;

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
        _textScale * (horizontal ? (IsRailCollapsed ? 54 : 64) : (IsRailCollapsed ? 64 : 236));

    /// <summary>Grows the bar with the text scale. The rail is the one surface sized in pixels that has
    /// to hold scaled text — the brand and the item labels — so at 200% a fixed 236px clipped both.
    /// </summary>
    public void SetTextScale(double factor) {
        if (!double.IsFinite(factor) || factor <= 0 || factor == _textScale)
            return;

        _textScale = factor;
        OnPropertyChanged(nameof(RailWidth));
        OnPropertyChanged(nameof(RailHeight));
    }

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
    public bool ShowLabels => !IsRailCollapsed;

    /// <summary>Whether the brand wordmark (beside the logo tile) is shown. Hidden when collapsed or
    /// when horizontal (the short bar shows the logo only).</summary>
    public bool ShowBrandText => !IsRailCollapsed && !IsHorizontal;

    /// <summary>Whether the footer shows the full user card (vs. a compact avatar). Full only on an
    /// expanded vertical bar.</summary>
    public bool ShowFullFooter => !IsRailCollapsed && !IsHorizontal;

    /// <summary>Where the footer's Help button sits relative to the avatar: beneath it on a collapsed
    /// vertical rail (64px is too narrow to fit both side by side), to its right otherwise.</summary>
    public Dock ControlsDock => IsRailCollapsed && !IsHorizontal ? Dock.Bottom : Dock.Right;

    /// <summary>Where the footer's avatar sits: the start of the same axis <see cref="ControlsDock"/>
    /// ends, so the two bracket the user's name when it is shown.</summary>
    public Dock FooterAvatarDock => IsRailCollapsed && !IsHorizontal ? Dock.Top : Dock.Left;

    // ----- Collapse/expand puck (the hover-revealed semi-circle domed into the bar's content edge) -----

    /// <summary>The semi-circle's radius: how far the puck reaches into the bar from its edge, and half
    /// the length of the flat side lying on it. Keeping it exactly half of <see cref="PuckLength"/> is
    /// what makes the corner radius describe a true half-disc rather than a rounded tab.</summary>
    private const double PuckRadius = 20;

    /// <summary>The flat side lying along the bar's edge — the semi-circle's diameter.</summary>
    private const double PuckLength = PuckRadius * 2;

    /// <summary>How long the puck lingers after the pointer leaves the bar, so a moment's wobble on the
    /// way to it does not snatch it away mid-reach.</summary>
    private static readonly TimeSpan ChevronHideDelay = TimeSpan.FromMilliseconds(600);

    private readonly IUiTimer _chevronHide;

    /// <summary>Whether the pointer currently counts as over the bar. Hover sets it at once; leaving clears
    /// it only after <see cref="ChevronHideDelay"/>. UI-only, never persisted.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowChevron))]
    private bool _isChevronVisible;

    /// <summary>Whether the puck draws. A drag masks it without disturbing the hover state, so a gesture that
    /// ends with the pointer still on the bar brings it straight back. The view binds this, not the flag:
    /// a style setter cannot override a local binding, so the drag rule has to live here.</summary>
    public bool ShowChevron => IsChevronVisible && !IsDragging;

    /// <summary>The pointer entered the bar: show the puck and cancel any pending hide.</summary>
    internal void PointerEnteredBar() {
        _chevronHide.Stop();
        IsChevronVisible = true;
    }

    /// <summary>The pointer left the bar: hide the puck once the grace period elapses. Restarting the timer
    /// from scratch is what lets a re-entry cancel the pending hide.</summary>
    internal void PointerExitedBar() {
        _chevronHide.Stop();
        _chevronHide.Start();
    }

    private void OnChevronHideElapsed(object? sender, EventArgs e) {
        _chevronHide.Stop();
        IsChevronVisible = false;
    }

    /// <summary>Which way the puck's chevron points: toward the docked edge when the bar is expanded (it
    /// will collapse), away from it when collapsed (it will expand).</summary>
    public ChevronDirection ChevronPointing => Orientation switch {
        NavOrientation.Left => IsRailCollapsed ? ChevronDirection.Right : ChevronDirection.Left,
        NavOrientation.Right => IsRailCollapsed ? ChevronDirection.Left : ChevronDirection.Right,
        NavOrientation.Top => IsRailCollapsed ? ChevronDirection.Down : ChevronDirection.Up,
        _ => IsRailCollapsed ? ChevronDirection.Up : ChevronDirection.Down,
    };

    /// <summary>The caret glyph shown on the puck. Filled, so the view sets Fill rather than Stroke.</summary>
    public Geometry ChevronIcon => Icons.Caret(ChevronPointing);

    /// <summary>Puck width: the stand-off axis on a vertical rail, the flat side on a horizontal bar.</summary>
    public double ChevronWidth => IsHorizontal ? PuckLength : PuckRadius;

    /// <summary>Puck height: the flat side on a vertical rail, the stand-off axis on a horizontal bar.</summary>
    public double ChevronHeight => IsHorizontal ? PuckRadius : PuckLength;

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

    /// <summary>Rounds the two corners facing into the bar by the full radius. On a box one radius deep
    /// and two long that is exactly a half-disc — domed inward, flat side flush on the content edge.</summary>
    public CornerRadius ChevronCornerRadius => Orientation switch {
        NavOrientation.Left => new CornerRadius(PuckRadius, 0, 0, PuckRadius),
        NavOrientation.Right => new CornerRadius(0, PuckRadius, PuckRadius, 0),
        NavOrientation.Top => new CornerRadius(PuckRadius, PuckRadius, 0, 0),
        _ => new CornerRadius(0, 0, PuckRadius, PuckRadius),
    };

    /// <summary>Toggles the collapsed (icons-only) state of the bar.</summary>
    [RelayCommand]
    private void ToggleCollapse() {
        // An explicit toggle overrides a width-driven collapse until the window next crosses the
        // threshold, so the control never looks like it did nothing.
        var collapsed = IsRailCollapsed;
        IsAutoCollapsed = false;
        IsCollapsed = !collapsed;
    }

    /// <summary>Expands the bar (used by the Settings control).</summary>
    [RelayCommand]
    private void Expand() {
        IsAutoCollapsed = false;
        IsCollapsed = false;
    }

    /// <summary>Collapses the bar to icons-only (used by the Settings control).</summary>
    [RelayCommand]
    private void Collapse() => IsCollapsed = true;

    /// <summary>Docks the bar to the given window edge.</summary>
    [RelayCommand]
    private void SetOrientation(NavOrientation orientation) => BeginRelocate(orientation);

    // ----- Re-dock fade -----

    /// <summary>How long the bar fades out before it changes edge, and back in after. A DockPanel offers no
    /// path between edges, so a move can only be tweened as a fade, not a slide.</summary>
    private static readonly TimeSpan RelocateFade = TimeSpan.FromMilliseconds(120);

    /// <summary>One beat between the edge changing and the fade back in, so the relayout lands while the
    /// size transitions are still suspended. Without it the .relocating class would drop in the same pass
    /// that changes the edge, and nothing orders the two — reinstate the size transition first and Width
    /// tweens from NaN.</summary>
    private static readonly TimeSpan RelocateSettle = TimeSpan.FromMilliseconds(30);

    private readonly IUiTimer _relocate;
    private NavOrientation _pendingEdge = NavOrientation.Left;
    private bool _edgeApplied;

    /// <summary>Whether the bar is mid-move between edges, which fades it out and suspends its size
    /// transitions. UI-only, never persisted.</summary>
    [ObservableProperty] private bool _isRelocating;

    /// <summary>Starts a docked-edge change. The edge itself is applied when the fade-out finishes, so the
    /// relayout happens while the bar is invisible and only its arrival is seen. Every re-dock path — the
    /// command, the picker and the drag — goes through here, so none of them can skip the fade.</summary>
    private void BeginRelocate(NavOrientation edge) {
        // While a move is already in flight it is the pending target, not the current edge, that a new
        // pick replaces — otherwise re-picking the edge being left would be read as a no-op.
        if (edge == (IsRelocating ? _pendingEdge : Orientation))
            return;

        _pendingEdge = edge;
        _edgeApplied = false;
        IsRelocating = true;
        _relocate.Stop();
        _relocate.Interval = RelocateFade;
        _relocate.Start();
    }

    // Two beats, not one: move while still faded out, then let the layout settle before fading back in.
    private void OnRelocateElapsed(object? sender, EventArgs e) {
        _relocate.Stop();

        if (!_edgeApplied) {
            Orientation = _pendingEdge;
            _edgeApplied = true;
            _relocate.Interval = RelocateSettle;
            _relocate.Start();
            return;
        }

        IsRelocating = false;
    }

    /// <summary>Asks the shell to open the Help modal.</summary>
    [RelayCommand]
    private void ShowHelp() => HelpRequested?.Invoke();

    private void SelectPosition(NavPositionOption option) {
        BeginRelocate(option.Value);
        PositionPicked?.Invoke();
    }

    /// <summary>Docks the bar to an edge chosen by a drag gesture. Same effect as the picker, so the
    /// Settings control and on-bar flyout stay in sync via <see cref="OnOrientationChanged"/>.</summary>
    public void DockTo(NavOrientation orientation) => BeginRelocate(orientation);

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
