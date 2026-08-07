using DashDetective.Tabs.Hardware;
using Xunit;

namespace DashDetective.Tests.Tabs.Hardware.Providers;

/// <summary>Covers <see cref="ChipsetNames"/>, the token scan both platforms fall back to when the board
/// catalog has no entry. It was private to the WMI provider until Linux needed it too, and nothing
/// exercised it then — this is the coverage the extraction earned.</summary>
public class ChipsetNamesTests {
    /// <summary>The table is ordered most-specific-first so a board naming a B650E does not match the
    /// B650 token and lose its E. Getting the order wrong is silent and plausible-looking.</summary>
    [Theory]
    [InlineData("ROG STRIX B650E-F GAMING WIFI", "AMD B650E")]
    [InlineData("MPG B650I EDGE WIFI", "AMD B650")]
    [InlineData("PRIME X670E-PRO WIFI", "AMD X670E")]
    [InlineData("TUF GAMING X670E-PLUS", "AMD X670E")]
    public void Derive_PrefersTheMoreSpecificToken(string product, string expected) =>
        Assert.Equal(expected, ChipsetNames.Derive(product));

    [Theory]
    [InlineData("ROG MAXIMUS Z790 HERO", "Intel Z790")]
    [InlineData("PRO B760M-A WIFI", "Intel B760")]
    [InlineData("H610M-HDV/M.2", "Intel H610")]
    public void Derive_RecognisesIntelChipsets(string product, string expected) =>
        Assert.Equal(expected, ChipsetNames.Derive(product));

    /// <summary>Board names arrive in whatever case the vendor wrote, so the scan is case-insensitive.</summary>
    [Fact]
    public void Derive_MatchesRegardlessOfCase() =>
        Assert.Equal("AMD B550", ChipsetNames.Derive("tuf gaming b550-plus"));

    /// <summary>An unknown board reports "" rather than a wrong guess — the caller turns that into "—".</summary>
    [Theory]
    [InlineData("VirtualBox")]
    [InlineData("Some Board With No Chipset Token")]
    [InlineData("")]
    [InlineData("   ")]
    public void Derive_WithNoKnownToken_IsEmpty(string product) =>
        Assert.Equal("", ChipsetNames.Derive(product));
}
