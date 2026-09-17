namespace DashDetective.Services.Accessibility;

/// <summary>
/// What the interface size means beyond its range: the popup type size that has to follow the scale
/// where a <c>ScaleHost</c> cannot reach. The range and its arithmetic are <see cref="ScaleRange"/>'s.
/// </summary>
internal static class UiScale {
    /// <summary>The unscaled context-menu and tooltip type size. Mirrors the <c>PopupFontSize</c> default
    /// in Dimensions.axaml, as <c>SemanticBrushes</c> mirrors Palette.axaml.</summary>
    internal const double BasePopupFontSize = 14;

    /// <summary>The popup type size at a given scale. Fluent templates the tooltip and context-menu
    /// presenters, so neither can host a <c>ScaleHost</c> and both follow the scale by type size.</summary>
    internal static double PopupFontSize(int percent) => BasePopupFontSize * ScaleRange.Factor(percent);
}
