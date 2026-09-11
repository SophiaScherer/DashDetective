using Avalonia.Media;
using DashDetective.Services.Theming;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace DashDetective.Tests.Services.Theming;

/// <summary>
/// Pins the shape of the light text ramp, which <c>PaletteContrastTests</c> cannot: that one measures
/// contrast, and a ramp could clear AA on every rung while having collapsed into three indistinguishable
/// pairs. The light ramp is spaced on CIE L* for exactly that reason — equal opacity steps are not equal
/// perceptual steps, and the ramp built from them had nowhere to lift its bottom two rungs into.
/// </summary>
public class PaletteRampTests {
    /// <summary>The six rungs used for text a user has to read, brightest first.</summary>
    private static readonly string[] BodyRamp =
        ["TextStrong", "TextPrimary", "TextSecondary", "TextTertiary", "TextMuted", "TextSubtle"];

    /// <summary>The surface a rung is composited over to weigh it. Light's rungs are black at an opacity,
    /// so their weight only means anything once composited.</summary>
    private static Color Surface(string variant) =>
        variant == "Light" ? Color.FromRgb(255, 255, 255) : Color.FromRgb(20, 20, 20);

    /// <summary>How far apart two neighbouring rungs have to sit to read as different weights. Below
    /// about 4 L* the step stops being visible on a short run of small text, which is where the old
    /// ramp's bottom end had ended up.</summary>
    private const double MinStep = 4.0;

    [Fact]
    public void Light_RampStepsAreEvenlySpacedInLightness() {
        var steps = Steps("Light");

        Assert.All(steps, step => Assert.InRange(step, 8.0, 9.6));
    }

    /// <summary>Evenness is the design; separation is the requirement. Asserted separately so a future
    /// ramp that trades one for the other says which it broke.</summary>
    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void EveryNeighbouringPair_IsFarEnoughApartToTellApart(string variant) {
        var tooClose = Weights(variant)
            .Zip(Weights(variant).Skip(1), (above, below) => (above, below))
            .Zip(BodyRamp.Zip(BodyRamp.Skip(1)), (pair, names) => (names, gap: Math.Abs(pair.above - pair.below)))
            .Where(entry => entry.gap < MinStep)
            .Select(entry => $"{entry.names.First} and {entry.names.Second} are {entry.gap:F1} L* apart")
            .ToList();

        Assert.True(tooClose.Count == 0,
            $"The {variant} ramp has rungs that read as one weight:" +
            Environment.NewLine + string.Join(Environment.NewLine, tooClose));
    }

    /// <summary>A ramp has to actually descend. A transposed pair would still pass the spacing checks
    /// above, and would put the emphatic rung below the muted one.</summary>
    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void TheRampIsMonotonic(string variant) {
        var weights = Weights(variant);

        // Light draws black on white, so its rungs climb in L* as emphasis falls; dark is the reverse.
        var descending = variant == "Light"
            ? weights.Zip(weights.Skip(1), (a, b) => b > a)
            : weights.Zip(weights.Skip(1), (a, b) => b < a);

        Assert.All(descending, Assert.True);
    }

    /// <summary>Each rung's composited CIE L*, brightest-emphasis first.</summary>
    private static List<double> Weights(string variant) {
        var surface = Surface(variant);
        return BodyRamp.Select(key => {
            var brush = PaletteFile.Resolve(variant, key);
            var color = Color.FromRgb((byte)brush.Color.R, (byte)brush.Color.G, (byte)brush.Color.B);
            return Tone.CompositedLightness(color, brush.Opacity, surface);
        }).ToList();
    }

    private static List<double> Steps(string variant) {
        var weights = Weights(variant);
        return weights.Zip(weights.Skip(1), (above, below) => Math.Abs(below - above)).ToList();
    }
}
