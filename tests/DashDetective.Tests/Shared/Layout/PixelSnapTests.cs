using DashDetective.Shared.Layout;
using System;
using Xunit;

namespace DashDetective.Tests.Shared.Layout;

/// <summary>Covers <see cref="PixelSnap"/>: a snapped slot survives the framework's own layout rounding
/// without ending past its panel, both edges land on whole pixels, the child's minimum is kept, and a
/// panel that does not round is left alone.</summary>
public class PixelSnapTests {
    private const double Tolerance = 1e-6;

    /// <summary>What Avalonia's arrange does to a stretched child's box when layout rounding is on:
    /// clamp to the minimum, round both the slot and the clamped size up to whole pixels, center the
    /// difference, then round the origin to the nearest pixel (to even at a midpoint). Mirrored here so
    /// the rounding the fix has to survive is pinned beside it.</summary>
    private static (double Start, double End) FrameworkRounds(double start, double length, double scale,
                                                               double minimum = 0) {
        var slot = RoundUp(length, scale);
        var size = RoundUp(Math.Max(length, minimum), scale);
        var origin = Math.Round((start + (slot - size) / 2) * scale) / scale;
        return (origin, origin + size);
    }

    // The framework skips the 8-digit truncation at a scale of exactly 1.
    private static double RoundUp(double length, double scale) =>
        scale == 1
            ? Math.Ceiling(length)
            : Math.Ceiling(Math.Round(length, 8, MidpointRounding.ToZero) * scale) / scale;

    [Fact]
    public void ArrangeRounding_UnsnappedFractionalSlot_EndsPastThePanel() {
        // The Dashboard at 125%: a 1080.8 board split into two slots with a 16 gutter.
        var (_, end) = FrameworkRounds(548.4, 532.4, 1.25);

        Assert.True(end > 1080.8 + Tolerance);
    }

    [Fact]
    public void Span_FractionalSlot_StaysInsideThePanelAfterFrameworkRounding() {
        var (start, length) = PixelSnap.Span(548.4, 532.4, 0, 1080.8, 1.25);
        var (_, end) = FrameworkRounds(start, length, 1.25);

        Assert.True(end <= 1080.8 + Tolerance);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(1.1)]
    [InlineData(1.25)]
    [InlineData(1.5)]
    [InlineData(1.75)]
    [InlineData(1.875)]
    [InlineData(2.0)]
    [InlineData(2.25)]
    public void Span_EqualColumns_EverySlotStaysInsideItsPanelAndClearOfItsNeighbor(double scale) {
        const double spacing = 12;
        for (var pixels = 400; pixels <= 2400; pixels += 7) {
            var panel = pixels / scale;
            for (var columns = 2; columns <= 6; columns++) {
                var item = FlowLayout.ItemWidth(panel, columns, spacing);
                var previousEnd = double.NegativeInfinity;
                for (var c = 0; c < columns; c++) {
                    var (start, length) = PixelSnap.Span(c * (item + spacing), item, 0, panel, scale);
                    var (left, right) = FrameworkRounds(start, length, scale);

                    Assert.True(left >= -Tolerance, $"{pixels}px/{columns}: slot {c} starts before the panel");
                    Assert.True(right <= panel + Tolerance, $"{pixels}px/{columns}: slot {c} ends past the panel");
                    Assert.True(left >= previousEnd - Tolerance, $"{pixels}px/{columns}: slot {c} overlaps");
                    previousEnd = right;
                }
            }
        }
    }

    [Fact]
    public void Span_LandsBothEdgesOnWholePixels() {
        var (start, length) = PixelSnap.Span(10.3, 101.7, 0, double.PositiveInfinity, 1.25);

        Assert.Equal(Math.Round(start * 1.25), start * 1.25, 6);
        Assert.Equal(Math.Round((start + length) * 1.25), (start + length) * 1.25, 3);
    }

    [Fact]
    public void Span_MovedByWholePixels_KeepsItsLength() {
        // Rounding half to even would snap these to 8 and 10 pixels; half up keeps both at 9.
        var (_, first) = PixelSnap.Span(1.5, 9, 0, double.PositiveInfinity, 1);
        var (_, moved) = PixelSnap.Span(2.5, 9, 0, double.PositiveInfinity, 1);

        Assert.Equal(first, moved, 6);
    }

    [Fact]
    public void Span_SnappingUnderTheMinimum_GrowsToIt() {
        // 150 is 187.5px, which the arrange rounds up to 188; a slot starting on half a pixel would
        // snap to 187 and leave the framework to center the difference across both edges.
        var (start, length) = PixelSnap.Span(0.4, 150, 150, double.PositiveInfinity, 1.25);

        Assert.Equal(0.8, start, 6);
        Assert.Equal(188, length * 1.25, 3);
    }

    [Fact]
    public void Span_MinimumAgainstThePanelEdge_GrowsAwayFromIt() {
        // The same slot ending on the panel's edge takes its missing pixel from the gutter instead.
        const double panel = 680;
        var (start, length) = PixelSnap.Span(panel - 150, 150, 150, panel, 1.25);
        var (_, end) = FrameworkRounds(start, length, 1.25, minimum: 150);

        Assert.True(end <= panel + Tolerance);
        Assert.Equal(188, length * 1.25, 3);
    }

    [Theory]
    [InlineData(112.0 / 96, 420)]
    [InlineData(1.1, 420)]
    [InlineData(1.1, 440)]
    [InlineData(224.0 / 96, 420)]
    public void Span_MinimumAtAnInexactScale_StaysInsideThePanelAfterTheClamp(double scale, double minimum) {
        // 420 at 112 DPI is a hair over 490px, so the clamp makes it 491; a snap counting 490 ends a
        // pixel past the panel once the clamp centers the extra pixel.
        var panel = 1000 / scale;
        var (start, length) = PixelSnap.Span(panel - minimum, minimum, minimum, panel, scale);
        var (_, end) = FrameworkRounds(start, length, scale, minimum);

        Assert.True(end <= panel + Tolerance);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    public void Span_NoLayoutRounding_LeavesTheSlotAsComputed(double scale) {
        Assert.Equal((548.4, 532.4), PixelSnap.Span(548.4, 532.4, 0, 1080.8, scale));
    }

    [Fact]
    public void Span_UnboundedLength_IsLeftAlone() {
        Assert.Equal((0.0, double.PositiveInfinity),
                     PixelSnap.Span(0, double.PositiveInfinity, 0, double.PositiveInfinity, 1.25));
    }
}
