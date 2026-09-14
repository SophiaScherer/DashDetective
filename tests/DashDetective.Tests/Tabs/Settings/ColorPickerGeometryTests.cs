using Avalonia;
using DashDetective.Tabs.Settings;
using Xunit;

namespace DashDetective.Tests.Tabs.Settings;

/// <summary>
/// Covers <see cref="ColorPickerGeometry"/>: a point and its hue and saturation round-trip, the wheel runs
/// clockwise from red at the top, picks outside the rim clamp, and key steps wrap or clamp.
/// </summary>
public class ColorPickerGeometryTests {
    private static readonly Point Center = new(100, 100);
    private const double Radius = 80;

    [Theory]
    [InlineData(0, 100, 20)]    // top
    [InlineData(90, 180, 100)]  // right
    [InlineData(180, 100, 180)] // bottom
    [InlineData(270, 20, 100)]  // left
    public void WheelAt_TheRim_RunsClockwiseFromTheTop(double hue, double x, double y) {
        var (pickedHue, saturation) = ColorPickerGeometry.WheelAt(new Point(x, y), Center, Radius, 0);

        Assert.Equal(hue, pickedHue, 6);
        Assert.Equal(1, saturation, 6);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(37.5, 0.25)]
    [InlineData(210, 0.8)]
    [InlineData(359, 1)]
    public void WheelPoint_RoundTripsThroughWheelAt(double hue, double saturation) {
        var point = ColorPickerGeometry.WheelPoint(hue, saturation, Center, Radius);
        var (pickedHue, pickedSaturation) = ColorPickerGeometry.WheelAt(point, Center, Radius, hue);

        Assert.Equal(saturation, pickedSaturation, 6);
        if (saturation > 0)
            Assert.Equal(hue, pickedHue, 6);
    }

    [Fact]
    public void WheelAt_BeyondTheRim_ClampsToFullSaturation() {
        var (hue, saturation) = ColorPickerGeometry.WheelAt(new Point(400, 100), Center, Radius, 0);

        Assert.Equal(90, hue, 6);
        Assert.Equal(1, saturation);
    }

    /// <summary>The center has no angle, so it must not reset the hue the user was on.</summary>
    [Fact]
    public void WheelAt_TheCenter_KeepsTheCurrentHue() {
        Assert.Equal((123.0, 0.0), ColorPickerGeometry.WheelAt(Center, Center, Radius, 123));
    }

    [Fact]
    public void WheelAt_NoRoom_KeepsTheHueWithNoSaturation() {
        Assert.Equal((45.0, 0.0), ColorPickerGeometry.WheelAt(new Point(5, 5), Center, 0, 45));
    }

    [Theory]
    [InlineData(-1e-14, 0)]
    [InlineData(360, 0)]
    [InlineData(725, 5)]
    [InlineData(double.PositiveInfinity, 0)]
    [InlineData(double.NaN, 0)]
    public void NormalizeHue_StaysInsideAFullTurn(double hue, double expected) {
        Assert.Equal(expected, ColorPickerGeometry.NormalizeHue(hue), 6);
    }

    /// <summary>Avalonia's HSV conversion loops forever on a hue this large, so it must arrive wrapped.</summary>
    [Fact]
    public void NormalizeHue_AHugeHue_LandsInsideAFullTurn() {
        Assert.InRange(ColorPickerGeometry.NormalizeHue(1e20), 0, 360 - 1e-9);
    }

    [Theory]
    [InlineData(10, 0)]
    [InlineData(60, 0.5)]
    [InlineData(110, 1)]
    [InlineData(-50, 0)]
    [InlineData(500, 1)]
    public void BarAt_MapsTheTrackAndClampsTheEnds(double x, double expected) {
        Assert.Equal(expected, ColorPickerGeometry.BarAt(x, left: 10, width: 100), 6);
    }

    [Fact]
    public void BarAt_NoTrack_IsZero() {
        Assert.Equal(0, ColorPickerGeometry.BarAt(20, 10, 0));
    }

    [Theory]
    [InlineData(359, 1, false, 0)]
    [InlineData(0, -1, false, 359)]
    [InlineData(350, 1, true, 5)]
    [InlineData(10, -2, true, 340)]
    public void NudgeHue_WrapsPastRed(double hue, int presses, bool coarse, double expected) {
        Assert.Equal(expected, ColorPickerGeometry.NudgeHue(hue, presses, coarse), 6);
    }

    [Theory]
    [InlineData(0.5, 1, false, 0.51)]
    [InlineData(0.5, -1, true, 0.4)]
    [InlineData(0.995, 1, false, 1)]
    [InlineData(0.05, -1, true, 0)]
    public void NudgeUnit_StepsAndClamps(double value, int presses, bool coarse, double expected) {
        Assert.Equal(expected, ColorPickerGeometry.NudgeUnit(value, presses, coarse), 6);
    }
}
