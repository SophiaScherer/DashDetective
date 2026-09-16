using DashDetective.Shared.Controls;
using Xunit;

namespace DashDetective.Tests.Shared.Controls;

/// <summary>
/// Covers <see cref="FieldWidth"/>, the rule stated on <see cref="SearchField"/>. Min/max bounds are
/// Avalonia's to apply on either side of this, so nothing here asserts a measured size.
/// </summary>
public class FieldWidthTests {
    /// <summary>A bounded container decides the width, and the content has no say in it.</summary>
    [Theory]
    [InlineData(320, 40)]
    [InlineData(320, 900)]
    [InlineData(0, 212)]
    public void Desired_FiniteRoom_TakesTheRoomWhateverTheContentMeasures(double available, double content) =>
        Assert.Equal(available, FieldWidth.Desired(available, content));

    /// <summary>A wrapping panel offers infinity, and there the content is the only answer there is.</summary>
    [Fact]
    public void Desired_UnboundedRoom_FallsBackToTheContent() =>
        Assert.Equal(212, FieldWidth.Desired(double.PositiveInfinity, content: 212));

    /// <summary>Returning NaN would poison the layout pass, so it is treated as unbounded.</summary>
    [Fact]
    public void Desired_UnknownRoom_FallsBackToTheContent() =>
        Assert.Equal(212, FieldWidth.Desired(double.NaN, content: 212));
}
