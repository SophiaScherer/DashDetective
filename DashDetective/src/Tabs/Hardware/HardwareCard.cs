using System.Collections.Generic;
using Avalonia.Media;

namespace DashDetective.Tabs.Hardware;

/// <summary>
/// One component card in the Hardware grid: a tinted icon tile + title/subtitle header over a list
/// of <see cref="HardwareSpec"/> rows (matching the design comp's card). A pure data model —
/// populated statically for now, ready to be filled from a provider when live data lands.
/// </summary>
public sealed record HardwareCard(
    string Title,
    string Subtitle,
    Geometry Icon,
    IBrush IconColor,
    IBrush IconBackground,
    IReadOnlyList<HardwareSpec> Rows);
