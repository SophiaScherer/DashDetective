using Avalonia.Media;

namespace DashDetective.Tabs.Hardware;

/// <summary>
/// Feature-local glyph geometries and fixed accent colours for the Hardware cards, ported from the
/// design comp's card icons. Kept tab-local (rather than the shell nav <c>Icons</c>) so the tab
/// stays self-contained. Geometries are authored in an 18×18 space and drawn as stroked outlines,
/// matching the shell icon convention. The colours are **fixed** legend-style tints (not the
/// theme-swapped accent), so they read the same in light and dark — each card carries an icon
/// foreground colour and a 14%-opacity tinted tile background (the comp's <c>rgba(…,0.14)</c>).
/// </summary>
public static class HardwareIcons {
    // ----- Glyphs (18×18, stroked) -----

    /// <summary>A chip die with two pins per edge (Processor).</summary>
    public static readonly Geometry Chip = Geometry.Parse(
        "M6,5 H12 A1,1 0 0 1 13,6 V12 A1,1 0 0 1 12,13 H6 A1,1 0 0 1 5,12 V6 A1,1 0 0 1 6,5 Z " +
        "M7.5,5 V2.5 M10.5,5 V2.5 M7.5,15.5 V13 M10.5,15.5 V13 " +
        "M2.5,7.5 H5 M2.5,10.5 H5 M13,7.5 H15.5 M13,10.5 H15.5");

    /// <summary>A rising line chart over a baseline (Graphics, Sensors).</summary>
    public static readonly Geometry Graph = Geometry.Parse(
        "M2,12 L5.5,8 L8.5,10.5 L13,5 L16,8 M2,15.5 H16");

    /// <summary>Four tiles forming a board/grid (Motherboard).</summary>
    public static readonly Geometry Grid = Geometry.Parse(
        "M2,2 H8 V8 H2 Z M10,2 H16 V6 H10 Z M10,8 H16 V16 H10 Z M2,10 H8 V16 H2 Z");

    /// <summary>Two stacked memory/storage bars (Memory, Storage Devices).</summary>
    public static readonly Geometry Bars = Geometry.Parse(
        "M3.7,3.5 H14.3 A1.2,1.2 0 0 1 15.5,4.7 V6.3 A1.2,1.2 0 0 1 14.3,7.5 " +
        "H3.7 A1.2,1.2 0 0 1 2.5,6.3 V4.7 A1.2,1.2 0 0 1 3.7,3.5 Z " +
        "M3.7,10.5 H14.3 A1.2,1.2 0 0 1 15.5,11.7 V13.3 A1.2,1.2 0 0 1 14.3,14.5 " +
        "H3.7 A1.2,1.2 0 0 1 2.5,13.3 V11.7 A1.2,1.2 0 0 1 3.7,10.5 Z");

    // ----- Fixed per-card colours (foreground + 14%-tint tile background, as #AARRGGBB) -----

    public static readonly IBrush Blue = Brush.Parse("#4cc2ff");
    public static readonly IBrush BlueBg = Brush.Parse("#244cc2ff");

    public static readonly IBrush Green = Brush.Parse("#6ccb5f");
    public static readonly IBrush GreenBg = Brush.Parse("#246ccb5f");

    public static readonly IBrush Purple = Brush.Parse("#c58fff");
    public static readonly IBrush PurpleBg = Brush.Parse("#24c58fff");

    public static readonly IBrush Yellow = Brush.Parse("#ffcf4d");
    public static readonly IBrush YellowBg = Brush.Parse("#24ffcf4d");

    public static readonly IBrush Orange = Brush.Parse("#ff8a5c");
    public static readonly IBrush OrangeBg = Brush.Parse("#24ff8a5c");

    public static readonly IBrush Red = Brush.Parse("#ff6b6b");
    public static readonly IBrush RedBg = Brush.Parse("#24ff6b6b");
}
