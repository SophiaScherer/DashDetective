using DashDetective.Shared;
using DashDetective.Tabs.Processes;
using Xunit;

namespace DashDetective.Tests.Shared;

/// <summary>Covers <see cref="EnumListCodec"/>: the round trip, and that a bad or stale record costs
/// itself rather than the whole list.</summary>
public class EnumListCodecTests {
    [Fact]
    public void EncodeThenDecode_RoundTrips() {
        var values = new[] { ProcessCategory.Windows, ProcessCategory.App };

        Assert.Equal(values, EnumListCodec.Decode<ProcessCategory>(EnumListCodec.Encode(values)));
    }

    [Fact]
    public void Encode_DropsRepeats() {
        var encoded = EnumListCodec.Encode(new[] {
            ProcessCategory.App, ProcessCategory.App, ProcessCategory.Background,
        });

        Assert.Equal(new[] { ProcessCategory.App, ProcessCategory.Background },
                     EnumListCodec.Decode<ProcessCategory>(encoded));
    }

    [Fact]
    public void Decode_EmptyOrMissing_IsEmpty() {
        Assert.Empty(EnumListCodec.Decode<ProcessCategory>(null));
        Assert.Empty(EnumListCodec.Decode<ProcessCategory>(""));
    }

    /// <summary>Stored by name, not ordinal, so a member removed in a later release drops out instead
    /// of silently re-pointing the record at whichever member now sits at that index.</summary>
    [Fact]
    public void Decode_DropsNamesNoMemberAnswersTo() {
        var encoded = "App\u001FDrivers\u001FWindows";

        Assert.Equal(new[] { ProcessCategory.App, ProcessCategory.Windows },
                     EnumListCodec.Decode<ProcessCategory>(encoded));
    }
}
