using DashDetective.Services.Platform.Linux;
using DashDetective.Tests.Fakes;
using Xunit;

namespace DashDetective.Tests.Services.Platform.Linux;

/// <summary>Covers <see cref="ProcPidStatusParser"/>: the owner and resident size, the four-value
/// <c>Uid</c> line, and the degradations that must leave a process listed rather than drop it.</summary>
public class ProcPidStatusParserTests {
    private static ProcPidStatus Parse(string body) =>
        ProcPidStatusParser.Parse(body.Replace("\r\n", "\n").Split('\n'));

    /// <summary>The <c>Uid</c> line carries real, effective, saved-set and filesystem uids. Only the first
    /// is the owner; a reader that takes the line whole, or the last value, is wrong the moment a process
    /// has dropped privileges.</summary>
    [Fact]
    public void Parse_ReadsTheRealUidFromTheFourValueLine() =>
        Assert.Equal(1000, Parse(ProcFixtures.ProcPidStatus).Uid);

    /// <summary>The <c>kB</c> label is kibibytes, so 345678 kB is a visible ×1024.</summary>
    [Fact]
    public void Parse_ScalesVmRssToBytes() =>
        Assert.Equal(345678L * 1024, Parse(ProcFixtures.ProcPidStatus).ResidentBytes);

    /// <summary>A kernel thread has no address space, so the file carries no <c>VmRSS</c> at all. Zero is
    /// the honest answer; requiring the field would drop every kernel thread from the list.</summary>
    [Fact]
    public void Parse_KernelThread_HasRootOwnerAndNoResidentSize() {
        var status = Parse(ProcFixtures.ProcPidStatusKernelThread);

        Assert.Equal(0, status.Uid);
        Assert.Equal(0L, status.ResidentBytes);
    }

    /// <summary>Unknown owner is <c>null</c>, never 0 — 0 is root, and a denied read must not promote a
    /// user's process into the system group.</summary>
    [Fact]
    public void Parse_NothingReadable_ReportsAnUnknownOwnerRatherThanRoot() {
        var status = Parse("");

        Assert.Null(status.Uid);
        Assert.Equal(ProcPidStatus.None, status);
    }

    /// <summary>Each field degrades on its own: a file with an owner but no memory line still names the
    /// owner, and vice versa. Unparseable values read as absent rather than as zero-the-number.</summary>
    [Theory]
    [InlineData("Name:\tsh\nUid:\t1000\t1000\t1000\t1000\n", 1000, 0L)]
    [InlineData("Name:\tsh\nVmRSS:\t  1024 kB\n", null, 1024L * 1024)]
    [InlineData("Uid:\tnot-a-number\nVmRSS:\t  wrong kB\n", null, 0L)]
    public void Parse_PartialFile_DegradesPerField(string body, int? uid, long resident) {
        var status = Parse(body);

        Assert.Equal(uid, status.Uid);
        Assert.Equal(resident, status.ResidentBytes);
    }
}
