using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace DashDetective.Shared.Controls;

/// <summary>
/// A summary metric card used on the Dashboard (CPU, Memory, GPU, …). Bundles a
/// label, coloured dot, value/unit, caption and a <see cref="Sparkline"/>.
/// </summary>
public partial class StatCard : UserControl {
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<StatCard, string?>(nameof(Label));

    public static readonly StyledProperty<string?> ValueProperty =
        AvaloniaProperty.Register<StatCard, string?>(nameof(Value));

    public static readonly StyledProperty<string?> UnitProperty =
        AvaloniaProperty.Register<StatCard, string?>(nameof(Unit));

    public static readonly StyledProperty<string?> SubProperty =
        AvaloniaProperty.Register<StatCard, string?>(nameof(Sub));

    public static readonly StyledProperty<IBrush?> AccentProperty =
        AvaloniaProperty.Register<StatCard, IBrush?>(nameof(Accent));

    public static readonly StyledProperty<string?> PointsProperty =
        AvaloniaProperty.Register<StatCard, string?>(nameof(Points));

    public StatCard() {
        InitializeComponent();
    }

    /// <summary>Uppercase metric name, e.g. "CPU".</summary>
    public string? Label {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Primary reading, e.g. "23".</summary>
    public string? Value {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>Unit shown next to the value, e.g. "%".</summary>
    public string? Unit {
        get => GetValue(UnitProperty);
        set => SetValue(UnitProperty, value);
    }

    /// <summary>Secondary caption, e.g. "Intel Core i9-14900K".</summary>
    public string? Sub {
        get => GetValue(SubProperty);
        set => SetValue(SubProperty, value);
    }

    /// <summary>Accent colour for the dot and sparkline.</summary>
    public IBrush? Accent {
        get => GetValue(AccentProperty);
        set => SetValue(AccentProperty, value);
    }

    /// <summary>Sparkline points as a "x,y x,y …" string.</summary>
    public string? Points {
        get => GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }
}
