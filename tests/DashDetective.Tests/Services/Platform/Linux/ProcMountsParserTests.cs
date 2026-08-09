using DashDetective.Services.Platform.Linux;
using DashDetective.Tests.Fakes;
using System.Linq;
using Xunit;

namespace DashDetective.Tests.Services.Platform.Linux;

/// <summary>Covers <see cref="ProcMountsParser"/>: the field layout, the octal escapes the kernel writes
/// into paths, and udev's different hex convention for the same job.</summary>
public class ProcMountsParserTests {
    private static MountEntry[] Parse(string body) =>
        [.. ProcMountsParser.Parse(body.Replace("\r\n", "\n").Split('\n'))];

    [Fact]
    public void Parse_ReadsTheDeviceMountPointAndFilesystem() {
        var root = Parse(ProcFixtures.ProcMounts).Single(e => e.MountPoint == "/");

        Assert.Equal("/dev/sda2", root.Device);
        Assert.Equal("ext4", root.FileSystem);
    }

    /// <summary>The whole file is read, pseudo-filesystems included — deciding what is a real volume is the
    /// provider's job, not the parser's.</summary>
    [Fact]
    public void Parse_KeepsPseudoFilesystems() =>
        Assert.Contains(Parse(ProcFixtures.ProcMounts), e => e.Device == "tmpfs");

    /// <summary>The separator is a space, so the kernel escapes any space inside a path. Left raw, a
    /// literal "\040" shows up in the Partitions table for most removable media.</summary>
    [Fact]
    public void Parse_UnescapesAnOctalEscapedMountPoint() =>
        Assert.Contains(Parse(ProcFixtures.ProcMounts), e => e.MountPoint == "/media/user/My Backup");

    [Theory]
    [InlineData(@"a\040b", "a b")]
    [InlineData(@"a\011b", "a\tb")]
    [InlineData(@"a\134b", @"a\b")]
    [InlineData(@"\040\040", "  ")]
    public void Unescape_ExpandsTheKernelsOctalEscapes(string field, string expected) =>
        Assert.Equal(expected, ProcMountsParser.Unescape(field));

    /// <summary>A backslash that starts no well-formed escape is a legal literal and is kept as
    /// written.</summary>
    [Theory]
    [InlineData(@"a\b", @"a\b")]
    [InlineData(@"a\99", @"a\99")]
    [InlineData(@"a\08", @"a\08")]
    [InlineData(@"trailing\", @"trailing\")]
    public void Unescape_LeavesAMalformedEscapeAlone(string field, string expected) =>
        Assert.Equal(expected, ProcMountsParser.Unescape(field));

    /// <summary>A row too short to carry a filesystem type is skipped rather than faulting the read.</summary>
    [Fact]
    public void Parse_SkipsAShortRow() =>
        Assert.Empty(Parse("/dev/sda1 /boot"));

    /// <summary>udev escapes the by-label symlink names in hex, not octal — the same job, a different
    /// convention, and reading one as the other mangles every labelled volume.</summary>
    [Theory]
    [InlineData(@"My\x20Backup", "My Backup")]
    [InlineData("Ubuntu", "Ubuntu")]
    [InlineData(@"a\x2Db", "a-b")]
    public void UnescapeUdev_ExpandsHexEscapes(string name, string expected) =>
        Assert.Equal(expected, ProcMountsParser.UnescapeUdev(name));
}
