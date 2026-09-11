using Avalonia.Media;
using DashDetective.Services.Theming;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace DashDetective.Tests.Services.Theming;

/// <summary>
/// The lines and tracks, which are graphics rather than text and so answer to WCAG's 3:1 rather than
/// 4.5:1 — and only where they carry meaning. The split is the point: a toggle's off track is how an off
/// switch is told from an on one, while a rule between two rows repeats what the spacing already says.
/// Both halves are asserted, so lifting a separator to 3:1 is a decision somebody makes on purpose.
/// </summary>
public class PaletteLineTests {
    /// <summary>The surfaces a line is drawn on.</summary>
    private static readonly string[] Surfaces =
        ["AppBackground", "SidebarBackground", "PanelBackground", "CardBackground", "FieldBackground"];

    /// <summary>WCAG 1.4.11: a graphic that carries information, which a control's state does.</summary>
    private const double NonText = 3.0;

    /// <summary>The floor for a separator on the light theme. Below the non-text bar on purpose — these
    /// repeat what the surfaces and the spacing already say — but far enough above the old 1.19 that a
    /// card edge is visible on white.</summary>
    private const double SeparatorFloor = 1.35;

    /// <summary>The one line token that encodes a state. Its light value was lifted to the bar; dark is
    /// left where it was, since this item is scoped to the light theme.</summary>
    [Fact]
    public void Light_ToggleTrack_MeetsTheNonTextBar() {
        var worst = Surfaces.Min(surface => Ratio("Light", "TrackOff", surface));

        Assert.True(worst >= NonText, $"The off track reads {worst:F2}:1, under {NonText}:1.");
    }

    /// <summary>
    /// The separators and the chart lattice stay below the non-text bar, deliberately and in both themes.
    /// Each is structure a reader already has by other means — panels are separated by their own surfaces
    /// and spacing, table rows by their alternating bands, and the chart grid is a scale whose values are
    /// on the axis labels beside it. At 3:1 each would read as a rule rather than a hairline, and the grid
    /// would outshout the trace drawn over it — which is the same reason the high-contrast tables leave
    /// ChartGrid alone.
    ///
    /// The ceiling half of the pair: <see cref="Light_Separators_ClearTheSeparatorFloor"/> is the floor.
    /// </summary>
    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void StructuralLines_AreBelowTheNonTextBar_ByDesign(string variant) {
        var above = new List<string>();

        foreach (var key in new[] { "Hairline", "RowLine", "ChartGrid" })
            foreach (var surface in Surfaces) {
                var ratio = Ratio(variant, key, surface);
                if (ratio >= NonText)
                    above.Add($"{key} on {surface}: {ratio:F2}:1");
            }

        Assert.True(above.Count == 0,
            "These are recorded as deliberately faint. Lifting one is a design decision — update this " +
            "test and the note on it rather than deleting the assertion:" +
            Environment.NewLine + string.Join(Environment.NewLine, above));
    }

    /// <summary>The light separators were lifted to be genuinely visible on white — the old 1.19 left a
    /// card edge all but invisible — without crossing into rule territory.</summary>
    [Theory]
    [InlineData("Hairline")]
    [InlineData("RowLine")]
    public void Light_Separators_ClearTheSeparatorFloor(string key) {
        var worst = Surfaces.Min(surface => Ratio("Light", key, surface));

        Assert.True(worst >= SeparatorFloor, $"{key} reads {worst:F2}:1, under {SeparatorFloor}:1.");
    }

    /// <summary>Light is not the weaker theme for a structural line. If a future change makes it so, that
    /// is the regression this item was raised about.</summary>
    [Theory]
    [InlineData("Hairline")]
    [InlineData("RowLine")]
    [InlineData("ChartGrid")]
    public void Light_IsNeverFainterThanDark_ForAStructuralLine(string key) {
        var light = Surfaces.Min(surface => Ratio("Light", key, surface));
        var dark = Surfaces.Min(surface => Ratio("Dark", key, surface));

        Assert.True(light >= dark, $"{key} fell behind dark in light: {light:F2}:1 against {dark:F2}:1.");
    }

    /// <summary>High contrast draws its structure with lines, so those do have to clear the bar. The
    /// chart grid is the authored exception and is excluded here for the reason its own note gives.</summary>
    [Theory]
    [InlineData("HighContrastLight")]
    [InlineData("HighContrastDark")]
    public void HighContrast_LinesCarryTheStructure(string variant) {
        var failures = new List<string>();

        foreach (var key in new[] { "Hairline", "RowLine", "TrackOff" })
            foreach (var surface in Surfaces) {
                var ratio = Ratio(variant, key, surface);
                if (ratio < NonText)
                    failures.Add($"{key} on {surface}: {ratio:F2}:1");
            }

        Assert.True(failures.Count == 0,
            $"{variant} carries structure on its lines:" +
            Environment.NewLine + string.Join(Environment.NewLine, failures));
    }

    // ----- The traces, which are the chart's actual data -----

    /// <summary>A trace is a graphic that carries the content, so it answers to the same 3:1 — which the
    /// authored series miss badly on white, storage worst at 1.47:1.</summary>
    [Fact]
    public void LightTraces_MeetTheNonTextBarOnWhite() {
        var traces = ChartPalette.TraceShades(ChartPalette.Default);
        var failures = new List<string>();

        foreach (var series in Enum.GetValues<ChartSeries>()) {
            var color = traces.For(series);
            var ratio = ContrastRatio.Of((color.R, color.G, color.B), 1.0, (255, 255, 255));
            if (ratio < NonText)
                failures.Add($"{series}: {ratio:F2}:1");
        }

        Assert.True(failures.Count == 0, string.Join(", ", failures));
    }

    /// <summary>A trace sits under the figure that names it, so it is the lighter of the two — the text
    /// rung is for something read, the trace rung for something seen. Asserted on the rungs themselves as
    /// well as on the shipped palette: every authored series is lighter than both rungs today, so the
    /// per-series check alone would still pass if the two rungs were swapped.</summary>
    [Fact]
    public void TheTraceRung_IsLighterThanTheTextRung() {
        Assert.True(Tone.LightGraphic > Tone.LightText,
            $"A trace ({Tone.LightGraphic} L*) has to sit lighter than a figure ({Tone.LightText} L*).");
    }

    [Fact]
    public void ALightTrace_IsLighterThanTheSameSeriesAsText() {
        var traces = ChartPalette.TraceShades(ChartPalette.Default);
        var text = ChartPalette.TextShades(ChartPalette.Default);

        foreach (var series in Enum.GetValues<ChartSeries>())
            Assert.True(Tone.Lightness(traces.For(series)) > Tone.Lightness(text.For(series)),
                $"{series}'s trace is not lighter than its figure.");
    }

    /// <summary>The darken-only rule is what protects the searched colour-vision tables, so it is pinned
    /// against a colour below both rungs rather than only against the authored palette, every member of
    /// which is lighter than both and so never exercises the guard.</summary>
    [Fact]
    public void AColourAlreadyDarkerThanBothRungs_IsLeftWhereItIs() {
        var deep = Color.Parse("#003F62"); // The deuteranopia light table's CPU, at 25.1 L*.

        Assert.Equal(deep, Tone.GraphicOnLight(deep));
        Assert.Equal(deep, Tone.TextOnLight(deep));
    }

    /// <summary>Re-hueing is what the palette is for; a trace shade must not undo it.</summary>
    [Fact]
    public void TraceShades_KeepEverySeriesOnItsOwnHue() {
        var traces = ChartPalette.TraceShades(ChartPalette.Default);
        var hues = Enum.GetValues<ChartSeries>()
            .Where(series => series is not ChartSeries.NetDown) // Deliberately shares CPU's colour.
            .Select(series => Math.Round(traces.For(series).ToHsl().H))
            .ToList();

        Assert.Equal(hues.Count, hues.Distinct().Count());
    }

    private static double Ratio(string variant, string lineKey, string surfaceKey) {
        var line = PaletteFile.Resolve(variant, lineKey);
        var surface = PaletteFile.Resolve(variant, surfaceKey);

        return ContrastRatio.Of(line.Color, line.Opacity, surface.Color);
    }
}
