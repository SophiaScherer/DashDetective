using DashDetective.Tests.Services.Theming;
using System.IO;
using Xunit;

namespace DashDetective.Tests.Shared.Controls;

/// <summary>Pins that the completion suffix follows the ghost brush after a theme switch, by source read since the control needs a render backend.</summary>
public class GhostCompletionBoxTests {
    private static string Source() =>
        File.ReadAllText(Path.Combine(PaletteFile.SourceRoot(), "src/Shared/Controls/GhostCompletionBox.axaml.cs"));

    [Fact]
    public void SuffixRun_ReappliesTheGhostBrushWhenItChanges() {
        var source = Source();

        Assert.Contains("Ghost.GetObservable(TextBlock.ForegroundProperty)", source);
        Assert.Contains("run.Foreground = brush;", source);
    }
}
