using DashDetective.Tabs.Hardware;
using System;
using System.Threading.Tasks;
using Xunit;

namespace DashDetective.Tests.Tabs.Hardware;

/// <summary>Covers the <see cref="IHardwareInfoProvider"/> seam: which implementation the platform
/// resolves to, and that the unsupported one degrades to exactly the placeholder snapshot the old
/// inline <c>OperatingSystem.IsWindows()</c> guard returned.
///
/// <see cref="HardwareViewModel"/> itself is not covered here — its constructor touches
/// <c>HardwareIcons</c>, whose static initialiser calls <c>Geometry.Parse</c> and needs a render
/// backend these tests deliberately don't have (see the Testing conventions in AGENTS.md).</summary>
public class HardwareInfoProviderTests {
    [Fact]
    public void ForCurrentPlatform_ResolvesTheReaderForThisHost() {
        var provider = IHardwareInfoProvider.ForCurrentPlatform();

        if (OperatingSystem.IsWindows())
            Assert.IsType<WindowsHardwareInfoProvider>(provider);
        else
            Assert.IsType<UnsupportedHardwareInfoProvider>(provider);
    }

    /// <summary>Every card reports <c>.Unknown</c>, so every field renders "—" rather than blanking.</summary>
    [Fact]
    public async Task Unsupported_GetAsync_ReportsUnknownForEveryCard() {
        var info = await new UnsupportedHardwareInfoProvider().GetAsync();

        Assert.Same(HardwareInfo.Unknown, info);
        Assert.Equal("—", info.Processor.Name);
        Assert.Equal("—", info.Memory.Summary);
        Assert.Equal("—", info.Storage.Summary);
        Assert.Equal("—", info.Motherboard.Chipset);
        Assert.Empty(info.Graphics.Adapters);
    }

    /// <summary>The real reader never throws, whatever WMI does — the whole page depends on it, and each
    /// section is meant to fall back independently rather than propagate.</summary>
    [Fact]
    public async Task Windows_GetAsync_NeverThrows() {
        if (!OperatingSystem.IsWindows())
            return;

        var info = await new WindowsHardwareInfoProvider().GetAsync();

        Assert.NotNull(info.Processor);
        Assert.NotNull(info.Memory);
        Assert.NotNull(info.Storage);
        Assert.NotNull(info.Motherboard);
        Assert.NotNull(info.Graphics);
    }
}
