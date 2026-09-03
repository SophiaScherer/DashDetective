using DashDetective.Services.Links;
using System;
using System.Collections.Generic;
using Xunit;

namespace DashDetective.Tests.Services.Links;

/// <summary>Covers <see cref="WebLinkOpener"/>: only an https address reaches the shell, and a launch
/// that throws is reported rather than propagated.</summary>
public class WebLinkOpenerTests {
    private readonly List<string> _started = [];

    private WebLinkOpener Opener(Exception? throws = null) => new(url => {
        _started.Add(url);
        if (throws is not null)
            throw throws;
    });

    [Fact]
    public void Open_HttpsUrl_ReachesTheShell() {
        Assert.True(Opener().Open("https://example.com/"));
        Assert.Equal("https://example.com/", Assert.Single(_started));
    }

    [Fact]
    public void Open_UppercaseScheme_IsStillHttps() {
        Assert.True(Opener().Open("HTTPS://example.com/"));
        Assert.Single(_started);
    }

    [Theory]
    [InlineData("http://example.com/")]
    [InlineData("file:///etc/passwd")]
    [InlineData("ms-settings:display")]
    [InlineData("example.com")]
    [InlineData("")]
    [InlineData("   ")]
    public void Open_AnythingButHttps_IsRefusedWithoutLaunching(string url) {
        Assert.False(Opener().Open(url));
        Assert.Empty(_started);
    }

    [Fact]
    public void Open_LaunchThrows_ReportsFailureRatherThanPropagating() {
        Assert.False(Opener(new InvalidOperationException("no browser")).Open("https://example.com/"));
        Assert.Single(_started);
    }
}
