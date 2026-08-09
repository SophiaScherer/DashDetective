using DashDetective.Services.Platform.Linux;
using DashDetective.Tests.Fakes;
using Xunit;

namespace DashDetective.Tests.Services.Platform.Linux;

/// <summary>Covers <see cref="ProcDiskstatsParser"/>: the column layout across the kernel versions that
/// grew it, and the packed identity that has to match what the block reader derives.</summary>
public class ProcDiskstatsParserTests {
    private static System.Collections.Generic.IReadOnlyDictionary<int, DiskStatsCounters> Parse(string body) =>
        ProcDiskstatsParser.Parse(body.Replace("\r\n", "\n").Split('\n'));

    private static DiskStatsCounters Sda() => Parse(ProcFixtures.ProcDiskstats)[(8 << 20) | 0];

    /// <summary>The key the whole Storage surface joins on: the same packing <see cref="SysBlockFacts"/>
    /// applies to <c>/sys/block/*/dev</c>, from the row's own first two columns.</summary>
    [Fact]
    public void Parse_KeysARowByItsPackedDeviceNumber() =>
        Assert.Equal("sda", Sda().Name);

    [Fact]
    public void Parse_ReadsTheTransferCounters() {
        var sda = Sda();

        Assert.Equal(2048UL, sda.SectorsRead);
        Assert.Equal(4096UL, sda.SectorsWritten);
        Assert.Equal(5000UL, sda.ReadsCompleted);
        Assert.Equal(3000UL, sda.WritesCompleted);
    }

    /// <summary>The three fields the source plan omitted, and the ones the page's headline numbers, queue
    /// readout and response readout are built from.</summary>
    [Fact]
    public void Parse_ReadsTheTimingAndDepthCounters() {
        var sda = Sda();

        Assert.Equal(800UL, sda.MillisecondsReading);
        Assert.Equal(900UL, sda.MillisecondsWriting);
        Assert.Equal(1000UL, sda.IoMilliseconds);
        Assert.Equal(0UL, sda.InFlight);
    }

    /// <summary>Partitions are listed alongside their disk and are parsed like any other row; dropping them
    /// is the sampler's job, since only it knows which numbers are disks.</summary>
    [Fact]
    public void Parse_KeepsPartitionRows() =>
        Assert.Equal("sda1", Parse(ProcFixtures.ProcDiskstats)[(8 << 20) | 1].Name);

    /// <summary>4.18 appended discards and 5.5 appended flushes. Only the first fourteen columns may be
    /// assumed, and reading a trailing column as a leading one is what this catches.</summary>
    [Fact]
    public void Parse_ReadsTheTwentyFieldFormByTheSameIndices() {
        var nvme = Parse(ProcFixtures.ProcDiskstatsModern)[(259 << 20) | 0];

        Assert.Equal("nvme0n1", nvme.Name);
        Assert.Equal(2048UL, nvme.SectorsRead);
        Assert.Equal(4096UL, nvme.SectorsWritten);
        Assert.Equal(1000UL, nvme.IoMilliseconds);
    }

    /// <summary>A row shorter than the oldest layout is not a stats row.</summary>
    [Fact]
    public void Parse_SkipsAShortRow() =>
        Assert.Empty(Parse("8 0 sda 5000 100 2048 800"));

    /// <summary>A non-numeric device number is not addressable, so the row is dropped rather than filed
    /// under a wrong key.</summary>
    [Fact]
    public void Parse_SkipsARowWithAMalformedDeviceNumber() =>
        Assert.Empty(Parse("x y sda 1 2 3 4 5 6 7 8 9 10 11"));
}
