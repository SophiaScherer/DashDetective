using Avalonia;
using DashDetective.Shared.Layout;
using Xunit;

namespace DashDetective.Tests.Shared.Layout;

/// <summary>Covers <see cref="DragDropHint.Map"/>: a box inside the scale host lands in the overlay scaled, and is unchanged at 100 %.</summary>
public class DragDropHintTests {
    [Fact]
    public void Map_InterfaceSizeOneHundredFifty_ScalesSizeAsWellAsOrigin() {
        var toOverlay = Matrix.CreateScale(1.5, 1.5) * Matrix.CreateTranslation(20, 10);

        var (box, _) = DragDropHint.Map(new Rect(10, 20, 100, 50), toOverlay, 4);

        Assert.Equal(new Rect(35, 40, 150, 75), box);
    }

    [Fact]
    public void Map_InterfaceSizeOneHundredFifty_ScalesTheCornerRadius() {
        var (_, radius) = DragDropHint.Map(new Rect(0, 0, 100, 50), Matrix.CreateScale(1.5, 1.5), 4);

        Assert.Equal(new CornerRadius(6), radius);
    }

    [Fact]
    public void Map_TranslationOnly_KeepsSizeAndRadius() {
        var (box, radius) = DragDropHint.Map(new Rect(10, 20, 100, 50), Matrix.CreateTranslation(5, 7), 4);

        Assert.Equal(new Rect(15, 27, 100, 50), box);
        Assert.Equal(new CornerRadius(4), radius);
    }
}
