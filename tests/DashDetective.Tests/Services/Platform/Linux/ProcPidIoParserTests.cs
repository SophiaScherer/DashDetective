using DashDetective.Services.Platform.Linux;
using DashDetective.Tests.Fakes;
using Xunit;

namespace DashDetective.Tests.Services.Platform.Linux;

/// <summary>Covers <see cref="ProcPidIoParser"/>: which pair of counters the Disk column is built from,
/// and the 0400 denial that is the ordinary case for a process you do not own.</summary>
public class ProcPidIoParserTests {
    private static bool TryParse(string body, out ulong total) =>
        ProcPidIoParser.TryParse(body.Replace("\r\n", "\n").Split('\n'), out total);

    /// <summary><c>rchar</c> + <c>wchar</c> — 3 MiB + 1 MiB. The fixture's <c>read_bytes</c>/
    /// <c>write_bytes</c> are deliberately much smaller, so summing the wrong pair produces 24 KiB and this
    /// fails loudly rather than plausibly.</summary>
    [Fact]
    public void TryParse_SumsTheSyscallLayerCounters() {
        Assert.True(TryParse(ProcFixtures.ProcPidIo, out var total));

        Assert.Equal(4UL * 1024 * 1024, total);
    }

    /// <summary>Mode 0400: another user's process denies the read, which surfaces as an empty file body.
    /// That is a blank rate, not an error and not a zero that reads as "idle" downstream.</summary>
    [Fact]
    public void TryParse_Denied_ReportsNothing() {
        Assert.False(TryParse("", out var total));

        Assert.Equal(0UL, total);
    }

    /// <summary>A torn read that caught only one counter still yields that counter — half a figure beats
    /// none, and the diff over the interval stays meaningful.</summary>
    [Fact]
    public void TryParse_OnlyOneCounterPresent_UsesIt() {
        Assert.True(TryParse("rchar: 2048\nsyscr: 12\n", out var total));

        Assert.Equal(2048UL, total);
    }

    /// <summary>Neither counter present — the file existed but carried nothing usable.</summary>
    [Fact]
    public void TryParse_NoCharCounters_ReportsNothing() =>
        Assert.False(TryParse("read_bytes: 8192\nwrite_bytes: 16384\n", out _));
}
