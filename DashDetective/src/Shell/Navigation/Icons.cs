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

    // Single chevrons (stroked) used for the collapse/expand affordance.
    public static readonly Geometry ChevronLeft = Geometry.Parse("M11,4 L6,9 L11,14");
    public static readonly Geometry ChevronRight = Geometry.Parse("M7,4 L12,9 L7,14");
    public static readonly Geometry ChevronUp = Geometry.Parse("M4,11 L9,6 L14,11");
    public static readonly Geometry ChevronDown = Geometry.Parse("M4,7 L9,12 L14,7");

    // Vertical "three dots" kebab menu glyph (filled — render with Fill, not Stroke).
    public static readonly Geometry Kebab = Geometry.Parse(
        "M9,3 a1.4,1.4 0 1 0 0.01,0 Z M9,9 a1.4,1.4 0 1 0 0.01,0 Z M9,15 a1.4,1.4 0 1 0 0.01,0 Z");

    /// <summary>
    /// The chevron for the collapse toggle, pointing toward the docked edge when the bar is expanded
    /// (i.e. the direction it will collapse) and away from it when collapsed (the direction it expands).
    /// </summary>
    public static Geometry Chevron(NavOrientation orientation, bool collapsed) => orientation switch {
        NavOrientation.Left => collapsed ? ChevronRight : ChevronLeft,
        NavOrientation.Right => collapsed ? ChevronLeft : ChevronRight,
        NavOrientation.Top => collapsed ? ChevronDown : ChevronUp,
        _ => collapsed ? ChevronUp : ChevronDown,
    };
}
