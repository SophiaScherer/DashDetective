using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace DashDetective.Shared.Controls;

/// <summary>
/// A key/value row for long path values. Collapsed, it shows the value on one truncated line with a
/// "…" toggle; expanded, it shows the full value wrapped onto its own line. Keeps its own expand
/// state (reset whenever <see cref="Value"/> re-binds) so a new selection always starts collapsed.
/// </summary>
public partial class ExpandablePathRow : UserControl {
    public static readonly StyledProperty<string?> KeyProperty =
        AvaloniaProperty.Register<ExpandablePathRow, string?>(nameof(Key));

    public static readonly StyledProperty<string?> ValueProperty =
        AvaloniaProperty.Register<ExpandablePathRow, string?>(nameof(Value));

    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<ExpandablePathRow, bool>(nameof(IsExpanded));

    public ExpandablePathRow() {
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

    /// <summary>Whether the full value is revealed (wrapped on its own line).</summary>
    public bool IsExpanded {
        get => GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change) {
        base.OnPropertyChanged(change);
        // Selecting another file re-binds Value; collapse so a long path can't leave the row
        // expanded from a previous selection.
        if (change.Property == ValueProperty)
            IsExpanded = false;
    }

    private void OnToggle(object? sender, RoutedEventArgs e) => IsExpanded = !IsExpanded;
}
