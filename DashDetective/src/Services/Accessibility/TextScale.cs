using System;
using System.Collections.Generic;

namespace DashDetective.Services.Accessibility;

/// <summary>
/// The text scale ladder and the type sizes it scales. Pure, so both are testable without a layout pass.
///
/// <see cref="BaseSizes"/> mirrors the <c>TextSize*</c> defaults in Dimensions.axaml, as
/// <c>SemanticBrushes</c> mirrors Palette.axaml — a test pins the two together.
/// </summary>
internal static class TextScale {
    /// <summary>The offered steps, the same ladder the interface size uses.</summary>
    internal static readonly IReadOnlyList<int> Percents = [100, 125, 150, 175, 200];

    internal const int DefaultPercent = 100;

    /// <summary>Every authored type size in the app, keyed by its resource name. These are the sizes the
    /// app already shipped, not a redesign: a ladder that rounded them would change how the app looks at
    /// 100%, which is the one thing every option on this card must not do.</summary>
    internal static readonly IReadOnlyDictionary<string, double> BaseSizes = new Dictionary<string, double> {
        ["TextSizeNano"] = 9,
        ["TextSizeMicro"] = 10,
        ["TextSizeMini"] = 10.5,
        ["TextSizeCaption"] = 11,
        ["TextSizeSmall"] = 11.5,
        ["TextSizeCompact"] = 12,
        ["TextSizeBody"] = 12.5,
        ["TextSizeMedium"] = 13,
        ["TextSizeSubhead"] = 13.5,
        ["TextSizeTitle"] = 14,
        ["TextSizeTitleLarge"] = 15,
        ["TextSizeHeading"] = 16,
        ["TextSizeHeadingLarge"] = 16.5,
        ["TextSizeDisplay"] = 18,
        ["TextSizeDisplayLarge"] = 22,
        ["TextSizeHero"] = 26,
    };

    /// <summary>A percentage as a multiplier. Clamped because a hand-edited settings file can say
    /// anything, and 0 would render no text at all.</summary>
    internal static double Factor(int percent) =>
        Math.Clamp(percent, Percents[0], Percents[^1]) / 100.0;

    /// <summary>The ladder at a given scale, ready to install as resources.</summary>
    internal static IReadOnlyDictionary<string, double> Sizes(int percent) {
        var factor = Factor(percent);
        var sizes = new Dictionary<string, double>(BaseSizes.Count);
        foreach (var (key, size) in BaseSizes)
            sizes[key] = size * factor;

        return sizes;
    }

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
