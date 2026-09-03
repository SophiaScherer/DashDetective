using DashDetective.Tabs.Network;
using Xunit;

namespace DashDetective.Tests.Tabs.Network;

/// <summary>Covers <see cref="HostLink"/>: which targets become an https address, how an IPv6 literal is
/// written, and that anything unusable reports null.</summary>
public class HostLinkTests {
    [Theory]
    [InlineData("example.com", "https://example.com/")]
    [InlineData("  example.com  ", "https://example.com/")]
    [InlineData("sub.example.co.uk", "https://sub.example.co.uk/")]
    [InlineData("localhost", "https://localhost/")]
    [InlineData("8.8.8.8", "https://8.8.8.8/")]
    public void For_UsableHost_IsHttps(string host, string expected) {
        Assert.Equal(expected, HostLink.For(host));
    }

    [Fact]
    public void For_Ipv6Literal_IsBracketed() {
        // A bare IPv6 literal is not a legal URL authority — its colons read as a port.
        Assert.Equal("https://[::1]/", HostLink.For("::1"));
    }

    [Fact]
    public void For_FullyQualifiedName_KeepsResolving() {
        Assert.NotNull(HostLink.For("example.com."));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("-bad-")]
    [InlineData("not a host")]
    [InlineData("example.com/path")]
    [InlineData("https://example.com")]
    [InlineData("example.com:443")]
    public void For_UnusableTarget_IsNull(string? host) {
        Assert.Null(HostLink.For(host));
    }
}
