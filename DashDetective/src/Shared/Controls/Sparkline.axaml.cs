using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using DashDetective.Shared.Charts;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace DashDetective.Shared.Controls;

/// <summary>
/// A reusable sparkline / line chart. Points are supplied as a whitespace separated
/// "x,y" string. By default the internal <see cref="Avalonia.Controls.Shapes.Polyline"/>
/// (inside a Viewbox) is auto-fitted to the data's own range; set <see cref="YMin"/> and
/// <see cref="YMax"/> to instead pin the vertical axis to a fixed range (e.g. 0–100 for a
/// CPU % chart), in which case the line is drawn directly in <see cref="Render"/>.
///
/// In fixed-range mode an optional second series (<see cref="Points2"/>/<see cref="Stroke2"/>)
/// may be supplied; both series share the same <see cref="YMin"/>/<see cref="YMax"/> axis so
/// their values are directly comparable. Set <see cref="Fill"/> to draw a translucent gradient
/// area beneath each line. Set <see cref="ShowGrid"/> to draw a faint lattice behind the data
/// (<see cref="GridRows"/> × <see cref="GridColumns"/>, coloured by <see cref="GridBrush"/>).
///
/// Fixed-range mode also carries optional axis furniture: three value labels down the left
/// (<see cref="AxisMaxLabel"/> / <see cref="AxisMidLabel"/> / <see cref="AxisMinLabel"/>), the ends of the
/// time range along the bottom (<see cref="AxisStartLabel"/> / <see cref="AxisEndLabel"/>) and a
/// <see cref="StatusText"/> line over the plot for a chart with nothing to draw yet. Each reserves
/// room only when it is set, so an unlabelled chart — every stat-card mini, every per-core cell — measures
/// and draws exactly as it did before.
///
/// All of these extras apply only to fixed-range mode; auto-fit mode is unchanged single-series behaviour.
/// </summary>
public partial class Sparkline : UserControl {
    public static readonly StyledProperty<IBrush?> StrokeProperty =
        AvaloniaProperty.Register<Sparkline, IBrush?>(nameof(Stroke));

    public static readonly StyledProperty<double> StrokeThicknessProperty =
        AvaloniaProperty.Register<Sparkline, double>(nameof(StrokeThickness), 1.6);

    public static readonly StyledProperty<string?> PointsProperty =
        AvaloniaProperty.Register<Sparkline, string?>(nameof(Points));

    public static readonly StyledProperty<double?> YMinProperty =
        AvaloniaProperty.Register<Sparkline, double?>(nameof(YMin));

    public static readonly StyledProperty<double?> YMaxProperty =
        AvaloniaProperty.Register<Sparkline, double?>(nameof(YMax));

    public static readonly StyledProperty<string?> Points2Property =
        AvaloniaProperty.Register<Sparkline, string?>(nameof(Points2));

    public static readonly StyledProperty<IBrush?> Stroke2Property =
        AvaloniaProperty.Register<Sparkline, IBrush?>(nameof(Stroke2));

    public static readonly StyledProperty<bool> FillProperty =
        AvaloniaProperty.Register<Sparkline, bool>(nameof(Fill));

    public static readonly StyledProperty<bool> ShowGridProperty =
        AvaloniaProperty.Register<Sparkline, bool>(nameof(ShowGrid));

    public static readonly StyledProperty<int> GridRowsProperty =
        AvaloniaProperty.Register<Sparkline, int>(nameof(GridRows), 4);

    public static readonly StyledProperty<int> GridColumnsProperty =
        AvaloniaProperty.Register<Sparkline, int>(nameof(GridColumns), 10);

    public static readonly StyledProperty<IBrush?> GridBrushProperty =
        AvaloniaProperty.Register<Sparkline, IBrush?>(nameof(GridBrush));

    public static readonly StyledProperty<double> AspectRatioProperty =
        AvaloniaProperty.Register<Sparkline, double>(nameof(AspectRatio), double.NaN);

    public static readonly StyledProperty<string?> AxisMaxLabelProperty =
        AvaloniaProperty.Register<Sparkline, string?>(nameof(AxisMaxLabel));

    public static readonly StyledProperty<string?> AxisMidLabelProperty =
        AvaloniaProperty.Register<Sparkline, string?>(nameof(AxisMidLabel));

    public static readonly StyledProperty<string?> AxisMinLabelProperty =
        AvaloniaProperty.Register<Sparkline, string?>(nameof(AxisMinLabel));

    public static readonly StyledProperty<string?> AxisStartLabelProperty =
        AvaloniaProperty.Register<Sparkline, string?>(nameof(AxisStartLabel));

    public static readonly StyledProperty<string?> AxisEndLabelProperty =
        AvaloniaProperty.Register<Sparkline, string?>(nameof(AxisEndLabel));

    public static readonly StyledProperty<string?> StatusTextProperty =
        AvaloniaProperty.Register<Sparkline, string?>(nameof(StatusText));

    public static readonly StyledProperty<IBrush?> AxisBrushProperty =
        AvaloniaProperty.Register<Sparkline, IBrush?>(nameof(AxisBrush));

    /// <summary>Axis and status text size. Smaller than the surrounding captions on purpose: the labels are
    /// a scale to read the chart against, not part of the page's copy.</summary>
    private const double AxisFontSize = 10;

    private List<Point> _data = new();
    private List<Point> _data2 = new();
    private bool _fixedRange;
    private double _yMin, _yMax;

    static Sparkline() {
        AffectsMeasure<Sparkline>(AspectRatioProperty);
    }

    public Sparkline() {
        InitializeComponent();
    }

    /// <summary>Line colour.</summary>
    public IBrush? Stroke {
        get => GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    /// <summary>Line thickness in pixels.</summary>
    public double StrokeThickness {
        get => GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    /// <summary>Points as a "x,y x,y …" string (any consistent coordinate range).</summary>
    public string? Points {
        get => GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    /// <summary>Optional lower bound of the vertical axis. Set with <see cref="YMax"/> to fix the scale.</summary>
    public double? YMin {
        get => GetValue(YMinProperty);
        set => SetValue(YMinProperty, value);
    }

    /// <summary>Optional upper bound of the vertical axis. Set with <see cref="YMin"/> to fix the scale.</summary>
    public double? YMax {
        get => GetValue(YMaxProperty);
        set => SetValue(YMaxProperty, value);
    }

    /// <summary>Optional second series ("x,y x,y …"), drawn on the same fixed axis as <see cref="Points"/>.</summary>
    public string? Points2 {
        get => GetValue(Points2Property);
        set => SetValue(Points2Property, value);
    }

    /// <summary>Line colour for the second series.</summary>
    public IBrush? Stroke2 {
        get => GetValue(Stroke2Property);
        set => SetValue(Stroke2Property, value);
    }

    /// <summary>When true (fixed-range mode), draw a translucent gradient area beneath each line.</summary>
    public bool Fill {
        get => GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    /// <summary>When true (fixed-range mode), draw a faint lattice behind the data.</summary>
    public bool ShowGrid {
        get => GetValue(ShowGridProperty);
        set => SetValue(ShowGridProperty, value);
    }

    /// <summary>Number of horizontal bands in the grid (draws this many + 1 lines). Design default: 4.</summary>
    public int GridRows {
        get => GetValue(GridRowsProperty);
        set => SetValue(GridRowsProperty, value);
    }

    /// <summary>Number of vertical columns in the grid (draws this many + 1 lines). Design default: 10.</summary>
    public int GridColumns {
        get => GetValue(GridColumnsProperty);
        set => SetValue(GridColumnsProperty, value);
    }

    /// <summary>Grid line colour. Falls back to the themed <c>ChartGrid</c> resource when unset.</summary>
    public IBrush? GridBrush {
        get => GetValue(GridBrushProperty);
        set => SetValue(GridBrushProperty, value);
    }

    /// <summary>Width ÷ height the chart holds as its slot resizes, bounded by <c>MinHeight</c> and
    /// <c>MaxHeight</c>. NaN (the default) leaves sizing to an explicit Height.</summary>
    public double AspectRatio {
        get => GetValue(AspectRatioProperty);
        set => SetValue(AspectRatioProperty, value);
    }

    /// <summary>Value label at the top of the axis, e.g. "100%" or "12 Mbps". Empty draws no gutter.</summary>
    public string? AxisMaxLabel {
        get => GetValue(AxisMaxLabelProperty);
        set => SetValue(AxisMaxLabelProperty, value);
    }

    /// <summary>Value label halfway up the axis, e.g. "50%".</summary>
    public string? AxisMidLabel {
        get => GetValue(AxisMidLabelProperty);
        set => SetValue(AxisMidLabelProperty, value);
    }

    /// <summary>Value label at the foot of the axis, e.g. "0".</summary>
    public string? AxisMinLabel {
        get => GetValue(AxisMinLabelProperty);
        set => SetValue(AxisMinLabelProperty, value);
    }

    /// <summary>Oldest end of the time range, e.g. "−60s". Empty draws no footer.</summary>
    public string? AxisStartLabel {
        get => GetValue(AxisStartLabelProperty);
        set => SetValue(AxisStartLabelProperty, value);
    }

    /// <summary>Newest end of the time range, e.g. "now".</summary>
    public string? AxisEndLabel {
        get => GetValue(AxisEndLabelProperty);
        set => SetValue(AxisEndLabelProperty, value);
    }

    /// <summary>A line drawn over the plot instead of leaving it to explain itself — a chart with nothing
    /// to show yet says so here. Empty draws nothing.</summary>
    public string? StatusText {
        get => GetValue(StatusTextProperty);
        set => SetValue(StatusTextProperty, value);
    }

    /// <summary>Axis and status text colour. Falls back to the themed <c>TextSubtle</c> resource.</summary>
    public IBrush? AxisBrush {
        get => GetValue(AxisBrushProperty);
        set => SetValue(AxisBrushProperty, value);
    }

    /// <summary>Derives the height from the measured width when <see cref="AspectRatio"/> is set, so the
    /// chart keeps its shape instead of flattening as its slot narrows. An explicit Height still wins:
    /// Avalonia's MeasureCore clamps this result to [MinHeight, MaxHeight], which a set Height pins to
    /// itself. MaxHeight is therefore also the absolute cap on how tall a wide chart grows.</summary>
    protected override Size MeasureOverride(Size availableSize) {
        var baseSize = base.MeasureOverride(availableSize); // applies the template
        if (double.IsNaN(AspectRatio))
            return baseSize;

        var width = availableSize.Width;
        var height = ChartAspect.HeightForWidth(width, AspectRatio, MinHeight, MaxHeight);
        return new Size(double.IsFinite(width) ? width : baseSize.Width, height);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change) {
        base.OnPropertyChanged(change);
        if (change.Property == PointsProperty
            || change.Property == Points2Property
            || change.Property == YMinProperty
            || change.Property == YMaxProperty)
            Rebuild();
        else if (_fixedRange
            && (change.Property == StrokeProperty || change.Property == StrokeThicknessProperty
                || change.Property == Stroke2Property || change.Property == FillProperty
                || change.Property == ShowGridProperty || change.Property == GridRowsProperty
                || change.Property == GridColumnsProperty || change.Property == GridBrushProperty
                || change.Property == AxisMaxLabelProperty || change.Property == AxisMidLabelProperty
                || change.Property == AxisMinLabelProperty || change.Property == AxisStartLabelProperty
                || change.Property == AxisEndLabelProperty || change.Property == StatusTextProperty
                || change.Property == AxisBrushProperty))
            InvalidateVisual();
    }

    public override void Render(DrawingContext context) {
        base.Render(context);
        if (!_fixedRange)
            return;

        double w = Bounds.Width, h = Bounds.Height;
        if (w <= 0 || h <= 0)
            return;

        // Axis text is measured before anything is drawn: what it needs decides how much room the plot has.
        var brush = AxisBrush ?? ResolveResource("TextSubtle");
        var top = Label(AxisMaxLabel, brush);
        var middle = Label(AxisMidLabel, brush);
        var bottom = Label(AxisMinLabel, brush);
        var start = Label(AxisStartLabel, brush);
        var end = Label(AxisEndLabel, brush);

        var plot = ChartAxis.PlotRect(w, h,
            ChartAxis.Gutter(TextWidth(top), TextWidth(middle), TextWidth(bottom)),
            ChartAxis.Footer(Math.Max(TextHeight(start), TextHeight(end))));

        // The grid and the axis labels sit behind the data and show even before any samples arrive, so an
        // empty chart still says what its scale is.
        if (ShowGrid)
            DrawGrid(context, plot);
        DrawAxisLabels(context, plot, top, middle, bottom, start, end);

        DrawSeries(context, plot);
        DrawStatus(context, plot);
    }

    /// <summary>Draws whichever series have enough points, all fills first so no line is occluded by one.</summary>
    private void DrawSeries(DrawingContext context, Rect plot) {
        var hasSeries1 = _data.Count >= 2 && Stroke is not null;
        var hasSeries2 = _data2.Count >= 2 && Stroke2 is not null;
        if (!hasSeries1 && !hasSeries2)
            return;

        // Share the x scale across both series so equal indices line up horizontally.
        var maxX = 0.0;
        foreach (var p in _data)
            if (p.X > maxX) maxX = p.X;
        foreach (var p in _data2)
            if (p.X > maxX) maxX = p.X;

        var span = _yMax - _yMin;
        if (span <= 0)
            return;

        if (Fill) {
            if (hasSeries1)
                DrawArea(context, _data, Stroke, plot, maxX, span);
            if (hasSeries2)
                DrawArea(context, _data2, Stroke2, plot, maxX, span);
        }

        if (hasSeries1)
            DrawLine(context, _data, Stroke!, plot, maxX, span);
        if (hasSeries2)
            DrawLine(context, _data2, Stroke2!, plot, maxX, span);
    }

    /// <summary>Value labels down the left of the plot and the time-range ends beneath it. The outer two
    /// value labels are pulled inside the plot's edges rather than centred on them, so neither is clipped.</summary>
    private static void DrawAxisLabels(DrawingContext context, Rect plot, FormattedText? top,
        FormattedText? middle, FormattedText? bottom, FormattedText? start, FormattedText? end) {
        if (top is not null)
            context.DrawText(top, new Point(plot.Left - ChartAxis.LabelGap - top.Width, plot.Top));
        if (middle is not null)
            context.DrawText(middle, new Point(plot.Left - ChartAxis.LabelGap - middle.Width,
                plot.Center.Y - middle.Height / 2));
        if (bottom is not null)
            context.DrawText(bottom, new Point(plot.Left - ChartAxis.LabelGap - bottom.Width,
                plot.Bottom - bottom.Height));

        var footerTop = plot.Bottom + ChartAxis.FooterGap;
        if (start is not null)
            context.DrawText(start, new Point(plot.Left, footerTop));
        if (end is not null)
            context.DrawText(end, new Point(plot.Right - end.Width, footerTop));
    }

    /// <summary>Centres <see cref="StatusText"/> over the plot. Drawn last, so it reads over whatever few
    /// samples have arrived rather than under them.</summary>
    private void DrawStatus(DrawingContext context, Rect plot) {
        var status = Label(StatusText, AxisBrush ?? ResolveResource("TextMuted"));
        if (status is null)
            return;

        context.DrawText(status, new Point(
            plot.Center.X - status.Width / 2, plot.Center.Y - status.Height / 2));
    }

    /// <summary>An axis label ready to measure and draw, or null when there is nothing to say.</summary>
    private FormattedText? Label(string? value, IBrush? brush) =>
        string.IsNullOrEmpty(value) || brush is null
            ? null
            : new FormattedText(value, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                                new Typeface(FontFamily), AxisFontSize, brush);

    private static double TextWidth(FormattedText? text) => text?.Width ?? 0;

    private static double TextHeight(FormattedText? text) => text?.Height ?? 0;

    /// <summary>Draws a faint lattice (<see cref="GridRows"/>+1 horizontal, <see cref="GridColumns"/>+1 vertical
    /// lines) behind the data. Coordinates are snapped to +0.5 device pixels for crisp 1px lines.</summary>
    private void DrawGrid(DrawingContext context, Rect plot) {
        var brush = GridBrush ?? ResolveResource("ChartGrid");
        if (brush is null)
            return;

        var pen = new Pen(brush, 1);

        var rows = Math.Max(1, GridRows);
        for (var i = 0; i <= rows; i++) {
            var y = Math.Round(plot.Top + plot.Height / rows * i) + 0.5;
            context.DrawLine(pen, new Point(plot.Left, y), new Point(plot.Right, y));
        }

        var cols = Math.Max(1, GridColumns);
        for (var i = 0; i <= cols; i++) {
            var x = Math.Round(plot.Left + plot.Width / cols * i) + 0.5;
            context.DrawLine(pen, new Point(x, plot.Top), new Point(x, plot.Bottom));
        }
    }

    /// <summary>A themed fallback brush, resolved for the current theme variant. TryFindResource rather than
    /// FindResource: the latter misses theme-dictionary brushes from code-behind and returns null.</summary>
    private IBrush? ResolveResource(string key) =>
        this.TryFindResource(key, ActualThemeVariant, out var value) ? value as IBrush : null;

    /// <summary>Maps a data point into the plot area. Keeps "smaller y = top" so the axis floor is at the top.</summary>
    private Point ToPixel(Point p, Rect plot, double maxX, double span) {
        var px = plot.Left + (maxX > 0 ? p.X / maxX * plot.Width : 0);
        var py = plot.Top + (p.Y - _yMin) / span * plot.Height;
        return new Point(px, py);
    }

    private void DrawLine(DrawingContext context, List<Point> data, IBrush stroke,
        Rect plot, double maxX, double span) {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open()) {
            var first = true;
            foreach (var p in data) {
                var point = ToPixel(p, plot, maxX, span);
                if (first) {
                    ctx.BeginFigure(point, isFilled: false);
                    first = false;
                } else {
                    ctx.LineTo(point);
                }
            }
            ctx.EndFigure(isClosed: false);
        }

        var pen = new Pen(stroke, StrokeThickness) {
            LineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round,
        };
        context.DrawGeometry(null, pen, geometry);
    }

    private void DrawArea(DrawingContext context, List<Point> data, IBrush? stroke,
        Rect plot, double maxX, double span) {
        var fill = MakeAreaBrush(stroke);
        if (fill is null)
            return;

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open()) {
            var start = ToPixel(data[0], plot, maxX, span);
            ctx.BeginFigure(new Point(start.X, plot.Bottom), isFilled: true); // start on the bottom axis
            ctx.LineTo(start);
            for (var i = 1; i < data.Count; i++)
                ctx.LineTo(ToPixel(data[i], plot, maxX, span));
            var end = ToPixel(data[^1], plot, maxX, span);
            ctx.LineTo(new Point(end.X, plot.Bottom)); // drop back to the bottom axis
            ctx.EndFigure(isClosed: true);
        }

        context.DrawGeometry(fill, null, geometry);
    }

    /// <summary>Vertical gradient (α 0.34 → 0.02) of the series colour for the area fill; null if colour unknown.</summary>
    private static IBrush? MakeAreaBrush(IBrush? stroke) {
        if (stroke is not ISolidColorBrush solid)
            return null;
        var c = solid.Color;
        return new LinearGradientBrush {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops = {
                new GradientStop(Color.FromArgb(87, c.R, c.G, c.B), 0),  // ~0.34 alpha
                new GradientStop(Color.FromArgb(5, c.R, c.G, c.B), 1),   // ~0.02 alpha
            },
        };
    }

    private void Rebuild() {
        _data = Parse(Points);
        _data2 = Parse(Points2);
        _fixedRange = YMin is double lo && YMax is double hi && hi > lo;

        if (_fixedRange) {
            _yMin = YMin!.Value;
            _yMax = YMax!.Value;
            AutoBox.IsVisible = false;
            InvalidateVisual();
            return;
        }

        // Auto-fit mode: unchanged legacy behaviour (Viewbox stretches the raw points).
        AutoLine.Points.Clear();
        foreach (var p in _data)
            AutoLine.Points.Add(p);
        AutoBox.IsVisible = true;
        InvalidateVisual();
    }

    private static List<Point> Parse(string? raw) {
        var result = new List<Point>();
        if (string.IsNullOrWhiteSpace(raw))
            return result;

        var tokens = raw.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var token in tokens) {
            var parts = token.Split(',');
            if (parts.Length == 2
                && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
                && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y)) {
                result.Add(new Point(x, y));
            }
        }

        return result;
    }
}
