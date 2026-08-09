using DashDetective.Services.Platform.Linux;
using DashDetective.Tests.Fakes;
using System.Linq;
using Xunit;

namespace DashDetective.Tests.Services.Platform.Linux;

/// <summary>Covers <see cref="ProcPids"/>, the listing both the Performance tab's process count and the
/// Processes tab's walk start from — so what counts as a process is decided once.</summary>
public class ProcPidsTests {
    /// <summary>A <c>/proc</c> with four PID directories among the named entries a real listing is full
    /// of, including the <c>self</c> link that looks like a directory but names no process.</summary>
    private static FakeProcFileSystem Tree() =>
        new FakeProcFileSystem()
            .WithFile("/proc/1/stat", ProcFixtures.ProcPidStat)
            .WithFile("/proc/2/stat", ProcFixtures.ProcPidStatKernelThread)
            .WithFile("/proc/412/stat", ProcFixtures.ProcPidStat)
            .WithFile("/proc/1337/stat", ProcFixtures.ProcPidStatHostileName)
            .WithFile("/proc/self/stat", ProcFixtures.ProcPidStat)
            .WithFile("/proc/thread-self/stat", ProcFixtures.ProcPidStat)
            .WithFile("/proc/meminfo", ProcFixtures.ProcMeminfo)
            .WithFile("/proc/uptime", "1234.56 4321.00");

    /// <summary>Ordered in the assertion, not by the subject: a directory listing has no guaranteed order
    /// and no caller may rely on one.</summary>
    [Fact]
    public void List_KeepsOnlyTheNumericEntries() =>
        Assert.Equal([1, 2, 412, 1337], ProcPids.List(Tree()).Order());

    /// <summary>An unreadable <c>/proc</c> yields nothing rather than throwing — the seam's empty contract,
    /// which callers read as "unknown", never as "no processes".</summary>
    [Fact]
    public void List_NothingStaged_IsEmpty() =>
        Assert.Empty(ProcPids.List(new FakeProcFileSystem()));
}
