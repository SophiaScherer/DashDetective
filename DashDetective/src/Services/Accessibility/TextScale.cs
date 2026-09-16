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

    /// <summary>The ladder at a given scale, ready to install as resources.</summary>
    internal static IReadOnlyDictionary<string, double> Sizes(int percent) {
        var factor = ScaleRange.Factor(percent);
        var sizes = new Dictionary<string, double>(BaseSizes.Count);
        foreach (var (key, size) in BaseSizes)
            sizes[key] = size * factor;

        return sizes;
    }
}
