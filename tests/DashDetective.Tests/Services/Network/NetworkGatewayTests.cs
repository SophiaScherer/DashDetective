using DashDetective.Services.Network;
using System.Net;
using Xunit;

namespace DashDetective.Tests.Services.Network;

/// <summary>Pins <see cref="NetworkGateway"/>'s contract: it never throws on any platform, and it reports
/// "no gateway" as null rather than inventing a host — the Toolkit and the Network tab answer that case
/// differently, so the lookup itself must not decide for them.</summary>
public class NetworkGatewayTests {
    [Fact]
    public void Primary_NeverThrows_AndIsEitherNullOrAnIPv4Address() {
        var gateway = NetworkGateway.Primary();

        // A CI runner may have no default route at all, so null is a legitimate result here.
        if (gateway is null)
            return;

        Assert.NotEqual("", gateway);
        Assert.Equal(4, gateway.Split('.').Length);
    }

    private static IPAddress Ip(string text) => IPAddress.Parse(text);

    [Fact]
    public void Pick_OnlyTheUnspecifiedAddress_ReturnsNull() =>
        Assert.Null(NetworkGateway.Pick([Ip("0.0.0.0")]));

    [Fact]
    public void Pick_SkipsTheUnspecifiedAddress_ForTheNextIPv4() =>
        Assert.Equal("192.168.1.1", NetworkGateway.Pick([Ip("0.0.0.0"), Ip("192.168.1.1")]));

    [Fact]
    public void Pick_SkipsIPv6Addresses_IncludingTheUnspecifiedOne() =>
        Assert.Equal("10.0.0.1", NetworkGateway.Pick([IPAddress.IPv6Any, Ip("fe80::1"), Ip("10.0.0.1")]));

    [Fact]
    public void Pick_NoAddresses_ReturnsNull() =>
        Assert.Null(NetworkGateway.Pick([]));
}
