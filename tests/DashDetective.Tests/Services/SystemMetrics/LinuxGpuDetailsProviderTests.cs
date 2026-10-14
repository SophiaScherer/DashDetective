using DashDetective.Services.SystemMetrics;
using DashDetective.Tests.Fakes;
using System;
using System.Threading.Tasks;
using Xunit;

namespace DashDetective.Tests.Services.SystemMetrics;

/// <summary>Covers <see cref="LinuxGpuDetailsProvider"/>: each requested card's details by its PCI-address
/// key, null where Linux publishes nothing (DirectX always, the driver version for in-tree amdgpu), and only
/// the cards asked about.</summary>
public class LinuxGpuDetailsProviderTests {
    private const string Nvidia = "0000:01:00.0";
    private const string Amd = "0000:03:00.0";

    private static FakeProcFileSystem BothCards() => new FakeProcFileSystem().WithAmdgpuCard().WithNvidiaCard();

    [Fact]
    public async Task GetAsync_NvidiaCard_CarriesDriverAndLocationButNoDirectX() {
        var details = await new LinuxGpuDetailsProvider(BothCards()).GetAsync([Nvidia]);

        Assert.Equal(new GpuDetails("550.107.02", null, "PCI bus 1, device 0, function 0"), details[Nvidia]);
    }

    /// <summary>amdgpu is in-tree and publishes no version of its own, so only the location remains.</summary>
    [Fact]
    public async Task GetAsync_AmdCard_HasNoDriverVersion() {
        var details = await new LinuxGpuDetailsProvider(BothCards()).GetAsync([Amd]);

        Assert.Equal(new GpuDetails(null, null, "PCI bus 3, device 0, function 0"), details[Amd]);
    }

    [Fact]
    public async Task GetAsync_OnlyTheCardsAskedAbout() {
        var details = await new LinuxGpuDetailsProvider(BothCards()).GetAsync([Amd, "0000:09:00.0"]);

        Assert.Equal([Amd], details.Keys);
    }

    /// <summary>A filesystem that breaks its never-throw contract yields nothing rather than a throw.</summary>
    [Fact]
    public async Task GetAsync_DeniedRead_IsEmpty() {
        var proc = BothCards().ThrowOn("/sys/class/drm/card1/device/driver/module/version");

        Assert.Empty(await new LinuxGpuDetailsProvider(proc).GetAsync([Nvidia]));
    }

    [Fact]
    public async Task GetAsync_RealFileSystem_SoftFailsToEmpty() {
        if (OperatingSystem.IsLinux())
            return;

        Assert.Empty(await new LinuxGpuDetailsProvider().GetAsync([Nvidia]));
    }
}
