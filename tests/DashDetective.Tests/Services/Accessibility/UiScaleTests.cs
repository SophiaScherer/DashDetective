using DashDetective.Services.Accessibility;
using System;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using Xunit;

namespace DashDetective.Tests.Services.Accessibility;

/// <summary>
/// Covers <see cref="UiScale"/>: the popup type size that follows the interface scale where a
/// <c>ScaleHost</c> cannot reach. The range and its arithmetic belong to <see cref="ScaleRange"/>.
/// </summary>
public class UiScaleTests {
    [Fact]
    public void PopupFontSize_ScalesTheBase() {
        Assert.Equal(UiScale.BasePopupFontSize, UiScale.PopupFontSize(100));
        Assert.Equal(UiScale.BasePopupFontSize * 2, UiScale.PopupFontSize(200));
    }

    /// <summary>It follows the shared range too, rather than clamping on its own terms.</summary>
    [Fact]
    public void PopupFontSize_OutOfRange_FollowsTheRangeEnds() {
        Assert.Equal(UiScale.BasePopupFontSize * 0.8, UiScale.PopupFontSize(0));
        Assert.Equal(UiScale.BasePopupFontSize * 2, UiScale.PopupFontSize(10_000));
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
