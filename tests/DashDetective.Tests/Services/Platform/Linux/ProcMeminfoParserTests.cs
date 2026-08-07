using DashDetective.Services.Platform.Linux;
using DashDetective.Tests.Fakes;
using System.Collections.Generic;
using Xunit;

namespace DashDetective.Tests.Services.Platform.Linux;

/// <summary>Covers <see cref="ProcMeminfoParser"/>: the kB→bytes scaling that is the whole point of the
/// class, the unitless counts that must not be scaled, and the malformed lines a file that grows fields
/// across kernel versions is expected to contain.</summary>
public class ProcMeminfoParserTests {
    private const ulong Kib = 1024;

    private static IReadOnlyDictionary<string, ulong> Parse(params string[] lines) =>
        ProcMeminfoParser.Parse(lines);

    /// <summary>The label says kB but the unit is kibibytes, so every suffixed value scales by 1024.
    /// Getting this wrong is a 2.4% error — small enough to look plausible on screen.</summary>
    [Fact]
    public void Parse_ScalesKilobyteValuesByAKibibyte() {
        var fields = Parse("MemTotal:       16777216 kB");

        Assert.Equal(16_777_216UL * Kib, ProcMeminfoParser.Value(fields, "MemTotal"));
    }

    /// <summary>A value with no unit is a count, not a size, and passes through unscaled.</summary>
    [Fact]
    public void Parse_UnitlessValue_IsNotScaled() {
        var fields = Parse("HugePages_Total:       3");

        Assert.Equal(3UL, ProcMeminfoParser.Value(fields, "HugePages_Total"));
    }

    /// <summary>The kernel writes "kB"; anything else is a unit this parser does not understand, and
    /// guessing at its scale would be worse than reporting nothing.</summary>
    [Fact]
    public void Parse_UnknownUnit_IsSkipped() {
        var fields = Parse("MemTotal:       16 MB");

        Assert.Empty(fields);
    }

    [Theory]
    [InlineData("MemTotal 16777216 kB")]     // no colon
    [InlineData(":       16777216 kB")]      // no key
    [InlineData("MemTotal:")]                // no value
    [InlineData("MemTotal:       none kB")]  // non-numeric
    [InlineData("")]
    public void Parse_MalformedLine_IsSkippedNotFatal(string line) {
        var fields = Parse(line, "MemFree:         2097152 kB");

        Assert.Equal(2_097_152UL * Kib, ProcMeminfoParser.Value(fields, "MemFree"));
        Assert.Single(fields);
    }

    /// <summary>An absent field reads 0 — the "not reported" contract every caller branches on.</summary>
    [Fact]
    public void Value_AbsentKey_IsZero() {
        Assert.Equal(0UL, ProcMeminfoParser.Value(Parse("MemTotal:       1 kB"), "MemAvailable"));
    }

    /// <summary>Keys are ordinal, as the kernel writes them — a lookup must not silently match a different
    /// casing and report the wrong field.</summary>
    [Fact]
    public void Value_IsCaseSensitive() {
        Assert.Equal(0UL, ProcMeminfoParser.Value(Parse("MemTotal:       1 kB"), "memtotal"));
    }

    /// <summary>A nonsensical value must saturate rather than wrap into a small, plausible-looking one.</summary>
    [Fact]
    public void Parse_OverflowingValue_Saturates() {
        var fields = Parse("MemTotal:       18446744073709551615 kB");

        Assert.Equal(ulong.MaxValue, ProcMeminfoParser.Value(fields, "MemTotal"));
    }

    [Fact]
    public void Parse_NoLines_IsEmpty() {
        Assert.Empty(ProcMeminfoParser.Parse([]));
    }

    /// <summary>The real file, end to end: every field the app reads is present and scaled.</summary>
    [Fact]
    public void Parse_TheFixture_ReadsEveryFieldTheAppNeeds() {
        var fields = ProcMeminfoParser.Parse(Lines(ProcFixtures.ProcMeminfo));

        Assert.Equal(16_777_216UL * Kib, ProcMeminfoParser.Value(fields, "MemTotal"));
        Assert.Equal(8_388_608UL * Kib, ProcMeminfoParser.Value(fields, "MemAvailable"));
        Assert.Equal(5_242_880UL * Kib, ProcMeminfoParser.Value(fields, "Cached"));
        Assert.Equal(786_432UL * Kib, ProcMeminfoParser.Value(fields, "SReclaimable"));
        Assert.Equal(9_437_184UL * Kib, ProcMeminfoParser.Value(fields, "Committed_AS"));
        Assert.Equal(10_485_760UL * Kib, ProcMeminfoParser.Value(fields, "CommitLimit"));
        Assert.Equal(0UL, ProcMeminfoParser.Value(fields, "HugePages_Total"));
    }

    private static string[] Lines(string body) => body.Split('\n');
}
