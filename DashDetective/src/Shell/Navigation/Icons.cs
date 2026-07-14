using Avalonia.Media;

namespace DashDetective.Shell.Navigation;

/// <summary>
/// Shared navigation icon geometries. Paths are authored in an 18x18 coordinate
/// space (matching the design document) and are drawn as stroked outlines.
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

    // A globe: an outer circle crossed by the equator and two curved meridians.
    public static readonly Geometry Network = Geometry.Parse(
        "M9,2 A7,7 0 1 1 8.99,2 Z " +
        "M2,9 H16 " +
        "M9,2 C6.5,4 5.5,6.4 5.5,9 C5.5,11.6 6.5,14 9,16 " +
        "M9,2 C11.5,4 12.5,6.4 12.5,9 C12.5,11.6 11.5,14 9,16");

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

    // Vertical "three dots" kebab menu glyph (filled — render with Fill, not Stroke). Each dot is a full
    // circle whose centre is stated explicitly, so the three sit on x=9 with symmetric vertical spacing
    // about (9,9) — the glyph then centres in its 18x18 box exactly like the stroked icons (Stretch=None).
    public static readonly Geometry Kebab = Geometry.Parse(
        "M7.6,4.5 a1.4,1.4 0 1 0 2.8,0 a1.4,1.4 0 1 0 -2.8,0 Z " +
        "M7.6,9 a1.4,1.4 0 1 0 2.8,0 a1.4,1.4 0 1 0 -2.8,0 Z " +
        "M7.6,13.5 a1.4,1.4 0 1 0 2.8,0 a1.4,1.4 0 1 0 -2.8,0 Z");

    /// <summary>
    /// The panel-split glyph for the collapse toggle. The rail sits on the docked-edge side when the bar
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
