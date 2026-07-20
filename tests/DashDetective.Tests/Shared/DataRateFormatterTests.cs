using DashDetective.Shared;
using Xunit;

namespace DashDetective.Tests.Shared;

/// <summary>Covers <see cref="DataRateFormatter"/>: the kbps/Mbps/Gbps magnitude boundaries,
/// <see cref="DataRateFormatter.Convert"/>, the ≥ 10 rounding threshold in
/// <see cref="DataRateFormatter.FormatValue"/>, negative clamps, and <see cref="DataRateFormatter.Split"/>.</summary>
public class DataRateFormatterTests {
    [Theory]
    [InlineData(0, RateUnit.Kbps)]
    [InlineData(0.99, RateUnit.Kbps)]
    [InlineData(1, RateUnit.Mbps)]
    [InlineData(999, RateUnit.Mbps)]
    [InlineData(999.99, RateUnit.Mbps)]
    [InlineData(1000, RateUnit.Gbps)]
    [InlineData(1500, RateUnit.Gbps)]
    [InlineData(-5, RateUnit.Kbps)]
    public void UnitFor_PicksUnitByMagnitude(double mbps, RateUnit expected) {
        Assert.Equal(expected, DataRateFormatter.UnitFor(mbps));
    }

    [Theory]
    [InlineData(500, RateUnit.Kbps, 500_000)]
    [InlineData(50, RateUnit.Mbps, 50)]
    [InlineData(2000, RateUnit.Gbps, 2)]
    [InlineData(-5, RateUnit.Mbps, 0)]
    public void Convert_ScalesToUnit_ClampingNegatives(double mbps, RateUnit unit, double expected) {
        Assert.Equal(expected, DataRateFormatter.Convert(mbps, unit), 6);
    }

    [Theory]
    [InlineData(RateUnit.Kbps, "kbps")]
    [InlineData(RateUnit.Mbps, "Mbps")]
    [InlineData(RateUnit.Gbps, "Gbps")]
    public void Label_ReturnsShortUnitString(RateUnit unit, string expected) {
        Assert.Equal(expected, DataRateFormatter.Label(unit));
    }

    [Theory]
    [InlineData(2.7, "2.7")]     // below 10 → one decimal
    [InlineData(0, "0.0")]
    [InlineData(10, "10")]       // threshold is inclusive → whole
    [InlineData(93.4, "93")]
    [InlineData(93.6, "94")]
    [InlineData(-3, "0.0")]      // negative clamps to 0
    public void FormatValue_WholeAtTenAndAbove_OneDecimalBelow(double value, string expected) {
        Assert.Equal(expected, DataRateFormatter.FormatValue(value));
    }

    [Theory]
    [InlineData(10.5, "10")]     // banker's rounding: half to even
    [InlineData(11.5, "12")]
    public void FormatValue_RoundsHalfToEven(double value, string expected) {
        Assert.Equal(expected, DataRateFormatter.FormatValue(value));
    }

    [Fact]
    public void Split_ReturnsValueAndUnitSeparately() {
        Assert.Equal(("2.7", "Mbps"), DataRateFormatter.Split(2.7));
        Assert.Equal(("1.5", "Gbps"), DataRateFormatter.Split(1500));
        Assert.Equal(("500", "kbps"), DataRateFormatter.Split(0.5));
    }

    [Fact]
    public void Format_JoinsValueAndUnit() {
        Assert.Equal("2.7 Mbps", DataRateFormatter.Format(2.7));
        Assert.Equal("1.5 Gbps", DataRateFormatter.Format(1500));
        Assert.Equal("500 kbps", DataRateFormatter.Format(0.5));
    }
}
