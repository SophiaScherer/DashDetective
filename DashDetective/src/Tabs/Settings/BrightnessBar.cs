using Avalonia;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using System;

namespace DashDetective.Tabs.Settings;

/// <summary>
/// The wheel's brightness: a track from black to the chosen hue and saturation at full brightness.
/// ←/↓ darken, →/↑ lighten, Home/End jump to the ends.
/// </summary>
public sealed class BrightnessBar : ColorPickSurface {
    public static readonly StyledProperty<double> HueProperty =
        AvaloniaProperty.Register<BrightnessBar, double>(nameof(Hue));

    public static readonly StyledProperty<double> SaturationProperty =
        AvaloniaProperty.Register<BrightnessBar, double>(nameof(Saturation));

    public static readonly StyledProperty<double> BrightnessProperty =
        AvaloniaProperty.Register<BrightnessBar, double>(nameof(Brightness), 1, defaultBindingMode: BindingMode.TwoWay);

    public double Hue {
        get => GetValue(HueProperty);
        set => SetValue(HueProperty, value);
    }

    public double Saturation {
        get => GetValue(SaturationProperty);
        set => SetValue(SaturationProperty, value);
    }

    /// <summary>0 black, 1 full.</summary>
    public double Brightness {
        get => GetValue(BrightnessProperty);
        set => SetValue(BrightnessProperty, value);
    }

    static BrightnessBar() {
        AffectsRender<BrightnessBar>(HueProperty, SaturationProperty, BrightnessProperty);
    }

    /// <summary>Room for the thumb and the focus ring above and below the track.</summary>
    private const double BarHeight = 24;

    /// <summary>The width used when the layout offers unlimited room.</summary>
    private const double DefaultWidth = 180;

    // Past the thumb's outer pen, so an end thumb stays inside the bounds.
    private double TrackLeft => ThumbRadius + 2;

    private double TrackWidth => Bounds.Width - 2 * TrackLeft;

    protected override Size MeasureOverride(Size availableSize) =>
        new(double.IsInfinity(availableSize.Width) ? DefaultWidth : availableSize.Width, BarHeight);

    public override void Render(DrawingContext context) {
        if (TrackWidth <= 0)
            return;

        var middle = Bounds.Height / 2;
        var track = new Rect(TrackLeft, middle - 5, TrackWidth, 10);
        var full = HsvColor.ToRgb(ColorPickerGeometry.NormalizeHue(Hue), Math.Clamp(Saturation, 0, 1), 1);
        var fill = new ImmutableLinearGradientBrush(
            [new ImmutableGradientStop(0, Colors.Black), new ImmutableGradientStop(1, full)],
            startPoint: new RelativePoint(0, 0.5, RelativeUnit.Relative),
            endPoint: new RelativePoint(1, 0.5, RelativeUnit.Relative));

        context.DrawRectangle(fill, null, track, 5, 5);

        if (ShowsFocus) {
            var (outer, inner) = FocusPens;
            var ring = track.Inflate(3);
            context.DrawRectangle(null, outer, ring, 8, 8);
            context.DrawRectangle(null, inner, ring, 8, 8);
        }

        DrawThumb(context, new Point(TrackLeft + Math.Clamp(Brightness, 0, 1) * TrackWidth, middle));
    }

    protected override void PickAt(Point point) {
        if (TrackWidth > 0)
            SetCurrentValue(BrightnessProperty, ColorPickerGeometry.BarAt(point.X, TrackLeft, TrackWidth));
    }

    protected override bool Nudge(Key key, bool coarse) {
        var value = key switch {
            Key.Right or Key.Up => ColorPickerGeometry.NudgeUnit(Brightness, 1, coarse),
            Key.Left or Key.Down => ColorPickerGeometry.NudgeUnit(Brightness, -1, coarse),
            Key.Home => 0,
            Key.End => 1,
            _ => double.NaN,
        };

        if (double.IsNaN(value))
            return false;

        SetCurrentValue(BrightnessProperty, value);
        return true;
    }
}
