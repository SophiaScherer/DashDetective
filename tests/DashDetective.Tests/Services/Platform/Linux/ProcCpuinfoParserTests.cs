using DashDetective.Services.Platform.Linux;
using DashDetective.Tests.Fakes;
using System.Collections.Generic;
using Xunit;

namespace DashDetective.Tests.Services.Platform.Linux;

/// <summary>Covers <see cref="ProcCpuinfoParser"/>: the tab separation that defeats a fixed-layout split,
/// the blank-line blocking that decides how many processors the file describes, and the trailing newline
/// that must not add a phantom one.</summary>
public class ProcCpuinfoParserTests {
    private static IReadOnlyList<IReadOnlyDictionary<string, string>> Parse(string body) =>
        ProcCpuinfoParser.Parse(body.Split('\n'));

    /// <summary>The real file separates key from value with a varying number of tabs, so the parser trims
    /// around the colon rather than splitting on a column. A fixture written with spaces would pass
    /// either way, which is why <see cref="ProcFixtures.ProcCpuInfo"/> uses real tabs.</summary>
    [Fact]
    public void Parse_ReadsTabSeparatedKeysAndValues() {
        var blocks = Parse(ProcFixtures.ProcCpuInfo);

        Assert.Equal(
            "Intel(R) Core(TM) i7-9700K CPU @ 3.60GHz",
            ProcCpuinfoParser.Value(blocks[0], "model name"));
        Assert.Equal("3600.000", ProcCpuinfoParser.Value(blocks[0], "cpu MHz"));
    }

    /// <summary>One block per logical processor, split on blank lines.</summary>
    [Fact]
    public void Parse_ReturnsOneBlockPerProcessor() {
        Assert.Equal(2, Parse(ProcFixtures.ProcCpuInfo).Count);
        Assert.Equal(8, Parse(ProcFixtures.AmdCpuInfo).Count);
    }

    /// <summary>The file ends with a blank line, which closes the last block rather than opening an empty
    /// ninth one — a phantom processor would inflate every thread count by one.</summary>
    [Fact]
    public void Parse_TrailingBlankLine_DoesNotAddAnEmptyBlock() {
        var blocks = Parse(ProcFixtures.AmdCpuInfo + "\n\n\n");

        Assert.Equal(8, blocks.Count);
        Assert.All(blocks, block => Assert.NotEmpty(block));
    }

    /// <summary>Blocks keep file order, so the first is genuinely processor 0.</summary>
    [Fact]
    public void Parse_KeepsBlocksInFileOrder() {
        var blocks = Parse(ProcFixtures.AmdCpuInfo);

        Assert.Equal("0", ProcCpuinfoParser.Value(blocks[0], "processor"));
        Assert.Equal("7", ProcCpuinfoParser.Value(blocks[7], "processor"));
    }

    /// <summary>Keys are prose rather than identifiers, and their casing has drifted across architectures
    /// ("cpu MHz"), so lookups are case-insensitive.</summary>
    [Fact]
    public void Parse_MatchesKeysCaseInsensitively() =>
        Assert.Equal("3600.000", ProcCpuinfoParser.Value(Parse(ProcFixtures.ProcCpuInfo)[0], "CPU MHz"));

    /// <summary>An architecture that writes none of the expected keys still parses — it just yields
    /// blocks the callers find nothing in, which is how ARM degrades to "—".</summary>
    [Fact]
    public void Parse_UnrecognisedKeys_AreKeptRatherThanFailingTheParse() {
        var block = Assert.Single(Parse("CPU implementer\t: 0x41\nCPU part\t: 0xd0c"));

        Assert.Equal("0x41", ProcCpuinfoParser.Value(block, "CPU implementer"));
        Assert.Equal("", ProcCpuinfoParser.Value(block, "model name"));
    }

    [Fact]
    public void Parse_SkipsLinesWithNoColon() {
        var block = Assert.Single(Parse("processor\t: 0\nthis line has no colon"));

        Assert.Equal("0", Assert.Single(block).Value);
    }

    [Fact]
    public void Parse_EmptyFile_YieldsNoBlocks() => Assert.Empty(ProcCpuinfoParser.Parse([]));
}
