using DashDetective.Tabs.Hardware.Catalog;
using System.Collections.Generic;
using Xunit;

namespace DashDetective.Tests.Tabs.Hardware;

/// <summary>Covers <see cref="HardwareCatalog"/>: the <c>Normalize</c> stripping rules and the
/// <c>Match</c> resolution order (exact, then bidirectional-substring with longest-key-wins, else
/// null). The <c>Match</c> algorithm is exercised with synthetic normalized tables so the assertions
/// don't depend on the shipped catalog data; a couple of real <c>Lookup</c> calls smoke-test the wiring.</summary>
public class HardwareCatalogTests {
    // --- Normalize ---

    [Fact]
    public void Normalize_StripsTrademarkParenthesesAndSymbols() {
        Assert.Equal("INTEL CORE I7", HardwareCatalog.Normalize("Intel(R) Core(TM) i7"));
        Assert.Equal("AMD RYZEN 5", HardwareCatalog.Normalize("AMD® Ryzen™ 5"));
    }

    [Fact]
    public void Normalize_StripsCpuAndProcessorWords() {
        Assert.Equal("INTEL CORE I5", HardwareCatalog.Normalize("Intel Core i5 CPU Processor"));
    }

    [Fact]
    public void Normalize_StripsClockSuffix() {
        Assert.Equal("AMD RYZEN 5 7600X", HardwareCatalog.Normalize("AMD Ryzen 5 7600X @ 4.70GHz"));
    }

    [Fact]
    public void Normalize_StripsIntegratedGpuSuffix() {
        Assert.Equal("AMD RYZEN 5 5600G", HardwareCatalog.Normalize("AMD Ryzen 5 5600G with Radeon Graphics"));
    }

    [Fact]
    public void Normalize_CollapsesNonAlphanumericRunsAndTrims() {
        Assert.Equal("FOO BAR BAZ", HardwareCatalog.Normalize("  Foo--Bar__Baz  "));
    }

    // --- Match (synthetic tables) ---

    [Fact]
    public void Match_ExactNormalizedKey_ReturnsThatValue() {
        var table = new Dictionary<string, string> { ["RTX 4070"] = "base" };
        Assert.Equal("base", HardwareCatalog.Match(table, "RTX 4070"));
    }

    [Fact]
    public void Match_LongestKeyWins_VariantNotShadowedByBase() {
        var table = new Dictionary<string, string> {
            ["RTX 4070"] = "base",
            ["RTX 4070 TI"] = "variant",
        };
        Assert.Equal("variant", HardwareCatalog.Match(table, "NVIDIA GeForce RTX 4070 Ti"));
        Assert.Equal("base", HardwareCatalog.Match(table, "NVIDIA GeForce RTX 4070"));
    }

    [Fact]
    public void Match_LongestKeyWins_SuffixDigitsDistinguishModels() {
        var table = new Dictionary<string, string> {
            ["7600"] = "base",
            ["7600X"] = "x-variant",
        };
        Assert.Equal("x-variant", HardwareCatalog.Match(table, "AMD Ryzen 5 7600X"));
        Assert.Equal("base", HardwareCatalog.Match(table, "AMD Ryzen 5 7600"));
    }

    [Fact]
    public void Match_ShortRawInsideLongKey_MatchesEitherDirection() {
        var table = new Dictionary<string, string> { ["RTX 4070 TI"] = "v" };
        Assert.Equal("v", HardwareCatalog.Match(table, "4070 Ti"));
    }

    [Fact]
    public void Match_NoOverlap_ReturnsNull() {
        var table = new Dictionary<string, string> { ["RTX 4070"] = "v" };
        Assert.Null(HardwareCatalog.Match(table, "GTX 1080"));
    }

    [Fact]
    public void Match_EmptyTable_ReturnsNull() {
        Assert.Null(HardwareCatalog.Match(new Dictionary<string, string>(), "RTX 4070"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Match_BlankRaw_ReturnsNull(string raw) {
        var table = new Dictionary<string, string> { ["RTX 4070"] = "v" };
        Assert.Null(HardwareCatalog.Match(table, raw));
    }

    // --- Real Lookup smoke ---

    [Fact]
    public void LookupGpu_KnownModel_ResolvesFromShippedCatalog() {
        var spec = HardwareCatalog.LookupGpu("NVIDIA GeForce RTX 4070 Ti");
        Assert.NotNull(spec);
        Assert.Equal("7,680", spec!.CudaCores);   // the Ti variant, not the base 4070's 5,888
    }

    [Fact]
    public void LookupGpu_UnknownModel_ReturnsNull() {
        Assert.Null(HardwareCatalog.LookupGpu("Definitely Not A Real GPU 9999"));
    }
}
