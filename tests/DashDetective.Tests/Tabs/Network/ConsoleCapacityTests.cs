using DashDetective.Tabs.Network;
using Xunit;

namespace DashDetective.Tests.Tabs.Network;

/// <summary>Covers <see cref="ConsoleCapacity"/>: how much scrollback a console box of a given height
/// has room for, and that a box too small for any still reports a readable minimum.</summary>
public class ConsoleCapacityTests {
    private const double LineHeight = 15;
    private const double Reserved = 38;   // 10px padding each side, plus the footer line and its gap
    private const int Min = 3;
    private const int Max = 24;

    private static int Lines(double height) =>
        ConsoleCapacity.LinesForHeight(height, LineHeight, Reserved, Min, Max);

    [Fact]
    public void ExactFit_CountsEveryWholeLine() {
        // 38 reserved + 6 × 15 = 128.
        Assert.Equal(6, Lines(128));
    }

    [Fact]
    public void PartialLine_IsNotCounted() {
        Assert.Equal(6, Lines(142));   // 104 usable: six lines and change
        Assert.Equal(7, Lines(143));   // 105 usable: exactly seven
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-50)]
    [InlineData(38)]      // reserved exactly, nothing left
    [InlineData(60)]      // room for one line, still floored at the minimum
    public void TooShort_FallsBackToTheMinimum(double height) {
        Assert.Equal(Min, Lines(height));
    }

    [Fact]
    public void VeryTall_ClampsToTheMaximum() {
        Assert.Equal(Max, Lines(5000));
    }

    [Fact]
    public void InfiniteHeight_IsTheMinimum() {
        // An unconstrained measure pass has no height to divide, so it must not report a huge capacity.
        Assert.Equal(Min, Lines(double.PositiveInfinity));
    }

    [Fact]
    public void ZeroLineHeight_IsTheMinimum() {
        Assert.Equal(Min, ConsoleCapacity.LinesForHeight(500, 0, Reserved, Min, Max));
    }

    [Fact]
    public void MaxBelowMin_StillReportsTheMinimum() {
        Assert.Equal(5, ConsoleCapacity.LinesForHeight(1000, LineHeight, Reserved, 5, 2));
    }

    [Fact]
    public void PingMonitor_ClampsTheLineCountItIsGiven() {
        using var monitor = new PingMonitor();
        Assert.Equal(PingMonitor.MinLines, monitor.LineCount);

        monitor.LineCount = 14;
        Assert.Equal(14, monitor.LineCount);

        monitor.LineCount = 500;
        Assert.Equal(PingMonitor.MaxLines, monitor.LineCount);

        monitor.LineCount = 0;
        Assert.Equal(PingMonitor.MinLines, monitor.LineCount);
    }

    [Fact]
    public void GrowsMonotonicallyWithHeight() {
        var previous = 0;
        for (var height = 0.0; height <= 600; height += 5) {
            var lines = Lines(height);
            Assert.True(lines >= previous, $"capacity fell from {previous} to {lines} at {height}");
            previous = lines;
        }
    }
}
