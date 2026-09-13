using DashDetective.Services.Theming;
using System.Linq;
using Xunit;

namespace DashDetective.Tests.Services.Theming;

/// <summary>Covers <see cref="ThemeService"/>'s graph colors: they drive the chart series only, Default
/// restores the authored palette, and a color-vision mode still wins.</summary>
public class ThemeServiceTests {
    private static GraphColors Named(string name) => GraphColors.All.First(c => c.Name == name);

    [Fact]
    public void ApplyGraphColors_SetsTheDerivedSeries() {
        var theme = new ThemeService();
        var green = Named("Green");

        theme.ApplyGraphColors(green);

        Assert.Same(green, theme.CurrentGraphColors);
        Assert.Equal(green.Series, theme.CurrentSeries);
    }

    [Fact]
    public void ApplyGraphColors_Null_RestoresTheDefaultPalette() {
        var theme = new ThemeService();
        theme.ApplyGraphColors(Named("Orange"));

        theme.ApplyGraphColors(null);

        Assert.Null(theme.CurrentGraphColors);
        Assert.Equal(ChartPalette.Default, theme.CurrentSeries);
    }

    [Fact]
    public void ApplyGraphColors_RaisesSeriesChanged() {
        var theme = new ThemeService();
        ChartSeriesColors? raised = null;
        theme.SeriesChanged += series => raised = series;

        theme.ApplyGraphColors(Named("Purple"));

        Assert.Equal(Named("Purple").Series, raised);
    }

    /// <summary>Rotating a color-blind-safe table by a hue offset would undo it, so the mode wins either
    /// way round.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ColorVisionMode_OverridesGraphColors(bool modeFirst) {
        var theme = new ThemeService();
        const ColorVisionMode mode = ColorVisionMode.Deuteranopia;

        try {
            if (modeFirst) {
                theme.ApplyColorVision(mode);
                theme.ApplyGraphColors(Named("Green"));
            } else {
                theme.ApplyGraphColors(Named("Green"));
                theme.ApplyColorVision(mode);
            }

            Assert.Equal(ColorVision.Series(mode, theme.RendersDark), theme.CurrentSeries);
        } finally {
            // SemanticBrushes is process-wide, so the mode must not leak into other tests.
            theme.ApplyColorVision(ColorVisionMode.None);
        }
    }

    [Fact]
    public void Accent_DefaultsToBlue() {
        Assert.Same(AccentPreset.Default, new ThemeService().CurrentAccent);
    }

    /// <summary>The split AB#83 made: an accent never recolors a chart.</summary>
    [Fact]
    public void ApplyAccent_LeavesTheSeriesAlone() {
        var theme = new ThemeService();
        theme.ApplyGraphColors(Named("Green"));
        var raised = false;
        theme.SeriesChanged += _ => raised = true;
        var custom = AccentPreset.FromHex("#1a3a8a");

        theme.ApplyAccent(custom);

        Assert.Same(custom, theme.CurrentAccent);
        Assert.Equal(Named("Green").Series, theme.CurrentSeries);
        Assert.False(raised);
    }

    [Fact]
    public void ApplyGraphColors_LeavesTheAccentAlone() {
        var theme = new ThemeService();
        var custom = AccentPreset.FromHex("#1a3a8a");
        theme.ApplyAccent(custom);

        theme.ApplyGraphColors(Named("Orange"));

        Assert.Same(custom, theme.CurrentAccent);
    }

    [Fact]
    public void ColorVisionOff_ReturnsToTheGraphColors() {
        var theme = new ThemeService();
        theme.ApplyGraphColors(Named("Green"));
        theme.ApplyColorVision(ColorVisionMode.Tritanopia);

        theme.ApplyColorVision(ColorVisionMode.None);

        Assert.Equal(Named("Green").Series, theme.CurrentSeries);
    }
}
