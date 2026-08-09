using DashDetective.Services.Platform.Linux;
using DashDetective.Tests.Fakes;
using Xunit;

namespace DashDetective.Tests.Services.Platform.Linux;

/// <summary>Covers <see cref="ProcPidStatParser"/>: the five fields it lifts out of a process's
/// <c>/proc/[pid]/stat</c>, and the parenthesised <c>comm</c> that defeats a whole-line split.</summary>
public class ProcPidStatParserTests {
    private static ProcPidStat Parse(string body) {
        Assert.True(ProcPidStatParser.TryParse(body, out var stat));
        return stat;
    }

    [Fact]
    public void TryParse_ReadsTheFieldsARowIsBuiltFrom() {
        var stat = Parse(ProcFixtures.ProcPidStat);

        Assert.Equal("gnome-shell", stat.Comm);
        Assert.Equal('S', stat.State);
        Assert.Equal(1, stat.ParentPid);
        Assert.Equal(14, stat.ThreadCount);
    }

    /// <summary>CPU time is <c>utime + stime</c>, because the two are only ever wanted together and the
    /// USER_HZ conversion should happen in one place. The fixture is 1200 + 340.</summary>
    [Fact]
    public void TryParse_SumsUserAndSystemTicks() =>
        Assert.Equal(1540UL, Parse(ProcFixtures.ProcPidStat).CpuTicks);

    /// <summary>The one that matters: <c>comm</c> is parenthesised and may hold spaces <b>and</b> nested
    /// parentheses. Splitting on the last <c>)</c> is what keeps every following field on its own index.</summary>
    [Fact]
    public void TryParse_HostileComm_KeepsEveryLaterFieldOnItsOwnIndex() {
        var stat = Parse(ProcFixtures.ProcPidStatHostileName);

        Assert.Equal("Web (Content) 2", stat.Comm);
        Assert.Equal('S', stat.State);
        Assert.Equal(1300, stat.ParentPid);
        Assert.Equal(3500UL, stat.CpuTicks);
        Assert.Equal(26, stat.ThreadCount);
    }

    /// <summary>A kernel thread: parent 2, one thread, and the <c>I</c> (idle) state that only kernel
    /// workers report.</summary>
    [Fact]
    public void TryParse_KernelThread_ReadsItsKthreaddParent() {
        var stat = Parse(ProcFixtures.ProcPidStatKernelThread);

        Assert.Equal("kworker/3:1H-events_highpri", stat.Comm);
        Assert.Equal('I', stat.State);
        Assert.Equal(2, stat.ParentPid);
        Assert.Equal(1, stat.ThreadCount);
    }

    /// <summary>A torn read is the common case, not the exotic one — <c>/proc/[pid]</c> vanishes mid-walk
    /// constantly — so every malformed body has to fail cleanly rather than yield half a row.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("412 (gnome-shell) S 1 412 412 0 -1")]              // truncated before num_threads
    [InlineData("412 gnome-shell S 1 412 412 0 -1 0 0 0 0 0 0 0 0 0 20 0 14")]  // no parentheses
    [InlineData("412 (gnome-shell) Sleeping 1 412 412 0 -1 0 0 0 0 0 0 0 0 0 20 0 14")] // state not one char
    [InlineData("412 (gnome-shell) S x 412 412 0 -1 0 0 0 0 0 0 0 0 0 20 0 14")] // non-numeric parent
    public void TryParse_MalformedBody_Fails(string? body) =>
        Assert.False(ProcPidStatParser.TryParse(body, out _));
}
