using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace DashDetective.Shared.Controls;

/// <summary>
/// A chart's key: up to two series, each a swatch in its own colour beside its name.
///
/// The throughput charts draw receive and send on one axis, and named neither — two coloured lines with
/// nothing saying which was which. An entry takes no room when its label is empty, so a chart that grows a
/// second series later needs no layout change, and one that never has a second stays a single entry.
/// </summary>
public partial class ChartLegend : UserControl {
    public static readonly StyledProperty<string?> Label1Property =
        AvaloniaProperty.Register<ChartLegend, string?>(nameof(Label1));

    public static readonly StyledProperty<IBrush?> Brush1Property =
        AvaloniaProperty.Register<ChartLegend, IBrush?>(nameof(Brush1));

    public static readonly StyledProperty<string?> Label2Property =
        AvaloniaProperty.Register<ChartLegend, string?>(nameof(Label2));

    public static readonly StyledProperty<IBrush?> Brush2Property =
        AvaloniaProperty.Register<ChartLegend, IBrush?>(nameof(Brush2));

    public static readonly StyledProperty<bool> Pattern2Property =
        AvaloniaProperty.Register<ChartLegend, bool>(nameof(Pattern2));

    public ChartLegend() {
        InitializeComponent();
    }

    /// <summary>The first series' name. Empty hides the entry.</summary>
    public string? Label1 {
        get => GetValue(Label1Property);
        set => SetValue(Label1Property, value);
    }

    /// <summary>The first series' colour, matching its line on the chart.</summary>
    public IBrush? Brush1 {
        get => GetValue(Brush1Property);
        set => SetValue(Brush1Property, value);
    }

    /// <summary>The second series' name. Empty hides the entry.</summary>
    public string? Label2 {
        get => GetValue(Label2Property);
        set => SetValue(Label2Property, value);
    }

    /// <summary>The second series' colour, matching its line on the chart.</summary>
    public IBrush? Brush2 {
        get => GetValue(Brush2Property);
        set => SetValue(Brush2Property, value);
    }

    /// <summary>Whether the chart is drawing its second series dashed. Both swatches become line marks
    /// when it is — solid and dashed — so the key shows the same distinction the chart makes rather than
    /// repeating the colour twice. Mirrors <c>Sparkline.PatternSecondSeries</c>; a legend saying one thing
    /// while the chart beside it does another would be worse than no legend.</summary>
    public bool Pattern2 {
        get => GetValue(Pattern2Property);
        set => SetValue(Pattern2Property, value);
    }
}
