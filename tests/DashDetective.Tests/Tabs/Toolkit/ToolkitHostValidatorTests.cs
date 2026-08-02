using DashDetective.Tabs.Toolkit;
using Xunit;

namespace DashDetective.Tests.Tabs.Toolkit;

/// <summary>Covers <see cref="ToolkitHostValidator"/> — the only user input in the Toolkit. Real hosts
/// and IP literals are accepted; anything that could read as a flag, carry shell punctuation or run past
/// the DNS length limits is refused. The flag cases are the ones that matter: injection is already
/// impossible (the value becomes one argument-list element), so the job left here is that an accepted
/// value cannot change what the command does.</summary>
public class ToolkitHostValidatorTests {
    [Theory]
    [InlineData("example.com")]
    [InlineData("learn.microsoft.com")]
    [InlineData("localhost")]
    [InlineData("my-router")]
    [InlineData("a.b.c.d.example.com")]
    [InlineData("example.com.")]        // legal fully-qualified form
    [InlineData("xn--bcher-kva.example")] // punycode
    [InlineData("host123")]
    public void IsValid_RealHostNames_AreAccepted(string host) {
        Assert.True(ToolkitHostValidator.IsValid(host));
    }

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("192.168.1.1")]
    [InlineData("127.0.0.1")]
    [InlineData("::1")]
    [InlineData("2001:db8::1")]
    public void IsValid_IpLiterals_AreAccepted(string host) {
        Assert.True(ToolkitHostValidator.IsValid(host));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsValid_NothingTyped_IsRefused(string? host) {
        Assert.False(ToolkitHostValidator.IsValid(host));
    }

    /// <summary>The property this validator exists for: an accepted value can never read as an option,
    /// so "ping forever" and friends cannot be smuggled into a row that promises four echoes.</summary>
    [Theory]
    [InlineData("-t")]
    [InlineData("-n")]
    [InlineData("/all")]
    [InlineData("--help")]
    [InlineData("-t example.com")]
    public void IsValid_AnythingReadingAsAFlag_IsRefused(string host) {
        Assert.False(ToolkitHostValidator.IsValid(host));
    }

    [Theory]
    [InlineData("example.com && calc")]
    [InlineData("example.com | more")]
    [InlineData("example.com; shutdown")]
    [InlineData("example.com`whoami`")]
    [InlineData("$(whoami)")]
    [InlineData("%windir%")]
    [InlineData("a\"b")]
    [InlineData("a'b")]
    [InlineData("a b")]
    [InlineData("a\tb")]
    [InlineData("a\nb")]
    [InlineData(@"C:\Windows")]
    [InlineData("http://example.com")]
    [InlineData("example.com/path")]
    public void IsValid_ShellPunctuationAndPaths_AreRefused(string host) {
        Assert.False(ToolkitHostValidator.IsValid(host));
    }

    [Theory]
    [InlineData("-example.com")]  // label may not start with a hyphen
    [InlineData("example-.com")]  // nor end with one
    [InlineData("example..com")]  // empty label
    [InlineData(".example.com")]
    public void IsValid_MalformedLabels_AreRefused(string host) {
        Assert.False(ToolkitHostValidator.IsValid(host));
    }

    [Fact]
    public void IsValid_OverlongName_IsRefused() {
        Assert.False(ToolkitHostValidator.IsValid(new string('a', ToolkitHostValidator.MaxLength + 1)));
    }

    [Fact]
    public void IsValid_OverlongLabel_IsRefused() {
        var label = new string('a', ToolkitHostValidator.MaxLabelLength + 1);

        Assert.False(ToolkitHostValidator.IsValid(label + ".com"));
        Assert.True(ToolkitHostValidator.IsValid(new string('a', ToolkitHostValidator.MaxLabelLength) + ".com"));
    }

    [Fact]
    public void IsValid_SurroundingWhitespace_IsTrimmedBeforeJudging() {
        Assert.True(ToolkitHostValidator.IsValid("  example.com  "));
    }

    [Fact]
    public void Normalize_ValidHost_IsTrimmed() {
        Assert.Equal("example.com", ToolkitHostValidator.Normalize("  example.com "));
    }

    [Fact]
    public void Normalize_RefusedHost_IsEmptySoNothingCanBePassedOn() {
        Assert.Equal("", ToolkitHostValidator.Normalize("-t"));
        Assert.Equal("", ToolkitHostValidator.Normalize(null));
    }
}
