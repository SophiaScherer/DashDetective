using Avalonia;
using Avalonia.Controls;

namespace DashDetective.Shared.Controls;

/// <summary>
/// A labelled key/value row (key on the left, value right-aligned) with a bottom
/// separator. Reused wherever specs are listed as pairs.
/// </summary>
public partial class InfoRow : UserControl {
    public static readonly StyledProperty<string?> KeyProperty =
        AvaloniaProperty.Register<InfoRow, string?>(nameof(Key));

    public static readonly StyledProperty<string?> ValueProperty =
        AvaloniaProperty.Register<InfoRow, string?>(nameof(Value));

    public static readonly StyledProperty<bool> MonoProperty =
        AvaloniaProperty.Register<InfoRow, bool>(nameof(Mono));

    public static readonly StyledProperty<bool> FlushProperty =
        AvaloniaProperty.Register<InfoRow, bool>(nameof(Flush));

    public InfoRow() {
        InitializeComponent();
    }

    public string? Key {
        get => GetValue(KeyProperty);
        set => SetValue(KeyProperty, value);
    }

    public string? Value {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>Draw the value in the monospace face, for addresses and IDs that read better
    /// column-aligned.</summary>
    public bool Mono {
        get => GetValue(MonoProperty);
        set => SetValue(MonoProperty, value);
    }

    /// <summary>Drop the bottom divider, for a list that reads as one block rather than a table.</summary>
    public bool Flush {
        get => GetValue(FlushProperty);
        set => SetValue(FlushProperty, value);
    }
}
