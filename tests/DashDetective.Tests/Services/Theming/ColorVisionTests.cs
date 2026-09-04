using Avalonia.Media;
using DashDetective.Services.Theming;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace DashDetective.Tests.Services.Theming;

/// <summary>
/// Checks that each mode's palette survives the deficiency it is chosen for, on the theme it is drawn on.
/// A palette picked by eye cannot be verified by looking at it on trichromatic vision — every hand-picked
/// assignment tried for this phase failed here.
/// </summary>
public class ColorVisionTests {
    private static string KindFor(ColorVisionMode mode) => mode switch {
        ColorVisionMode.Protanopia => "protan",
        ColorVisionMode.Tritanopia => "tritan",
        _ => "deutan",
    };

    /// <summary>Every mode on every theme: the tables differ by theme, so checking one proves nothing
    /// about the other.</summary>
    public static TheoryData<ColorVisionMode, bool> Cases() {
        var data = new TheoryData<ColorVisionMode, bool>();
        foreach (var mode in new[] {
                     ColorVisionMode.Deuteranopia, ColorVisionMode.Protanopia, ColorVisionMode.Tritanopia })
            foreach (var dark in new[] { true, false })
                data.Add(mode, dark);
        return data;
    }

    /// <summary>Good, warn and bad are read as a scale, and confusing any two misreports the machine's
    /// health. Info and idle join them because one row shows one of all five.</summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void Status_StaysDistinctUnderTheDeficiency(ColorVisionMode mode, bool dark) {
        var status = ColorVision.Status(mode, dark);

        AssertSeparated([
            ("Good", status.Good), ("Warn", status.Warn), ("Bad", status.Bad),
            ("Info", status.Info), ("Idle", status.Idle),
        ], mode, dark);
    }

    /// <summary>The per-metric series a reader matches against the legend and the stat cards.</summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void ChartSeries_StayDistinctUnderTheDeficiency(ColorVisionMode mode, bool dark) {
        var series = ColorVision.Series(mode, dark);
        Assert.NotNull(series);

        AssertSeparated([
            ("Cpu", series!.Cpu), ("Memory", series.Memory), ("Gpu", series.Gpu),
            ("Storage", series.Storage), ("Threads", series.Threads),
        ], mode, dark);
    }

    /// <summary>Download and upload share one axis and are drawn over each other, so they get their own
    /// check rather than relying on the set above.</summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void NetworkSeries_StayDistinctUnderTheDeficiency(ColorVisionMode mode, bool dark) {
        var series = ColorVision.Series(mode, dark)!;
        var separation = ColorVisionSimulator.SeparationUnder(series.NetDown, series.NetUp, KindFor(mode));

        Assert.True(separation >= ColorVisionSimulator.MinimumSeparation,
            $"{mode}/{Theme(dark)}: download and upload share an axis and come out {separation:F1} apart, " +
            $"below the {ColorVisionSimulator.MinimumSeparation} minimum.");
    }

    /// <summary>A colour has to be visible before its hue matters. This is what forced per-theme tables:
    /// one set cannot clear 3:1 on both backgrounds and still spread five categories apart.</summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void EveryColor_IsVisibleOnItsOwnTheme(ColorVisionMode mode, bool dark) {
        var background = dark ? (20, 20, 20) : (255, 255, 255);
        var status = ColorVision.Status(mode, dark);
        var series = ColorVision.Series(mode, dark)!;

        var all = new (string Name, Color Color)[] {
            ("Good", status.Good), ("Warn", status.Warn), ("Bad", status.Bad),
            ("Info", status.Info), ("Idle", status.Idle),
            ("Cpu", series.Cpu), ("Memory", series.Memory), ("Gpu", series.Gpu),
            ("Storage", series.Storage), ("Threads", series.Threads),
        };

        var dim = all
            .Select(c => (c.Name, Ratio: ContrastRatio.Of((c.Color.R, c.Color.G, c.Color.B), 1.0, background)))
            .Where(c => c.Ratio < 3.0)
            .Select(c => $"{c.Name} {c.Ratio:F2}:1")
            .ToArray();

        Assert.True(dim.Length == 0,
            $"{mode}/{Theme(dark)} draws these below 3:1 on its own background: {string.Join(", ", dim)}");
    }

    /// <summary>What the modes exist to fix, measured rather than asserted. Deuteranopia is severe — the
    /// authored green "good" and red "bad" land ~1.7 apart. Protanopia scrapes over the bar, since it
    /// darkens red enough to separate by lightness; it keeps a mode as that margin is inside simulation
    /// error.</summary>
    [Theory]
    [InlineData("deutan", 0.0, 5.0)]
    [InlineData("tritan", 15.0, ColorVisionSimulator.MinimumSeparation)]
    [InlineData("protan", ColorVisionSimulator.MinimumSeparation, 25.0)]
    public void AuthoredStatus_ScoresWhereTheModesWereChosenFor(string kind, double atLeast, double lessThan) {
        var authored = ColorVision.Authored;

        var worst = Pairs([
                ("Good", authored.Good), ("Warn", authored.Warn), ("Bad", authored.Bad),
                ("Info", authored.Info), ("Idle", authored.Idle),
            ])
            .Min(p => ColorVisionSimulator.SeparationUnder(p.A.Color, p.B.Color, kind));

        Assert.True(worst >= atLeast && worst < lessThan,
            $"The authored status colours now score {worst:F1} under {kind}, outside the measured " +
            $"[{atLeast}, {lessThan}) this phase was built on. The modes were chosen against those " +
            "numbers, so a change here is a reason to revisit them.");
    }

    /// <summary>None keeps the authored colours on both themes, which is what makes the setting genuinely
    /// switchable off.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void None_KeepsTheAuthoredColorsAndDefersToTheAccent(bool dark) {
        Assert.Equal(ColorVision.Authored, ColorVision.Status(ColorVisionMode.None, dark));
        Assert.Null(ColorVision.Series(ColorVisionMode.None, dark));
    }

    private static string Theme(bool dark) => dark ? "dark" : "light";

    private static void AssertSeparated(
        (string Name, Color Color)[] named, ColorVisionMode mode, bool dark) {
        var kind = KindFor(mode);
        var failures = new List<string>();

        foreach (var (a, b) in Pairs(named)) {
            var separation = ColorVisionSimulator.SeparationUnder(a.Color, b.Color, kind);
            if (separation < ColorVisionSimulator.MinimumSeparation)
                failures.Add($"{a.Name} vs {b.Name}: {separation:F1}");
        }

        Assert.True(failures.Count == 0,
            $"{mode}/{Theme(dark)} under {kind} leaves these closer than " +
            $"{ColorVisionSimulator.MinimumSeparation}: {string.Join(", ", failures)}");
    }

    private static IEnumerable<((string Name, Color Color) A, (string Name, Color Color) B)> Pairs(
        (string Name, Color Color)[] named) {
        for (var i = 0; i < named.Length; i++)
            for (var j = i + 1; j < named.Length; j++)
                yield return (named[i], named[j]);
    }
}
