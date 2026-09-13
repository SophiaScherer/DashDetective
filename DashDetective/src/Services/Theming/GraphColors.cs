using Avalonia.Media;
using System.Collections.Generic;

namespace DashDetective.Services.Theming;

/// <summary>
/// One single-hue Graph colors choice: a name and the hue the chart palette is derived from. Charts only;
/// the accent never reads this. <c>null</c> stands for the authored Default palette.
/// </summary>
public sealed record GraphColors(string Name, Color Hue) {
    /// <summary>The series palette this choice yields, re-hued rather than flattened.</summary>
    public ChartSeriesColors Series => ChartPalette.Derive(Hue);

    /// <summary>The four single-hue choices. Blue derives <see cref="ChartPalette.Default"/> exactly.</summary>
    public static readonly IReadOnlyList<GraphColors> All = [
        new("Blue", Color.Parse("#4cc2ff")),
        new("Green", Color.Parse("#6dcc61")),
        new("Purple", Color.Parse("#d0a4ff")),
        new("Orange", Color.Parse("#ff9f79")),
    ];

    /// <summary>Resolves a persisted name, or <c>null</c> (the Default palette) for an empty or unknown one.</summary>
    public static GraphColors? Find(string? name) {
        if (string.IsNullOrEmpty(name))
            return null;
        foreach (var colors in All)
            if (colors.Name == name)
                return colors;
        return null;
    }
}
