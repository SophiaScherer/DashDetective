using DashDetective.Tabs.Performance;
using System.Collections.Generic;
using Xunit;

namespace DashDetective.Tests.Tabs.Performance;

/// <summary>Covers <see cref="GpuEngineTiles"/>: only the engine names that unambiguously mean Copy, Video
/// Encode or Video Decode get a tile, each reads its busiest engine, and an engine type the adapter lacks
/// is absent rather than 0.</summary>
public class GpuEngineTilesTests {
    [Theory]
    [InlineData("copy", "Copy")]
    [InlineData("videoencode", "Video Encode")]
    [InlineData("video encode", "Video Encode")]
    [InlineData("videodecode", "Video Decode")]
    [InlineData("video decode", "Video Decode")]
    public void LabelFor_TaskManagerEngine_MapsToItsLabel(string key, string label) {
        Assert.Equal(label, GpuEngineTiles.LabelFor(key));
    }

    /// <summary>AMD's "Video Codec" serves encode and decode, so naming it either would be a guess.</summary>
    [Theory]
    [InlineData("video codec")]
    [InlineData("videocodec")]
    [InlineData("3d")]
    [InlineData("compute")]
    [InlineData("video jpeg")]
    public void LabelFor_OtherEngine_IsNull(string key) {
        Assert.Null(GpuEngineTiles.LabelFor(key));
    }

    [Fact]
    public void Readings_BothSpellingsOfOneEngine_KeepTheBusiest() {
        var readings = GpuEngineTiles.Readings(new Dictionary<string, double> {
            ["videodecode"] = 12, ["video decode"] = 30, ["copy"] = 4, ["3d"] = 50,
        });

        Assert.Equal(new Dictionary<string, double> { ["Video Decode"] = 30, ["Copy"] = 4 }, readings);
    }

    /// <summary>The Radeon iGPU's engine set: Copy and Video Decode, and a Video Codec that gets no tile.
    /// Video Encode is absent rather than 0.</summary>
    [Fact]
    public void Readings_AdapterWithoutAnEncoder_HasNoEncodeReading() {
        var readings = GpuEngineTiles.Readings(new Dictionary<string, double> {
            ["3d"] = 5, ["copy"] = 0, ["video decode"] = 0, ["video codec"] = 20,
        });

        Assert.False(readings.ContainsKey("Video Encode"));
        Assert.Equal(["Copy", "Video Decode"], readings.Keys);
    }
}
