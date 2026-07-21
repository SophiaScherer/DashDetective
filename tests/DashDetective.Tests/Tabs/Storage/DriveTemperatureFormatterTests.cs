using DashDetective.Tabs.Storage;
using Xunit;

namespace DashDetective.Tests.Tabs.Storage;

/// <summary>Covers <see cref="DriveTemperatureFormatter.Format"/>: the whole-degree "NN°C" readout, rounding,
/// and the "—" placeholder when there is no reading.</summary>
public class DriveTemperatureFormatterTests {
    [Fact]
    public void Format_Null_ShowsDash() {
        Assert.Equal("—", DriveTemperatureFormatter.Format(null));
    }

    [Fact]
    public void Format_WholeDegrees_RendersWithUnit() {
        Assert.Equal("51°C", DriveTemperatureFormatter.Format(51));
    }

    [Theory]
    [InlineData(41.4, "41°C")]   // rounds down
    [InlineData(41.6, "42°C")]   // rounds up
    public void Format_Fractional_RoundsToNearestWhole(double celsius, string expected) {
        Assert.Equal(expected, DriveTemperatureFormatter.Format(celsius));
    }
}
