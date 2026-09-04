using DashDetective.Services.Accessibility;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Xunit;

namespace DashDetective.Tests.Services.Accessibility;

/// <summary>Covers the text ladder: the arithmetic, and the two ways it can silently rot — a token
/// whose default drifts from Dimensions.axaml, and a view that goes back to an authored literal.</summary>
public class TextScaleTests {
    [Theory]
    [InlineData(100, 1.0)]
    [InlineData(150, 1.5)]
    [InlineData(200, 2.0)]
    public void Factor_IsThePercentage(int percent, double expected) =>
        Assert.Equal(expected, TextScale.Factor(percent));

    [Theory]
    [InlineData(0, 100)]
    [InlineData(500, 200)]
    public void Nearest_SnapsOntoTheLadder(int stored, int expected) =>
        Assert.Equal(expected, TextScale.Nearest(stored));

    [Fact]
    public void Sizes_ScaleEveryStepTogether() {
        var doubled = TextScale.Sizes(200);

        Assert.Equal(TextScale.BaseSizes.Count, doubled.Count);
        foreach (var (key, size) in TextScale.BaseSizes)
            Assert.Equal(size * 2, doubled[key]);
    }

    /// <summary>100% has to be the app exactly as it shipped — that is the rule every option on the
    /// Accessibility card follows, and the one this sweep could most easily break.</summary>
    [Fact]
    public void Sizes_AtOneHundredAreTheAuthoredSizes() =>
        Assert.Equal(TextScale.BaseSizes, TextScale.Sizes(100));

    /// <summary>The table is a C# mirror of Dimensions.axaml's ladder, as <c>UiScale</c> mirrors the
    /// popup size. Nothing else would catch a token's default drifting from the one it scales.</summary>
    [Fact]
    public void BaseSizes_MatchTheDimensionsDefaults() {
        var dimensions = File.ReadAllText(Dimensions());

        foreach (var (key, size) in TextScale.BaseSizes) {
            // Invariant: a comma decimal separator would look for "12,5" and never find it.
            var authored = size.ToString(CultureInfo.InvariantCulture);
            Assert.Contains($"<sys:Double x:Key=\"{key}\">{authored}</sys:Double>",
                            dimensions, StringComparison.Ordinal);
        }
    }

    /// <summary>And the other direction, so a token added to the XAML alone cannot go unscaled.</summary>
    [Fact]
    public void DimensionsDeclaresNoTokenTheLadderDoesNotScale() {
        var declared = Regex.Matches(File.ReadAllText(Dimensions()),
                                     @"x:Key=""(TextSize\w+)""")
                            .Select(m => m.Groups[1].Value);

        foreach (var key in declared)
            Assert.True(TextScale.BaseSizes.ContainsKey(key),
                        $"Dimensions.axaml declares {key}, which TextScale does not scale.");
    }

    /// <summary>Text scale is the one feature that IS a sweep: a size left as a literal simply would not
    /// grow, and would do it invisibly on one page. This is what keeps the sweep swept.</summary>
    [Fact]
    public void NoViewAuthorsAFontSizeLiteral() {
        var offenders = new List<string>();
        var literal = new Regex(@"FontSize=""[0-9]|Property=""FontSize"" Value=""[0-9]");

        foreach (var file in Directory.EnumerateFiles(Source(), "*.axaml", SearchOption.AllDirectories)) {
            var text = File.ReadAllText(file);
            if (literal.IsMatch(text))
                offenders.Add(Path.GetFileName(file));
        }

        Assert.Empty(offenders);
    }

    private static string Dimensions() => Path.Combine(Source(), "Shared/Styles/Dimensions.axaml");

    /// <summary>Walks up to the repository from this file's own compile-time path, as
    /// <c>UiScaleTests</c> does: anchoring to the binaries breaks under <c>--artifacts-path</c>.</summary>
    private static string Source([CallerFilePath] string thisFile = "") {
        var dir = new DirectoryInfo(Path.GetDirectoryName(thisFile)!);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "DashDetective.sln")))
            dir = dir.Parent;

        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "DashDetective", "src");
    }
}
