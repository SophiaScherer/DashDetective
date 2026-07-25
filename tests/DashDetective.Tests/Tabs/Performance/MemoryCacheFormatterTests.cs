using DashDetective.Tabs.Performance;
using System.Globalization;
using Xunit;

namespace DashDetective.Tests.Tabs.Performance;

/// <summary>Covers <see cref="MemoryCacheFormatter.Format"/>: rendering a byte count as one decimal of
/// binary GB (matching the In use / Available tiles beside it), and falling back to the neutral placeholder
/// when the provider reports no reading.</summary>
public class MemoryCacheFormatterTests {
    [Theory]
    [InlineData(6_144_000_000UL, "5.7 GB")]     // ~1.5M pages of 4 KiB — binary GB, so not "6.1"
    [InlineData(1_073_741_824UL, "1.0 GB")]     // exactly one GiB keeps the trailing zero
    [InlineData(17_179_869_184UL, "16.0 GB")]
    [InlineData(536_870_912UL, "0.5 GB")]       // sub-GB keeps the leading zero
    [InlineData(4096UL, "0.0 GB")]              // a tiny-but-real reading is not the placeholder
    public void Format_PlausibleReading_RendersBinaryGb(ulong bytes, string expected) {
        Assert.Equal(expected, MemoryCacheFormatter.Format(bytes));
    }

    [Fact]
    public void Format_NoReading_ReturnsPlaceholder() {
        Assert.Equal("—", MemoryCacheFormatter.Format(null));
    }

    /// <summary>Zero bytes means "not reported", not a real cache of nothing.</summary>
    [Fact]
    public void Format_ZeroBytes_ReturnsPlaceholder() {
        Assert.Equal("—", MemoryCacheFormatter.Format(0));
    }

    /// <summary>The decimal separator is a period regardless of the ambient culture, matching the rest of
    /// the app's InvariantCulture formatting.</summary>
    [Fact]
    public void Format_UnderACommaDecimalCulture_StillUsesAPeriod() {
        var previous = CultureInfo.CurrentCulture;
        try {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            Assert.Equal("5.7 GB", MemoryCacheFormatter.Format(6_144_000_000UL));
        } finally {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
