using Avalonia.Controls;
using DashDetective.Shared.Controls;

namespace DashDetective.Shared.Layout;

/// <summary>How a reorderable panel names its children, so a saved order can point at them.</summary>
public static class WidgetIds {
    /// <summary>This child's widget id: its own if it carries one, otherwise its item view model's —
    /// a strip's children are generated, so there the identity is on the item rather than on the
    /// container holding it. Empty for a child with no id, which is what pins it where its author put
    /// it.</summary>
    public static string Of(Control control) =>
        control switch {
            IWidgetIdentity { WidgetId: { Length: > 0 } own } => own,
            { DataContext: IWidgetIdentity { WidgetId: { Length: > 0 } item } } => item,
            _ => "",
        };
}
