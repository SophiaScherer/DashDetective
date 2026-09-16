namespace DashDetective.Shared.Controls;

/// <summary>
/// The width a <see cref="SearchField"/> asks for, kept out of the control so the rule is testable
/// without a layout pass — the same reason <c>ChartAxis</c> and <c>PagerMath</c> sit apart from their
/// callers.
/// </summary>
internal static class FieldWidth {
    /// <summary>The room offered, or the measured content when a container offers infinity — the only
    /// answer available there. See <see cref="SearchField"/> for why the content is otherwise ignored.</summary>
    internal static double Desired(double available, double content) =>
        double.IsInfinity(available) || double.IsNaN(available) ? content : available;
}
