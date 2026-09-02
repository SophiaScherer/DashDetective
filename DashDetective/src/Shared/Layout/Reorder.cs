using Avalonia;
using Avalonia.Controls;
using DashDetective.Shared.Controls;

namespace DashDetective.Shared.Layout;

/// <summary>What part of an item a drag may start from.</summary>
public enum ReorderGrip {
    /// <summary>Not reorderable. A strip is a layout before it is a control, so this is its default.</summary>
    None,

    /// <summary>The widget's header, and nothing else in it. A board's default: a titled panel is
    /// picked up by its title.</summary>
    Header,

    /// <summary>Anywhere on the item, minus any control inside it that takes clicks of its own. For a
    /// card, which has no header to grab.</summary>
    Item,

    /// <summary>Only from an element marked <see cref="Reorder.IsGripProperty"/>. For an item that is
    /// itself a control, so a press anywhere else belongs to that control.</summary>
    Marked,
}

/// <summary>How a reorderable panel names its children, so a saved order can point at them.</summary>
public sealed class Reorder {
    /// <summary>An id for a child that is neither a widget nor generated from an item — a tile written
    /// out in markup. The other two name themselves.</summary>
    public static readonly AttachedProperty<string?> IdProperty =
        AvaloniaProperty.RegisterAttached<Reorder, Control, string?>("Id");

    /// <summary>Marks the one element inside an item that a drag may be started from. For an item
    /// that is itself a control — the Performance rail's rows are buttons — a grip is the only way to
    /// tell a drag from a click.</summary>
    public static readonly AttachedProperty<bool> IsGripProperty =
        AvaloniaProperty.RegisterAttached<Reorder, Control, bool>("IsGrip");

    public static string? GetId(Control control) => control.GetValue(IdProperty);

    public static bool GetIsGrip(Control control) => control.GetValue(IsGripProperty);

    public static void SetIsGrip(Control control, bool value) => control.SetValue(IsGripProperty, value);

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
