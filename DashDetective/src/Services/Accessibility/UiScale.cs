using System;
using System.Collections.Generic;

namespace DashDetective.Services.Accessibility;

/// <summary>
/// The UI scale ladder, and how a stored percentage becomes the factor <c>ScaleHost</c> transforms by.
/// Pure, so both are testable without a layout pass.
/// </summary>
internal static class UiScale {
    /// <summary>The offered steps. 100 is the size the app ships at; 200 is where a maximised 1080p
    /// window still holds a readable page.</summary>
    internal static readonly IReadOnlyList<int> Percents = [100, 125, 150, 175, 200];

    internal const int DefaultPercent = 100;

    /// <summary>The unscaled context-menu and tooltip type size. Mirrors the <c>PopupFontSize</c> default
    /// in Dimensions.axaml, as <c>SemanticBrushes</c> mirrors Palette.axaml.</summary>
    internal const double BasePopupFontSize = 12.5;

    /// <summary>A percentage as a transform factor. Clamped because a hand-edited settings file can say
    /// anything, and 0 would collapse the window to nothing.</summary>
    internal static double Factor(int percent) =>
        Math.Clamp(percent, Percents[0], Percents[^1]) / 100.0;

    /// <summary>The popup type size at a given scale. Fluent templates the tooltip and context-menu
    /// presenters, so neither can host a <c>ScaleHost</c> and both follow the scale by type size.</summary>
    internal static double PopupFontSize(int percent) => BasePopupFontSize * Factor(percent);

    /// <summary>The nearest offered step to a stored value, so an unrecognized percentage still selects
    /// a segment rather than leaving the control blank.</summary>
    internal static int Nearest(int percent) {
        var best = Percents[0];
        foreach (var step in Percents)
            if (Math.Abs(step - percent) < Math.Abs(best - percent))
                best = step;
        return best;
    }
}
