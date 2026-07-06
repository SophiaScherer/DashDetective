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
}
