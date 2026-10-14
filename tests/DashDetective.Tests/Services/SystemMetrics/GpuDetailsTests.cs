using DashDetective.Services.SystemMetrics;
using Xunit;

namespace DashDetective.Tests.Services.SystemMetrics;

/// <summary>Covers <see cref="GpuDetails.FormatDirectX"/>: a <c>D3D_FEATURE_LEVEL</c> value reads as Task
/// Manager's "12 (FL major.minor)". The Direct3D probe itself needs a GPU and is checked on screen.</summary>
public class GpuDetailsTests {
    [Theory]
    [InlineData(0xc200, "12 (FL 12.2)")]
    [InlineData(0xc100, "12 (FL 12.1)")]
    [InlineData(0xc000, "12 (FL 12.0)")]
    [InlineData(0xb100, "12 (FL 11.1)")]
    [InlineData(0xb000, "12 (FL 11.0)")]
    public void FormatDirectX_FeatureLevel_ReadsAsTaskManagerWordsIt(int level, string expected) {
        Assert.Equal(expected, GpuDetails.FormatDirectX(level));
    }
}
