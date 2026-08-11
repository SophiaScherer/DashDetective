using DashDetective.Services.Network;
using DashDetective.Tabs.Toolkit;
using System.Net.NetworkInformation;
using Xunit;

namespace DashDetective.Tests.Services.Network;

/// <summary>
/// Smoke cover for the adapter selection, which is portable managed code over
/// <see cref="NetworkInterface"/> and so needs no platform seam.
///
/// These run against the live host, so they assert only what holds on any machine — including a CI runner
/// with one virtual adapter, and a dev box with several. The failure they exist to catch is the one that
/// would otherwise stay invisible until someone booted the VM: a managed API that is Windows-only in
/// practice throwing <c>PlatformNotSupportedException</c> on the Linux leg. Whether the *right* adapter is
/// picked cannot be asserted headlessly and is a VM check.
/// </summary>
public class NetworkUsageSamplerTests {
    /// <summary>Selection reads adapter status, type, statistics and gateways — every one of which must be
    /// implemented on whichever platform this runs on.</summary>
    [Fact]
    public void SelectPrimary_ReadsTheHostsAdaptersWithoutThrowing() {
        var primary = NetworkUsageSampler.SelectPrimary();

        // Null is legitimate: a machine with no usable adapter. What matters is that getting there did not
        // throw, and that a chosen adapter is one the sampler can actually read.
        if (primary is not null)
            Assert.NotNull(primary.GetIPStatistics());
    }

    /// <summary>The gateway test the candidate filter narrows by. Runs it over every adapter on the host so
    /// an unimplemented <c>GetIPProperties</c> would surface here rather than in the VM.</summary>
    [Fact]
    public void HasUsableGateway_ReadsEveryAdapterWithoutThrowing() {
        foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
            NetworkUsageSampler.HasUsableGateway(adapter);
    }

    /// <summary>Baselining in the constructor and sampling both go through the same selection. Rates are
    /// never negative — the sampler clamps a counter reset rather than reporting a backwards rate.</summary>
    [Fact]
    public void Sample_ReportsNonNegativeRates() {
        var sample = new NetworkUsageSampler().Sample();

        Assert.True(sample.DownMbps >= 0);
        Assert.True(sample.UpMbps >= 0);
    }

    /// <summary>The Toolkit's gateway suggestion shares that selection and must never fail a page load, on
    /// any platform — it falls back to a literal rather than throwing.</summary>
    [Fact]
    public void PrimaryGateway_AlwaysYieldsAHost() =>
        Assert.False(string.IsNullOrWhiteSpace(ToolkitDefaults.PrimaryGateway()));
}
