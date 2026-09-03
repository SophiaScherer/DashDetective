using Avalonia.Media;

namespace DashDetective.Shell.Navigation;

/// <summary>
/// Shared navigation icon geometries. Paths are authored in an 18x18 coordinate
/// space (matching the design document) and are drawn as stroked outlines — except the
/// <c>Caret*</c> disclosure set, which is filled.
/// </summary>
public static class Icons {
    // Four rounded tiles forming a dashboard grid.
    public static readonly Geometry Dashboard = Geometry.Parse(
        "M2,2 H8 V8 H2 Z M10,2 H16 V6 H10 Z M10,8 H16 V16 H10 Z M2,10 H8 V16 H2 Z");

    // A gear-like glyph: a centre circle with radiating spokes.
    public static readonly Geometry Settings = Geometry.Parse(
        "M9,6.5 A2.5,2.5 0 1 1 8.99,6.5 " +
        "M9,1.5 V3.5 M9,14.5 V16.5 M1.5,9 H3.5 M14.5,9 H16.5 " +
        "M3.7,3.7 L5.1,5.1 M12.9,12.9 L14.3,14.3 M14.3,3.7 L12.9,5.1 M5.1,12.9 L3.7,14.3");

    // A folder with a raised left tab.
    public static readonly Geometry FileExplorer = Geometry.Parse(
        "M2.5,6 L6.5,6 L8,7.5 L15.5,7.5 L15.5,14 L2.5,14 Z");

    // A document sheet with a folded top-right corner. Distinguishes a file from a folder in the
    // universal-search results, where both appear side by side.
    public static readonly Geometry Document = Geometry.Parse(
        "M4.5,2.5 H10.5 L14,6 V15.5 H4.5 Z M10.5,2.5 V6 H14");

    // A CPU/chip glyph: a rounded square die with eight pins radiating out (the Processes tab),
    // matching the design document's processes icon.
    public static readonly Geometry Processes = Geometry.Parse(
        "M6.3,5 H11.7 A1.3,1.3 0 0 1 13,6.3 V11.7 A1.3,1.3 0 0 1 11.7,13 " +
        "H6.3 A1.3,1.3 0 0 1 5,11.7 V6.3 A1.3,1.3 0 0 1 6.3,5 Z " +
        "M7,2 V4 M11,2 V4 M7,14 V16 M11,14 V16 M2,7 H4 M2,11 H4 M14,7 H16 M14,11 H16");

    // A hardware/chip glyph: a rounded die with two pins on each edge, matching the design
    // document's hardware icon. Distinct from the Processes die (whose pins sit at different
    // offsets) so the two chip-like tabs stay visually separable.
    public static readonly Geometry Hardware = Geometry.Parse(
        "M6,5 H12 A1,1 0 0 1 13,6 V12 A1,1 0 0 1 12,13 H6 A1,1 0 0 1 5,12 V6 A1,1 0 0 1 6,5 Z " +
        "M7.5,5 V2.5 M10.5,5 V2.5 M7.5,15.5 V13 M10.5,15.5 V13 " +
        "M2.5,7.5 H5 M2.5,10.5 H5 M13,7.5 H15.5 M13,10.5 H15.5");

    // A rising line-graph: an L-shaped axis (left + bottom) with a climbing polyline over it,
    // matching the design document's Performance icon.
    public static readonly Geometry Performance = Geometry.Parse(
        "M2.5,3 V15.5 H15.5 " +
        "M4.5,12.5 L7.5,9 L10,11 L15,5.5");

    // A globe: an outer circle crossed by the equator and two curved meridians.
    public static readonly Geometry Network = Geometry.Parse(
        "M9,2 A7,7 0 1 1 8.99,2 Z " +
        "M2,9 H16 " +
        "M9,2 C6.5,4 5.5,6.4 5.5,9 C5.5,11.6 6.5,14 9,16 " +
        "M9,2 C11.5,4 12.5,6.4 12.5,9 C12.5,11.6 11.5,14 9,16");

    // Two stacked disk-drive platters, each with a small activity dot (a round-cap degenerate segment),
    // forming a stacked-disk glyph that matches the design document's storage icon.
    public static readonly Geometry Storage = Geometry.Parse(
        "M5,4 H13 A1,1 0 0 1 14,5 V7 A1,1 0 0 1 13,8 H5 A1,1 0 0 1 4,7 V5 A1,1 0 0 1 5,4 Z " +
        "M5,10 H13 A1,1 0 0 1 14,11 V13 A1,1 0 0 1 13,14 H5 A1,1 0 0 1 4,13 V11 A1,1 0 0 1 5,10 Z " +
        "M11.8,6 h0.01 M11.8,12 h0.01");

    // A terminal window (the Toolkit tab): a rounded console frame with a ">" prompt chevron and the
    // command line beside it, matching the design document's commands icon.
    public static readonly Geometry Toolkit = Geometry.Parse(
        "M3.5,3.5 H14.5 A1,1 0 0 1 15.5,4.5 V13.5 A1,1 0 0 1 14.5,14.5 " +
        "H3.5 A1,1 0 0 1 2.5,13.5 V4.5 A1,1 0 0 1 3.5,3.5 Z " +
        "M5.5,7.5 L7.5,9.5 L5.5,11.5 M9.5,11.5 H12.5");

    // A circled question mark (the Help affordance): the outer ring, the hook, and the dot beneath it.
    // The dot is a round-cap degenerate segment (as in Storage's activity dots) so it draws from the
    // same stroke as the rest of the glyph.
    /// <summary>Magnifier. Drawn by the SearchField and by the toolbar's collapsed search button.</summary>
    public static readonly Geometry Search = Geometry.Parse(
        "M7,7 m-4.5,0 a4.5,4.5 0 1 0 9,0 a4.5,4.5 0 1 0 -9,0 M10.5,10.5 L14,14");

    public static readonly Geometry Help = Geometry.Parse(
        "M9,2 A7,7 0 1 1 8.99,2 Z " +
        "M6.8,7 A2.2,2.2 0 1 1 9.9,9 C9.2,9.5 8.9,9.8 8.9,10.6 " +
        "M9,13 h0.01");

    // Panel/sidebar-split glyph (stroked) used for the collapse/expand affordance, matching the design
    // document: a rounded panel outline with a thin divider carving off a narrow rail. The divider sits
    // on the side the bar will move toward, so the glyph reads directionally per dock edge and state.
    private const string PanelFrame =
        "M4,3.5 H14 A1.5,1.5 0 0 1 15.5,5 V13 A1.5,1.5 0 0 1 14,14.5 " +
        "H4 A1.5,1.5 0 0 1 2.5,13 V5 A1.5,1.5 0 0 1 4,3.5 Z ";
    public static readonly Geometry PanelRailLeft = Geometry.Parse(PanelFrame + "M6.5,3.5 V14.5");
    public static readonly Geometry PanelRailRight = Geometry.Parse(PanelFrame + "M11.5,3.5 V14.5");
    public static readonly Geometry PanelRailTop = Geometry.Parse(PanelFrame + "M2.5,6.5 H15.5");
    public static readonly Geometry PanelRailBottom = Geometry.Parse(PanelFrame + "M2.5,11.5 H15.5");

    // Plain chevrons (stroked) for the Network connections pager's prev/next. Authored in the same 18x18
    // space as the other glyphs, apexed on the centre so they read as centred at any size.
    public static readonly Geometry ChevronLeft = Geometry.Parse("M11,4 L6,9 L11,14");
    public static readonly Geometry ChevronRight = Geometry.Parse("M7,4 L12,9 L7,14");

    // Skip-to-end chevrons for the same pager: the same stroke against a stop bar, so the four arrows
    // read as one set with ChevronLeft/ChevronRight above.
    public static readonly Geometry ChevronFirst = Geometry.Parse("M12,4 L7,9 L12,14 M5,4 V14");
    public static readonly Geometry ChevronLast = Geometry.Parse("M6,4 L11,9 L6,14 M13,4 V14");

    // Filled disclosure carets — the app's one expand/collapse glyph, matching the ▾/▸ the widget
    // headers and the Processes table draw as text. FILLED, not stroked like everything above: a
    // consumer must set Fill rather than Stroke. Authored as an 8x5 triangle centred in the 18x18 space,
    // which is the size the 12.5px text glyph renders at.
    public static readonly Geometry CaretLeft = Geometry.Parse("M11.5,5 L11.5,13 L6.5,9 Z");
    public static readonly Geometry CaretRight = Geometry.Parse("M6.5,5 L6.5,13 L11.5,9 Z");
    public static readonly Geometry CaretUp = Geometry.Parse("M5,11.5 L13,11.5 L9,6.5 Z");
    public static readonly Geometry CaretDown = Geometry.Parse("M5,6.5 L13,6.5 L9,11.5 Z");

    /// <summary>
    /// The disclosure caret for a direction, used by the nav bar's edge puck. A plain map — the rule
    /// deciding which way the puck points lives on <c>NavigationViewModel.ChevronPointing</c>, where it
    /// is testable without a render backend.
    /// </summary>
    public static Geometry Caret(ChevronDirection direction) => direction switch {
        ChevronDirection.Left => CaretLeft,
        ChevronDirection.Right => CaretRight,
        ChevronDirection.Up => CaretUp,
        _ => CaretDown,
    };

    /// <summary>
    /// The panel-split glyph for the drag-to-dock chip. The rail sits on the docked-edge side when the bar
    /// is expanded (the direction it will collapse) and flips to the opposite side when collapsed (the
    /// direction it will expand), keeping the affordance directional.
    /// </summary>
    public static Geometry PanelGlyph(NavOrientation orientation, bool collapsed) => orientation switch {
        NavOrientation.Left => collapsed ? PanelRailRight : PanelRailLeft,
        NavOrientation.Right => collapsed ? PanelRailLeft : PanelRailRight,
        NavOrientation.Top => collapsed ? PanelRailBottom : PanelRailTop,
        _ => collapsed ? PanelRailTop : PanelRailBottom,
    };
}
