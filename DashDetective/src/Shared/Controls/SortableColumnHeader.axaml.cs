using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace DashDetective.Shared.Controls;

/// <summary>
/// A table column header that sorts its table when clicked, showing a direction arrow while it is the
/// active sort. File Explorer and Processes list eleven of these between them; the markup lives here
/// once rather than being copy-pasted per column.
/// </summary>
public partial class SortableColumnHeader : UserControl {
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<SortableColumnHeader, string?>(nameof(Text));

    public static readonly StyledProperty<SortColumn?> ColumnProperty =
        AvaloniaProperty.Register<SortableColumnHeader, SortColumn?>(nameof(Column));

    public static readonly StyledProperty<bool> ArrowFirstProperty =
        AvaloniaProperty.Register<SortableColumnHeader, bool>(nameof(ArrowFirst));

    public static readonly StyledProperty<HorizontalAlignment> ContentAlignmentProperty =
        AvaloniaProperty.Register<SortableColumnHeader, HorizontalAlignment>(
            nameof(ContentAlignment), HorizontalAlignment.Left);

    public SortableColumnHeader() {
        InitializeComponent();
    }

    /// <summary>The column label, already uppercased by the caller.</summary>
    public string? Text {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>The column's sort state and command.</summary>
    public SortColumn? Column {
        get => GetValue(ColumnProperty);
        set => SetValue(ColumnProperty, value);
    }

    /// <summary>Puts the arrow before the label instead of after it — what a right-aligned numeric
    /// column wants, so the label stays flush against the column edge its digits line up on.</summary>
    public bool ArrowFirst {
        get => GetValue(ArrowFirstProperty);
        set => SetValue(ArrowFirstProperty, value);
    }

    /// <summary>Where the label sits inside the cell. Set this rather than the control's own
    /// HorizontalAlignment: aligning the control shrinks it to its text, so only the glyphs stay
    /// clickable and the rest of the column stops sorting.</summary>
    public HorizontalAlignment ContentAlignment {
        get => GetValue(ContentAlignmentProperty);
        set => SetValue(ContentAlignmentProperty, value);
    }
}
