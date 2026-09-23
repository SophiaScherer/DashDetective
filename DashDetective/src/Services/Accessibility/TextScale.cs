using System.Collections.Generic;

namespace DashDetective.Services.Accessibility;

/// <summary>
/// The type sizes the text scale scales. Pure, so the table is testable without a layout pass; the
/// range and its arithmetic are <see cref="ScaleRange"/>'s.
///
/// <see cref="BaseSizes"/> mirrors the <c>TextSize*</c> defaults in Dimensions.axaml, as
/// <c>SemanticBrushes</c> mirrors Palette.axaml — a test pins the two together.
/// </summary>
internal static class TextScale {
    /// <summary>Every authored type size in the app, keyed by its resource name. The ladder was rebalanced
    /// once, measured against File Explorer: the old steps read a size small beside it, so each grew by
    /// ~1.12 rounded to 0.5. Steps stay strictly increasing — a test pins that — since rounding two
    /// neighbours onto one value would flatten the hierarchy the ladder exists to carry.</summary>
    internal static readonly IReadOnlyDictionary<string, double> BaseSizes = new Dictionary<string, double> {
        ["TextSizeMicro"] = 11,
        ["TextSizeMini"] = 11.5,
        ["TextSizeCaption"] = 12,
        ["TextSizeSmall"] = 12.5,
        ["TextSizeCompact"] = 13,
        ["TextSizeBody"] = 14,
        ["TextSizeMedium"] = 14.5,
        ["TextSizeSubhead"] = 15,
        ["TextSizeTitle"] = 15.5,
        ["TextSizeTitleLarge"] = 16.5,
        ["TextSizeHeading"] = 17.5,
        ["TextSizeHeadingLarge"] = 18,
        ["TextSizeDisplay"] = 20,
        ["TextSizeDisplayLarge"] = 24,
        ["TextSizeHero"] = 28.5,
    };

    /// <summary>The ladder at a given scale, ready to install as resources.</summary>
    internal static IReadOnlyDictionary<string, double> Sizes(int percent) {
        var factor = ScaleRange.Factor(percent);
        var sizes = new Dictionary<string, double>(BaseSizes.Count);
        foreach (var (key, size) in BaseSizes)
            sizes[key] = size * factor;

        return sizes;
    }
}
