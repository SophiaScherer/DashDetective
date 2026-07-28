using DashDetective.Tabs.Storage;
using Xunit;

namespace DashDetective.Tests.Tabs.Storage;

/// <summary>Covers <see cref="PartitionTypeFormatter.Format"/>: the well-known GPT type GUIDs, the brace/case
/// variance WMI can return, and the fallbacks when the type is unknown or absent.</summary>
public class PartitionTypeFormatterTests {
    [Theory]
    [InlineData("{de94bba4-06d1-4d40-a16a-bfd50179d6ac}", "Recovery")]
    [InlineData("{c12a7328-f81f-11d2-ba4b-00a0c93ec93b}", "EFI System")]
    [InlineData("{e3c9e316-0b5c-4db8-817d-f92df00215ae}", "Reserved")]
    [InlineData("{ebd0a0a2-b9e5-4433-87c0-68b6b72699c7}", "Data")]
    public void Format_KnownGuid_NamesTheType(string gptType, string expected) {
        Assert.Equal(expected, PartitionTypeFormatter.Format(gptType, hasDriveLetter: false));
    }

    [Theory]
    [InlineData("de94bba4-06d1-4d40-a16a-bfd50179d6ac")]   // no braces
    [InlineData("{DE94BBA4-06D1-4D40-A16A-BFD50179D6AC}")] // upper case
    [InlineData("  {de94bba4-06d1-4d40-a16a-bfd50179d6ac}  ")]
    public void Format_GuidVariants_StillMatch(string gptType) {
        Assert.Equal("Recovery", PartitionTypeFormatter.Format(gptType, hasDriveLetter: false));
    }

    [Fact]
    public void Format_KnownGuid_IgnoresDriveLetter() {
        const string recovery = "{de94bba4-06d1-4d40-a16a-bfd50179d6ac}";
        Assert.Equal("Recovery", PartitionTypeFormatter.Format(recovery, hasDriveLetter: true));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{00000000-0000-0000-0000-000000000000}")]
    public void Format_UnknownWithLetter_FallsBackToData(string? gptType) {
        Assert.Equal("Data", PartitionTypeFormatter.Format(gptType, hasDriveLetter: true));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{00000000-0000-0000-0000-000000000000}")]
    public void Format_UnknownWithoutLetter_ShowsDash(string? gptType) {
        Assert.Equal("—", PartitionTypeFormatter.Format(gptType, hasDriveLetter: false));
    }
}
