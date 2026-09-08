using DashDetective.Shared.Controls;
using Xunit;

namespace DashDetective.Tests.Shared.Controls;

/// <summary>
/// Covers <see cref="WheelStep"/>: how far one wheel notch moves a surface. The toolkit's own answer is a
/// fixed 50 px, so this arithmetic is the whole of the fix.
/// </summary>
public class WheelStepTests {
    [Fact]
    public void Distance_MultipliesTheLinesTheOsAsksFor() =>
        Assert.Equal(90, WheelStep.Distance(3, viewport: 700, unit: 30));

    /// <summary>An unreadable setting lands where a default Windows box would, not on a single line.</summary>
    [Fact]
    public void Distance_UnknownSetting_UsesTheWindowsDefault() =>
        Assert.Equal(WheelStep.DefaultLines * 30, WheelStep.Distance(null, viewport: 700, unit: 30));

    /// <summary>The OS's "one screen at a time", less an overlap so the reader keeps their place.</summary>
    [Fact]
    public void Distance_PageSetting_MovesAViewportLessAnOverlap() =>
        Assert.Equal(670, WheelStep.Distance(WheelStep.PageLines, viewport: 700, unit: 30));

    /// <summary>A viewport shorter than the overlap would otherwise scroll backwards, or not at all.</summary>
    [Fact]
    public void Distance_PageSettingOnATinyViewport_StillMovesALine() =>
        Assert.Equal(30, WheelStep.Distance(WheelStep.PageLines, viewport: 20, unit: 30));

    /// <summary>0 is the Windows API's "do not scroll", and is honored rather than corrected.</summary>
    [Fact]
    public void Distance_ZeroLines_DoesNotScroll() =>
        Assert.Equal(0, WheelStep.Distance(0, viewport: 700, unit: 30));

    /// <summary>No other negative means anything to the API, and a frozen wheel looks like a broken app.</summary>
    [Theory]
    [InlineData(-2)]
    [InlineData(-1000)]
    public void Distance_MeaninglessNegative_FallsBackToTheDefault(int lines) =>
        Assert.Equal(WheelStep.DefaultLines * 30, WheelStep.Distance(lines, viewport: 700, unit: 30));

    /// <summary>The unit is the surface's own, so a by-item surface moves three items.</summary>
    [Fact]
    public void Distance_ByItemUnits_CountsItems() =>
        Assert.Equal(3, WheelStep.Distance(3, viewport: 20, unit: 1));
}
