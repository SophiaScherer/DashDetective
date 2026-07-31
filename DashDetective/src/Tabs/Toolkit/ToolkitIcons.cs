using Avalonia.Media;

namespace DashDetective.Tabs.Toolkit;

/// <summary>
/// Feature-local glyphs and fixed badge colours for the Toolkit rows, one set per
/// <see cref="ToolkitEntryKind"/>. Same conventions as <c>HardwareIcons</c>: 18×18 stroked
/// geometries, and **fixed** legend-style tints (not the theme-swapped accent) so a kind reads the
/// same in light and dark — a foreground colour plus a 14%-opacity tile/pill background.
///
/// Only <see cref="ToolkitEntry"/>'s presentation getters and the view reach this, and they do so
/// lazily: <c>Geometry.Parse</c> needs a render backend, so nothing the tests touch may load it.
/// </summary>
public static class ToolkitIcons {
    // ----- Glyphs (18×18, stroked) -----

    /// <summary>A folder with a raised left tab.</summary>
    public static readonly Geometry Folder = Geometry.Parse(
        "M2.5,5 H7 L8.5,6.5 H15.5 V13.5 H2.5 Z");

    /// <summary>An application window: a frame with a title bar.</summary>
    public static readonly Geometry App = Geometry.Parse(
        "M3,4 H15 A0.5,0.5 0 0 1 15.5,4.5 V13.5 A0.5,0.5 0 0 1 15,14 " +
        "H3 A0.5,0.5 0 0 1 2.5,13.5 V4.5 A0.5,0.5 0 0 1 3,4 Z M2.5,7 H15.5");

    /// <summary>A console prompt: a ">" chevron and the command line beside it.</summary>
    public static readonly Geometry Command = Geometry.Parse(
        "M3.5,5 L7.5,9 L3.5,13 M9,13 H14.5");

    /// <summary>A gear: a centre circle with radiating spokes.</summary>
    public static readonly Geometry Panel = Geometry.Parse(
        "M9,6.5 A2.5,2.5 0 1 1 8.99,6.5 " +
        "M9,1.5 V3.5 M9,14.5 V16.5 M1.5,9 H3.5 M14.5,9 H16.5 " +
        "M3.7,3.7 L5.1,5.1 M12.9,12.9 L14.3,14.3 M14.3,3.7 L12.9,5.1 M5.1,12.9 L3.7,14.3");

    // ----- Fixed per-kind colours (foreground + 14%-tint background, as #AARRGGBB) -----

    private static readonly IBrush Blue = Brush.Parse("#4cc2ff");
    private static readonly IBrush BlueBg = Brush.Parse("#244cc2ff");
    private static readonly IBrush Purple = Brush.Parse("#c58fff");
    private static readonly IBrush PurpleBg = Brush.Parse("#24c58fff");
    private static readonly IBrush Green = Brush.Parse("#6ccb5f");
    private static readonly IBrush GreenBg = Brush.Parse("#246ccb5f");
    private static readonly IBrush Yellow = Brush.Parse("#ffcf4d");
    private static readonly IBrush YellowBg = Brush.Parse("#24ffcf4d");

    /// <summary>The row glyph for a kind.</summary>
    public static Geometry GlyphFor(ToolkitEntryKind kind) => kind switch {
        ToolkitEntryKind.Folder => Folder,
        ToolkitEntryKind.App => App,
        ToolkitEntryKind.Panel => Panel,
        _ => Command,
    };

    /// <summary>The badge/glyph colour for a kind.</summary>
    public static IBrush ForegroundFor(ToolkitEntryKind kind) => kind switch {
        ToolkitEntryKind.Folder => Blue,
        ToolkitEntryKind.App => Purple,
        ToolkitEntryKind.Panel => Yellow,
        _ => Green,
    };

    /// <summary>The tinted badge/tile fill for a kind.</summary>
    public static IBrush BackgroundFor(ToolkitEntryKind kind) => kind switch {
        ToolkitEntryKind.Folder => BlueBg,
        ToolkitEntryKind.App => PurpleBg,
        ToolkitEntryKind.Panel => YellowBg,
        _ => GreenBg,
    };
}
