using Avalonia;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using System;

namespace DashDetective.Tabs.Settings;

/// <summary>
/// A hue and saturation wheel: hue around, saturation outward, dimmed by <see cref="Brightness"/>.
/// ←/→ turn the hue, ↑/↓ change saturation.
/// </summary>
public sealed class ColorWheel : ColorPickSurface {
    public static readonly StyledProperty<double> HueProperty =
        AvaloniaProperty.Register<ColorWheel, double>(nameof(Hue), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<double> SaturationProperty =
        AvaloniaProperty.Register<ColorWheel, double>(nameof(Saturation), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<double> BrightnessProperty =
        AvaloniaProperty.Register<ColorWheel, double>(nameof(Brightness), 1);

    /// <summary>0-360, red at the top.</summary>
    public double Hue {
        get => GetValue(HueProperty);
        set => SetValue(HueProperty, value);
    }

    /// <summary>0 at the center, 1 at the rim.</summary>
    public double Saturation {
        get => GetValue(SaturationProperty);
        set => SetValue(SaturationProperty, value);
    }

    /// <summary>Only shades the wheel; the brightness bar owns the value.</summary>
    public double Brightness {
        get => GetValue(BrightnessProperty);
        set => SetValue(BrightnessProperty, value);
    }

    /// <summary>The full-saturation hue sweep; also the Custom swatch's fill.</summary>
    internal static readonly IBrush HueRing = BuildHueRing();

    private static readonly IBrush WhiteCenter = new ImmutableRadialGradientBrush(
        [new ImmutableGradientStop(0, Colors.White), new ImmutableGradientStop(1, Color.FromArgb(0, 255, 255, 255))],
        radiusX: new RelativeScalar(0.5, RelativeUnit.Relative),
        radiusY: new RelativeScalar(0.5, RelativeUnit.Relative));

    static ColorWheel() {
        AffectsRender<ColorWheel>(HueProperty, SaturationProperty, BrightnessProperty);
    }

    /// <summary>The side used when the layout offers unlimited room.</summary>
    private const double DefaultSide = 180;

    /// <summary>A square as large as the room allows.</summary>
    protected override Size MeasureOverride(Size availableSize) {
        var side = Math.Min(availableSize.Width, availableSize.Height);
        side = double.IsInfinity(side) ? DefaultSide : side;
        return new Size(side, side);
    }

    private Point Center => new(Bounds.Width / 2, Bounds.Height / 2);

    private double Radius => Math.Min(Bounds.Width, Bounds.Height) / 2 - ThumbRadius - 3;

    public override void Render(DrawingContext context) {
        var radius = Radius;
        if (radius <= 0)
            return;

        var center = Center;
        context.DrawEllipse(HueRing, null, center, radius, radius);
        context.DrawEllipse(WhiteCenter, null, center, radius, radius);
        if (Brightness < 1)
            context.DrawEllipse(new ImmutableSolidColorBrush(Colors.Black, 1 - Brightness), null, center, radius, radius);

        if (ShowsFocus) {
            var (outer, inner) = FocusPens;
            context.DrawEllipse(null, outer, center, radius + 3, radius + 3);
            context.DrawEllipse(null, inner, center, radius + 3, radius + 3);
        }

        DrawThumb(context, ColorPickerGeometry.WheelPoint(ColorPickerGeometry.NormalizeHue(Hue), Saturation, center, radius));
    }

    protected override void PickAt(Point point) {
        if (Radius <= 0)
            return;

        var (hue, saturation) = ColorPickerGeometry.WheelAt(point, Center, Radius, Hue);
        SetCurrentValue(HueProperty, hue);
        SetCurrentValue(SaturationProperty, saturation);
    }

    protected override bool Nudge(Key key, bool coarse) {
        switch (key) {
            case Key.Left or Key.Right:
                SetCurrentValue(HueProperty, ColorPickerGeometry.NudgeHue(Hue, key == Key.Right ? 1 : -1, coarse));
                return true;
            case Key.Up or Key.Down:
                SetCurrentValue(SaturationProperty,
                                ColorPickerGeometry.NudgeUnit(Saturation, key == Key.Up ? 1 : -1, coarse));
                return true;
            default:
                return false;
        }
    }

    /// <summary>Full-saturation hues every 30 degrees, closing back on red.</summary>
    private static IBrush BuildHueRing() {
        var stops = new ImmutableGradientStop[13];
        for (var i = 0; i < stops.Length; i++)
            stops[i] = new ImmutableGradientStop(i / 12.0, HsvColor.ToRgb(i * 30 % 360, 1, 1));
        return new ImmutableConicGradientBrush(stops);
    }
}
