using Avalonia;
using Avalonia.Controls;

namespace DashDetective.Shared.Layout;

/// <summary>
/// Makes a <see cref="Grid"/>'s column set data-bindable. Avalonia's own <c>ColumnDefinitions</c>
/// property cannot be bound — it is a collection property with no bindable setter, and the XAML
/// compiler rejects a binding outright — so a responsive table has no other converter-free way to
/// change its columns with its width.
///
/// The responsive tables use this on both the sticky header and the shared row template, binding both
/// to one view-model string so the columns cannot drift apart.
/// </summary>
public static class GridColumns {
    /// <summary>A ColumnDefinitions string (e.g. <c>"2*,0*,1*"</c>) applied to the grid whenever it
    /// changes. Dropped columns are given zero width rather than removed, so cell Grid.Column indices
    /// stay put.</summary>
    public static readonly AttachedProperty<string?> DefinitionsProperty =
        AvaloniaProperty.RegisterAttached<Grid, string?>("Definitions", typeof(GridColumns));

    static GridColumns() {
        DefinitionsProperty.Changed.AddClassHandler<Grid>(OnDefinitionsChanged);
    }

    public static string? GetDefinitions(Grid grid) => grid.GetValue(DefinitionsProperty);

    public static void SetDefinitions(Grid grid, string? value) => grid.SetValue(DefinitionsProperty, value);

    private static void OnDefinitionsChanged(Grid grid, AvaloniaPropertyChangedEventArgs e) {
        if (e.NewValue is string definitions && !string.IsNullOrWhiteSpace(definitions))
            grid.ColumnDefinitions = ColumnDefinitions.Parse(definitions);
    }
}
