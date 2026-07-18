using Avalonia.Media;
using System.Collections.Generic;

namespace DashDetective.Services.Theming;

/// <summary>
/// One selectable accent colour and the shades it drives across the app:
/// <list type="bullet">
///   <item><see cref="Color"/> — the accent itself (charts, highlights, selected states).</item>
///   <item><see cref="Hover"/> — a lighter tint for pointer-over on accent-filled controls.</item>
///   <item><see cref="OnAccent"/> — the contrast colour for text/icons drawn on the accent fill.</item>
///   <item><see cref="Deep"/> — a darker shade for the bottom stop of the brand-logo gradient.</item>
/// </list>
/// Immutable; the fixed set lives in <see cref="All"/>. Applied by <see cref="ThemeService"/>.
/// </summary>
public sealed record AccentPreset(string Name, Color Color, Color Hover, Color OnAccent, Color Deep) {

    /// <summary>The four accents from the design comp; blue (index 0) is the default.</summary>
    public static readonly IReadOnlyList<AccentPreset> All = new[] {
        new AccentPreset("Blue",   Avalonia.Media.Color.Parse("#4cc2ff"), Avalonia.Media.Color.Parse("#66d0ff"), Avalonia.Media.Color.Parse("#06263a"), Avalonia.Media.Color.Parse("#2a7fd4")),
        new AccentPreset("Green",  Avalonia.Media.Color.Parse("#6ccb5f"), Avalonia.Media.Color.Parse("#84d67a"), Avalonia.Media.Color.Parse("#0c2a10"), Avalonia.Media.Color.Parse("#3c9e4f")),
        new AccentPreset("Purple", Avalonia.Media.Color.Parse("#c58fff"), Avalonia.Media.Color.Parse("#d3a8ff"), Avalonia.Media.Color.Parse("#23103a"), Avalonia.Media.Color.Parse("#7d54c9")),
        new AccentPreset("Orange", Avalonia.Media.Color.Parse("#ff8a5c"), Avalonia.Media.Color.Parse("#ff9f78"), Avalonia.Media.Color.Parse("#3a1606"), Avalonia.Media.Color.Parse("#d45f34")),
    };

    /// <summary>The default accent (blue), matching the comp.</summary>
    public static AccentPreset Default => All[0];
}
