using DashDetective.Services.Accessibility;
using System;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using Xunit;

namespace DashDetective.Tests.Services.Accessibility;

/// <summary>
/// Covers <see cref="UiScale"/>: the ladder the Accessibility card offers, the clamp that keeps a
/// hand-edited settings file from collapsing the window, and the popup type size that follows the
/// scale where a <c>ScaleHost</c> cannot reach.
/// </summary>
public class UiScaleTests {
    [Fact]
    public void Percents_StartAtTheDefaultAndAscend() {
        Assert.Equal(UiScale.DefaultPercent, UiScale.Percents[0]);

        for (var i = 1; i < UiScale.Percents.Count; i++)
            Assert.True(UiScale.Percents[i] > UiScale.Percents[i - 1]);
    }

    [Theory]
    [InlineData(100, 1.0)]
    [InlineData(150, 1.5)]
    [InlineData(200, 2.0)]
    public void Factor_ConvertsAPercentage(int percent, double expected) =>
        Assert.Equal(expected, UiScale.Factor(percent));

    /// <summary>The settings file is editable by hand, and 0 or a negative would collapse the window to
    /// nothing rather than degrade — so the clamp is load-bearing, not defensive decoration.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-50)]
    [InlineData(40)]
    public void Factor_BelowTheLadder_PinsToTheSmallestStep(int percent) =>
        Assert.Equal(UiScale.Percents[0] / 100.0, UiScale.Factor(percent));

    [Fact]
    public void Factor_AboveTheLadder_PinsToTheLargestStep() =>
        Assert.Equal(UiScale.Percents[^1] / 100.0, UiScale.Factor(10_000));

    [Fact]
    public void Nearest_AKnownStep_ReturnsItUnchanged() {
        foreach (var step in UiScale.Percents)
            Assert.Equal(step, UiScale.Nearest(step));
    }

    /// <summary>An unrecognized value has to select a segment: leaving the control blank would offer no
    /// way back to a known scale except by guessing.</summary>
    [Theory]
    [InlineData(130, 125)]
    [InlineData(160, 150)]
    [InlineData(0, 100)]
    [InlineData(500, 200)]
    public void Nearest_SnapsOntoTheLadder(int stored, int expected) =>
        Assert.Equal(expected, UiScale.Nearest(stored));

    [Fact]
    public void PopupFontSize_ScalesTheBase() {
        Assert.Equal(UiScale.BasePopupFontSize, UiScale.PopupFontSize(100));
        Assert.Equal(UiScale.BasePopupFontSize * 2, UiScale.PopupFontSize(200));
    }

    /// <summary>The base is a C# mirror of Dimensions.axaml's <c>PopupFontSize</c> default, the way
    /// SemanticBrushes mirrors Palette.axaml. Nothing else would catch the two drifting apart.</summary>
    [Fact]
    public void BasePopupFontSize_MatchesTheDimensionsDefault() {
        var dimensions = File.ReadAllText(Path.Combine(SourceRoot(), "src/Shared/Styles/Dimensions.axaml"));

        // Invariant: a comma decimal separator would look for "12,5" and never find it.
        var authored = UiScale.BasePopupFontSize.ToString(CultureInfo.InvariantCulture);

        Assert.Contains($"<sys:Double x:Key=\"PopupFontSize\">{authored}</sys:Double>",
                        dimensions, StringComparison.Ordinal);
    }

    /// <summary>Walks up to the repository from this file's own compile-time path, as
    /// <c>PaletteOwnershipTests</c> does: anchoring to the binaries breaks under
    /// <c>--artifacts-path</c>.</summary>
    private static string SourceRoot([CallerFilePath] string thisFile = "") {
        var dir = new DirectoryInfo(Path.GetDirectoryName(thisFile)!);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "DashDetective.sln")))
            dir = dir.Parent;

        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "DashDetective");
    }
}
