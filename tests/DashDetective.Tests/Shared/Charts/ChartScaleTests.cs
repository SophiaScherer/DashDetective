using DashDetective.Shared.Charts;
using System;
using Xunit;

namespace DashDetective.Tests.Shared.Charts;

/// <summary>Covers <see cref="ChartScale"/>: peak across one/two windows, headroom, the floor clamp,
/// and empty/all-zero/all-negative windows.</summary>
public class ChartScaleTests {
    [Fact]
    public void Peak_EmptyWindows_ReturnsZero() {
        Assert.Equal(0, ChartScale.Peak(Array.Empty<double>()));
    }

    [Fact]
    public void Peak_AllZero_ReturnsZero() {
        Assert.Equal(0, ChartScale.Peak(new double[] { 0, 0, 0 }));
    }

    [Fact]
    public void Peak_AllNegative_ReturnsZero() {
        // Seeded at 0, so a window that never rises above zero peaks at 0 (never negative).
        Assert.Equal(0, ChartScale.Peak(new double[] { -5, -2, -9 }));
    }

    [Fact]
    public void Peak_SingleWindow_ReturnsLargest() {
        Assert.Equal(5, ChartScale.Peak(new double[] { 1, 5, 3 }));
    }

    [Fact]
    public void Peak_TwoWindows_ReturnsLargestAcrossBoth() {
        Assert.Equal(9, ChartScale.Peak(new double[] { 1, 5, 3 }, new double[] { 2, 9, 4 }));
    }

    [Fact]
    public void FitPeak_AppliesHeadroom() {
        Assert.Equal(115, ChartScale.FitPeak(100, 1.0), 6);
    }

    [Fact]
    public void FitPeak_ScaledBelowFloor_ReturnsFloor() {
        // 0.5 · 1.15 = 0.575 < 1.0 → clamped up to the floor.
        Assert.Equal(1.0, ChartScale.FitPeak(0.5, 1.0), 6);
    }

    [Fact]
    public void FitPeak_ScaledEqualsFloor_ReturnsFloor() {
        Assert.Equal(1.15, ChartScale.FitPeak(1.0, 1.15, 1.15), 6);
    }

    [Fact]
    public void FitAxis_EmptyWindow_ReturnsFloor() {
        Assert.Equal(1.0, ChartScale.FitAxis(Array.Empty<double>()), 6);
    }

    [Fact]
    public void FitAxis_UsesDefaultHeadroom() {
        Assert.Equal(11.5, ChartScale.FitAxis(new double[] { 10 }), 6);
    }

    [Fact]
    public void FitAxis_TwoWindows_FitsSharedPeak() {
        Assert.Equal(9.2, ChartScale.FitAxis(new double[] { 2 }, new double[] { 8 }), 6);
    }
}
