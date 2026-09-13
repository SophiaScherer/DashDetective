using Avalonia;
using System;

namespace DashDetective.Tabs.Settings;

/// <summary>
/// The arithmetic behind the color wheel and the brightness bar, kept out of the controls so it is testable
/// without a layout pass. Hue 0 sits at the top of the wheel and runs clockwise, as
/// <c>ConicGradientBrush</c> draws it.
/// </summary>
internal static class ColorPickerGeometry {
    internal const double HueStep = 1;
    internal const double CoarseHueStep = 15;
    internal const double Step = 0.01;
    internal const double CoarseStep = 0.1;

    /// <summary>The hue and saturation under <paramref name="point"/>. Outside the wheel clamps to its rim;
    /// the dead center keeps <paramref name="currentHue"/>, since no angle exists there.</summary>
    internal static (double Hue, double Saturation) WheelAt(Point point, Point center, double radius, double currentHue) {
        if (radius <= 0)
            return (currentHue, 0);

        var dx = point.X - center.X;
        var dy = point.Y - center.Y;
        var distance = Math.Sqrt(dx * dx + dy * dy);
        if (distance == 0)
            return (currentHue, 0);

        var hue = NormalizeHue(Math.Atan2(dx, -dy) * 180 / Math.PI);
        return (hue, Math.Min(distance / radius, 1));
    }

    /// <summary>Where a hue and saturation sit on the wheel.</summary>
    internal static Point WheelPoint(double hue, double saturation, Point center, double radius) {
        var angle = hue * Math.PI / 180;
        var distance = Math.Clamp(saturation, 0, 1) * radius;
        return new Point(center.X + distance * Math.Sin(angle), center.Y - distance * Math.Cos(angle));
    }

    /// <summary>The brightness under <paramref name="x"/> on a track from <paramref name="left"/> spanning
    /// <paramref name="width"/>, clamped to the ends.</summary>
    internal static double BarAt(double x, double left, double width) =>
        width <= 0 ? 0 : Math.Clamp((x - left) / width, 0, 1);

    /// <summary>A hue in [0, 360). Non-finite reads as red: Avalonia's own wrap loops forever on a huge hue.</summary>
    internal static double NormalizeHue(double hue) {
        if (!double.IsFinite(hue))
            return 0;

        var wrapped = hue % 360;
        if (wrapped < 0)
            wrapped += 360;

        // A tiny negative rounds up to exactly 360.
        return wrapped >= 360 ? 0 : wrapped;
    }

    /// <summary>A hue moved by whole key presses, wrapping past red.</summary>
    internal static double NudgeHue(double hue, int presses, bool coarse) =>
        NormalizeHue(hue + presses * (coarse ? CoarseHueStep : HueStep));

    /// <summary>A saturation or brightness moved by whole key presses, held to 0-1.</summary>
    internal static double NudgeUnit(double value, int presses, bool coarse) =>
        Math.Clamp(value + presses * (coarse ? CoarseStep : Step), 0, 1);
}
