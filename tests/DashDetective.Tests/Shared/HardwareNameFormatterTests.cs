using DashDetective.Shared;
using Xunit;

namespace DashDetective.Tests.Shared;

/// <summary>Covers <see cref="HardwareNameFormatter"/>: the CPU trim (trademarks, "@ …GHz" clock,
/// "N-Core Processor", " Processor"/" CPU", whitespace) and the GPU trim (leading vendor prefix,
/// trademarks, whitespace), plus the null/blank fallbacks.</summary>
public class HardwareNameFormatterTests {
    [Fact]
    public void ShortenCpu_StripsCoreProcessorSuffix() {
        Assert.Equal("AMD Ryzen 5 7600X", HardwareNameFormatter.ShortenCpu("AMD Ryzen 5 7600X 6-Core Processor"));
    }

    [Fact]
    public void ShortenCpu_StripsTrademarksClockAndCpuWord() {
        Assert.Equal("Intel Core i7-9700K",
            HardwareNameFormatter.ShortenCpu("Intel(R) Core(TM) i7-9700K CPU @ 3.60GHz"));
    }

    [Fact]
    public void ShortenCpu_CollapsesWhitespace() {
        Assert.Equal("AMD Ryzen 5", HardwareNameFormatter.ShortenCpu("AMD   Ryzen  5"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ShortenCpu_Blank_ReturnsUnknownCpu(string raw) {
        Assert.Equal("Unknown CPU", HardwareNameFormatter.ShortenCpu(raw));
    }

    [Fact]
    public void ShortenGpu_StripsLeadingVendorPrefix() {
        Assert.Equal("GeForce RTX 3060", HardwareNameFormatter.ShortenGpu("NVIDIA GeForce RTX 3060"));
        Assert.Equal("Radeon RX 6700 XT", HardwareNameFormatter.ShortenGpu("AMD Radeon RX 6700 XT"));
    }

    [Fact]
    public void ShortenGpu_VendorPrefixIsCaseInsensitive() {
        Assert.Equal("GeForce RTX 3060", HardwareNameFormatter.ShortenGpu("nvidia GeForce RTX 3060"));
    }

    [Fact]
    public void ShortenGpu_StripsTrademarksAndVendor() {
        Assert.Equal("Arc A770", HardwareNameFormatter.ShortenGpu("Intel(R) Arc(TM) A770"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ShortenGpu_Blank_ReturnsUnknownGpu(string raw) {
        Assert.Equal("Unknown GPU", HardwareNameFormatter.ShortenGpu(raw));
    }
}
