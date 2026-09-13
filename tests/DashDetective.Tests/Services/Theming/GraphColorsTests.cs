using DashDetective.Services.Theming;
using System.Linq;
using Xunit;

namespace DashDetective.Tests.Services.Theming;

/// <summary>Covers <see cref="GraphColors"/>: the persisted name resolves back to its choice, and Blue
/// agrees with the Default palette the app starts in.</summary>
public class GraphColorsTests {
    [Fact]
    public void All_NamesAreUnique() {
        var names = GraphColors.All.Select(c => c.Name).ToList();

        Assert.Equal(names.Count, names.Distinct().Count());
    }

    [Fact]
    public void Find_EveryName_RoundTrips() {
        foreach (var colors in GraphColors.All)
            Assert.Same(colors, GraphColors.Find(colors.Name));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Teal")]
    public void Find_EmptyOrUnknown_IsDefault(string? name) {
        Assert.Null(GraphColors.Find(name));
    }

    /// <summary>Picking Blue must not visibly differ from the Default look.</summary>
    [Fact]
    public void Series_Blue_ReproducesTheAuthoredPalette() {
        Assert.Equal(ChartPalette.Default, GraphColors.Find("Blue")!.Series);
    }
}
