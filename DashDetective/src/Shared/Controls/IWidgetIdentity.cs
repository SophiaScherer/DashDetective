namespace DashDetective.Shared.Controls;

/// <summary>A board child that carries a widget identity. Implemented by <see cref="WidgetPanel"/> and
/// by any control that wraps one, so the board can name it without reaching into its visual tree.</summary>
public interface IWidgetIdentity {
    string? WidgetId { get; }
}
