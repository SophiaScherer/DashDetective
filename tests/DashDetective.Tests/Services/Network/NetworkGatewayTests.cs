using DashDetective.Services.Network;
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
}
