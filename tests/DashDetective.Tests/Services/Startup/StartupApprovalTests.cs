using DashDetective.Services.Startup;
using Xunit;

namespace DashDetective.Tests.Services.Startup;

/// <summary>Pins the decode of Task Manager's <c>StartupApproved\Run</c> value: the first byte is the
/// switch, and a missing value means enabled.</summary>
public class StartupApprovalTests {
    [Theory]
    [InlineData(new byte[] { 0x02, 0, 0, 0 }, false)]
    [InlineData(new byte[] { 0x06, 0, 0, 0 }, false)]
    [InlineData(new byte[] { 0x03, 0, 0, 0 }, true)]
    [InlineData(new byte[] { 0x07, 0, 0, 0 }, true)]
    [InlineData(new byte[0], false)]
    public void IsDisabled_ReadsTheFirstByte(byte[] value, bool expected) =>
        Assert.Equal(expected, StartupApproval.IsDisabled(value));

    [Fact]
    public void IsDisabled_NoValue_IsEnabled() => Assert.False(StartupApproval.IsDisabled(null));
}
