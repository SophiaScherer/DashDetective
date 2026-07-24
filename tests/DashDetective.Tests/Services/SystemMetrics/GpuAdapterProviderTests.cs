using DashDetective.Services.SystemMetrics;
using Xunit;

namespace DashDetective.Tests.Services.SystemMetrics;

/// <summary>Covers <see cref="GpuAdapterProvider.FormatLuidToken"/>: the pure LUID→PDH-token formatting that
/// joins DXGI adapters to the <c>\GPU Engine(*)</c> counter instances. The DXGI COM path itself is verified
/// by smoke-run, not unit-tested.</summary>
public class GpuAdapterProviderTests {
    [Fact]
    public void FormatLuidToken_MatchesPdhInstanceTokenCasingAndWidth() {
        // The live NVIDIA adapter's LUID on the dev box (high 0, low 0xE54B) → the exact PDH token.
        Assert.Equal("luid_0x00000000_0x0000e54b", GpuAdapterProvider.FormatLuidToken(0, 0xE54B));
    }

    [Fact]
    public void FormatLuidToken_PadsBothPartsToEightHexDigits() {
        Assert.Equal("luid_0x00000001_0x000abcde", GpuAdapterProvider.FormatLuidToken(1, 0xABCDE));
    }

    [Fact]
    public void FormatLuidToken_FormatsNegativeHighPartAsUnsignedHex() {
        // HighPart is a signed int; a negative value formats as its unsigned 32-bit hex representation.
        Assert.Equal("luid_0xffffffff_0x00000000", GpuAdapterProvider.FormatLuidToken(-1, 0));
    }
}
