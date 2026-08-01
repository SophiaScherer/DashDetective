using DashDetective.Tabs.Hardware;
using Xunit;

namespace DashDetective.Tests.Tabs.Hardware;

/// <summary>Covers the Graphics records' defaults: every field a machine can't report has to arrive as the
/// neutral placeholder, and an unreadable adapter list has to stay empty so the card keeps its
/// placeholders rather than rendering a card for a GPU that isn't there.</summary>
public class GraphicsAdapterInfoTests {
    [Fact]
    public void Adapter_UnsuppliedFields_DefaultToThePlaceholder() {
        var adapter = new GraphicsAdapterInfo("AMD Radeon(TM) Graphics", Driver: "32.0.12019.1028");

        Assert.Equal("AMD Radeon(TM) Graphics", adapter.Name);
        Assert.Equal("32.0.12019.1028", adapter.Driver);
        Assert.Equal("—", adapter.Memory);
        Assert.Equal("—", adapter.CudaCores);
        Assert.Equal("—", adapter.BoostClock);
        Assert.Equal("—", adapter.Bus);
    }

    [Fact]
    public void AdapterUnknown_IsAllPlaceholders() {
        Assert.Equal("—", GraphicsAdapterInfo.Unknown.Name);
        Assert.Equal("—", GraphicsAdapterInfo.Unknown.Memory);
    }

    [Fact]
    public void Unknown_HasNoAdapters_SoTheCardKeepsItsPlaceholders() {
        Assert.Empty(GraphicsInfo.Unknown.Adapters);
    }

    [Fact]
    public void Info_CarriesEveryAdapterInOrder() {
        var info = new GraphicsInfo([
            new GraphicsAdapterInfo("NVIDIA GeForce RTX 3060", "12 GB GDDR6", "3,584", "1.78 GHz", "32.0", "PCIe 4.0 x16"),
            new GraphicsAdapterInfo("AMD Radeon(TM) Graphics", Driver: "32.0"),
        ]);

        Assert.Equal(2, info.Adapters.Count);
        Assert.Equal("NVIDIA GeForce RTX 3060", info.Adapters[0].Name);
        Assert.Equal("3,584", info.Adapters[0].CudaCores);
        Assert.Equal("AMD Radeon(TM) Graphics", info.Adapters[1].Name);
    }
}
