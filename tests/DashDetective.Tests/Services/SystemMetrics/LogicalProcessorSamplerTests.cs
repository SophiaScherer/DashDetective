using DashDetective.Services.SystemMetrics;
using Xunit;

namespace DashDetective.Tests.Services.SystemMetrics;

/// <summary>Covers <see cref="LogicalProcessorSampler.TryParseInstance"/>: parsing "group,core" PDH instance
/// names into their numeric parts and rejecting the "_Total" roll-ups and malformed names.</summary>
public class LogicalProcessorSamplerTests {
    [Theory]
    [InlineData("0,0", 0, 0)]
    [InlineData("0,3", 0, 3)]
    [InlineData("0,11", 0, 11)]
    [InlineData("1,10", 1, 10)]
    public void TryParseInstance_ValidCore_ParsesGroupAndCore(string instance, int group, int core) {
        Assert.True(LogicalProcessorSampler.TryParseInstance(instance, out var g, out var c));
        Assert.Equal(group, g);
        Assert.Equal(core, c);
    }

    [Theory]
    [InlineData("_Total")]
    [InlineData("_total")]
    [InlineData("0,_Total")]
    [InlineData("0,_total")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("0")]        // no comma
    [InlineData("0,")]       // nothing after the comma
    [InlineData(",3")]       // nothing before the comma
    [InlineData("a,b")]      // non-numeric
    public void TryParseInstance_TotalRollupsAndMalformed_AreRejected(string? instance) {
        Assert.False(LogicalProcessorSampler.TryParseInstance(instance, out _, out _));
    }
}
