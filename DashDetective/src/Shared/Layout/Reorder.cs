using Avalonia;
using Avalonia.Controls;
using DashDetective.Shared.Controls;

namespace DashDetective.Shared.Layout;

/// <summary>How a reorderable panel names its children, so a saved order can point at them.</summary>
public sealed class Reorder {
    /// <summary>An id for a child that is neither a widget nor generated from an item — a tile written
    /// out in markup. The other two name themselves.</summary>
    public static readonly AttachedProperty<string?> IdProperty =
        AvaloniaProperty.RegisterAttached<Reorder, Control, string?>("Id");

    public static string? GetId(Control control) => control.GetValue(IdProperty);

    public static void SetId(Control control, string? value) => control.SetValue(IdProperty, value);

    /// <summary>This child's id: the one attached in markup, else its own, else its item view model's —
    /// a strip's children are generated, so there the identity is on the item rather than on the
    /// container holding it. Empty for a child with no id, which is what pins it where its author put
    /// it.</summary>
    public static string IdOf(Control control) =>
        control switch {
            _ when GetId(control) is { Length: > 0 } attached => attached,
            IWidgetIdentity { WidgetId: { Length: > 0 } own } => own,
            { DataContext: IWidgetIdentity { WidgetId: { Length: > 0 } item } } => item,
            _ => "",
        };
}
