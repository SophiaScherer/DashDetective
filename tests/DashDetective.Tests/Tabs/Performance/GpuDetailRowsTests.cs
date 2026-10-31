using DashDetective.Services.SystemMetrics;
using DashDetective.Tabs.Performance;
using System.Linq;
using Xunit;

namespace DashDetective.Tests.Tabs.Performance;

/// <summary>Covers <see cref="GpuDetailRows"/>: each published fact becomes a row under Task Manager's
/// label, in its order, and an unpublished one has no row at all.</summary>
public class GpuDetailRowsTests {
    [Fact]
    public void For_EveryFact_ListsThemInOrder() {
        var rows = GpuDetailRows.For(new GpuDetails("32.0.16.1062", "12 (FL 12.2)", "PCI bus 1, device 0, function 0"));

        Assert.Equal(
            [new DetailRow("Driver version", "32.0.16.1062"), new DetailRow("DirectX version", "12 (FL 12.2)"),
             new DetailRow("Physical location", "PCI bus 1, device 0, function 0")],
            rows);
    }

    /// <summary>Linux: no DirectX, and amdgpu publishes no driver version — only the location remains.</summary>
    [Fact]
    public void For_MissingFacts_AreOmitted() {
        var rows = GpuDetailRows.For(new GpuDetails(null, " ", "PCI bus 3, device 0, function 0"));

        Assert.Equal(["Physical location"], rows.Select(r => r.Key));
    }

    [Fact]
    public void For_NoDetails_IsEmpty() {
        Assert.Empty(GpuDetailRows.For(null));
    }
}
