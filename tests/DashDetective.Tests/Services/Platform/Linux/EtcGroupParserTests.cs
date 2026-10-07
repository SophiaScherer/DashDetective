using DashDetective.Services.Platform.Linux;
using System.Collections.Generic;
using Xunit;

namespace DashDetective.Tests.Services.Platform.Linux;

/// <summary>Covers <see cref="EtcGroupParser"/>: gids are found by group name, and a comment, a short line or
/// a bad gid is skipped rather than fatal.</summary>
public class EtcGroupParserTests {
    private static readonly HashSet<string> Admin = ["sudo", "wheel"];

    [Fact]
    public void GidsNamed_FindsEachNamedGroup() {
        var gids = EtcGroupParser.GidsNamed(["root:x:0:", "sudo:x:27:alice,bob", "wheel:x:10:", "users:x:100:"], Admin);

        Assert.Equal(new HashSet<int> { 27, 10 }, gids);
    }

    [Fact]
    public void GidsNamed_SkipsCommentsShortLinesAndBadGids() {
        var gids = EtcGroupParser.GidsNamed(["# sudo:x:27:", "sudo", "wheel:x:ten:", "sudo:x:27:"], Admin);

        Assert.Equal(new HashSet<int> { 27 }, gids);
    }

    // Names are compared exactly: a group called "sudoers" grants nothing.
    [Fact]
    public void GidsNamed_MatchesWholeNamesOnly() =>
        Assert.Empty(EtcGroupParser.GidsNamed(["sudoers:x:500:", "Sudo:x:501:"], Admin));
}
