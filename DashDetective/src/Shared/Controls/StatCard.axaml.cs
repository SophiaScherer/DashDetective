using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace DashDetective.Shared.Controls;

/// <summary>
/// A summary metric card used on the Dashboard (CPU, Memory, GPU, …). Bundles a
/// label, coloured dot, value/unit, caption and a <see cref="Sparkline"/>.
/// </summary>
public partial class StatCard : UserControl {
    /// <summary>Whether the card is also a click target, which adds the shared selectable-card hover and
    /// hand cursor. The click itself belongs to the call site, which wraps the card in a button.</summary>
    public static readonly StyledProperty<bool> SelectableProperty =
        AvaloniaProperty.Register<StatCard, bool>(nameof(Selectable));

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

    public static readonly StyledProperty<double?> YMinProperty =
        AvaloniaProperty.Register<StatCard, double?>(nameof(YMin));

    public static readonly StyledProperty<double?> YMaxProperty =
        AvaloniaProperty.Register<StatCard, double?>(nameof(YMax));

    public static readonly StyledProperty<string?> AxisMaxLabelProperty =
        AvaloniaProperty.Register<StatCard, string?>(nameof(AxisMaxLabel));

    public static readonly StyledProperty<string?> AxisMinLabelProperty =
        AvaloniaProperty.Register<StatCard, string?>(nameof(AxisMinLabel));

    public StatCard() {
        InitializeComponent();
    }

    /// <summary>Uppercase metric name, e.g. "CPU".</summary>
    public bool Selectable {
        get => GetValue(SelectableProperty);
        set => SetValue(SelectableProperty, value);
    }

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

    /// <summary>Optional fixed lower bound for the sparkline's vertical axis (see <see cref="Sparkline.YMin"/>).</summary>
    public double? YMin {
        get => GetValue(YMinProperty);
        set => SetValue(YMinProperty, value);
    }

    /// <summary>Optional fixed upper bound for the sparkline's vertical axis (see <see cref="Sparkline.YMax"/>).</summary>
    public double? YMax {
        get => GetValue(YMaxProperty);
        set => SetValue(YMaxProperty, value);
    }

    /// <summary>Top of the card chart's axis, e.g. "100%". Not always a percentage: a card whose series is
    /// auto-scaled (throughput) tops out at its own live ceiling.</summary>
    public string? AxisMaxLabel {
        get => GetValue(AxisMaxLabelProperty);
        set => SetValue(AxisMaxLabelProperty, value);
    }

    /// <summary>Foot of the card chart's axis.</summary>
    public string? AxisMinLabel {
        get => GetValue(AxisMinLabelProperty);
        set => SetValue(AxisMinLabelProperty, value);
    }
}
