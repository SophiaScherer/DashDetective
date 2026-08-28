using DashDetective.Tabs.Processes;
using Xunit;

namespace DashDetective.Tests.Tabs.Processes;

/// <summary>Covers <see cref="ProcessSortState"/>: the round trip through settings, and that a record
/// naming no column this table has costs itself and nothing more.</summary>
public class ProcessSortStateTests {
    [Theory]
    [InlineData(ProcessSortKey.Name, true)]
    [InlineData(ProcessSortKey.Cpu, false)]
    [InlineData(ProcessSortKey.Memory, true)]
    public void EncodeThenDecode_RoundTrips(ProcessSortKey key, bool ascending) {
        Assert.True(ProcessSortState.TryDecode(ProcessSortState.Encode(key, ascending),
                                               out var decodedKey, out var decodedAscending));
        Assert.Equal(key, decodedKey);
        Assert.Equal(ascending, decodedAscending);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Cpu")]
    [InlineData("Network\u001FAsc")]
    [InlineData("Cpu\u001FAsc\u001Fextra")]
    public void TryDecode_Rubbish_IsRefusedRatherThanGuessed(string? encoded) {
        Assert.False(ProcessSortState.TryDecode(encoded, out _, out _));
    }
}
