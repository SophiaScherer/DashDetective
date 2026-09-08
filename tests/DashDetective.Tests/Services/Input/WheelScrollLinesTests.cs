using DashDetective.Services.Input;
using DashDetective.Shared.Controls;
using System;
using Xunit;

namespace DashDetective.Tests.Services.Input;

/// <summary>
/// Covers the <see cref="IWheelScrollLines"/> seam: the reader each platform resolves to, the mapping off
/// the Win32 value, and the no-data contract elsewhere.
/// </summary>
public class WheelScrollLinesTests {
    [Fact]
    public void ForCurrentPlatform_ResolvesTheReaderForThisHost() {
        var lines = IWheelScrollLines.ForCurrentPlatform();

        if (OperatingSystem.IsWindows())
            Assert.IsType<WindowsWheelScrollLines>(lines);
        else
            Assert.IsType<UnsupportedWheelScrollLines>(lines);
    }

    [Theory]
    [InlineData(3u, 3)]
    [InlineData(1u, 1)]
    [InlineData(0u, 0)]
    public void Interpret_KeepsALineCountAsItIs(uint raw, int expected) =>
        Assert.Equal(expected, WindowsWheelScrollLines.Interpret(raw));

    /// <summary>WHEEL_PAGESCROLL arrives as the whole uint range, and would otherwise be four billion
    /// lines. Pinned to <see cref="WheelStep"/>'s marker so the two cannot drift apart.</summary>
    [Fact]
    public void Interpret_PageScroll_BecomesThePageMarker() =>
        Assert.Equal(WheelStep.PageLines, WindowsWheelScrollLines.Interpret(uint.MaxValue));

    [Fact]
    public void Unsupported_ReportsNoSetting() =>
        Assert.Null(new UnsupportedWheelScrollLines().PerNotch());
}
