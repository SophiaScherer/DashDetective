using DashDetective.Services.Platform.Linux;
using Xunit;

namespace DashDetective.Tests.Services.Platform.Linux;

/// <summary>Covers <see cref="ProcStatParser"/>: the column-count defensiveness, the two accounting
/// decisions (steal is busy, guest is already folded into user), and the busy-fraction math.</summary>
public class ProcStatParserTests {
    [Fact]
    public void TryParseCpuLine_TenColumns_SumsUserThroughSteal() {
        // user 1000, nice 100, system 500, idle 8000, iowait 300, irq 0, softirq 100, steal 0.
        var parsed = ProcStatParser.TryParseCpuLine(
            "cpu  1000 100 500 8000 300 0 100 0 0 0", out var label, out var busy, out var total);

        Assert.True(parsed);
        Assert.Equal("cpu", label);
        Assert.Equal(10000UL, total);
        Assert.Equal(1700UL, busy); // total − (idle 8000 + iowait 300)
    }

    /// <summary>The pre-2.6.11 form has no steal/guest columns; a parser that assumes ten would read past
    /// the end or reject the line outright.</summary>
    [Fact]
    public void TryParseCpuLine_SevenColumns_ParsesWhatIsThere() {
        var parsed = ProcStatParser.TryParseCpuLine(
            "cpu  1000 100 500 8000 300 0 100", out _, out var busy, out var total);

        Assert.True(parsed);
        Assert.Equal(10000UL, total);
        Assert.Equal(1700UL, busy);
    }

    /// <summary>The kernel folds guest time into user, so counting columns 8 and 9 would double it. The
    /// large guest figures here must not move either counter.</summary>
    [Fact]
    public void TryParseCpuLine_GuestColumns_AreExcludedFromTheTotal() {
        var parsed = ProcStatParser.TryParseCpuLine(
            "cpu  1000 100 500 8000 300 0 100 0 9999 8888", out _, out var busy, out var total);

        Assert.True(parsed);
        Assert.Equal(10000UL, total);
        Assert.Equal(1700UL, busy);
    }

    /// <summary>Steal is time the hypervisor took; the guest was not idle for it, and counting it is what
    /// makes the reading agree with <c>top</c> inside a VM.</summary>
    [Fact]
    public void TryParseCpuLine_Steal_CountsAsBusy() {
        var parsed = ProcStatParser.TryParseCpuLine(
            "cpu  1000 100 500 8000 300 0 100 2000 0 0", out _, out var busy, out var total);

        Assert.True(parsed);
        Assert.Equal(12000UL, total);
        Assert.Equal(3700UL, busy); // the extra 2000 lands on the busy side
    }

    [Fact]
    public void TryParseCpuLine_PerCoreLine_KeepsItsLabel() {
        var parsed = ProcStatParser.TryParseCpuLine(
            "cpu3 250 25 125 2000 75 0 25 0 0 0", out var label, out var busy, out var total);

        Assert.True(parsed);
        Assert.Equal("cpu3", label);
        Assert.Equal(2500UL, total);
        Assert.Equal(425UL, busy);
    }

    /// <summary>The aggregate line is double-spaced and per-core lines are single-spaced; both split the
    /// same way, so a caller can tell them apart by label rather than by layout.</summary>
    [Fact]
    public void TryParseCpuLine_IgnoresRunsOfSpaces() {
        Assert.True(ProcStatParser.TryParseCpuLine("cpu     1 2 3 4", out var label, out _, out var total));
        Assert.Equal("cpu", label);
        Assert.Equal(10UL, total);
    }

    [Theory]
    [InlineData("intr 45678901 22 1234 0 0")]      // a non-cpu line from the trailer
    [InlineData("ctxt 98765432")]
    [InlineData("cpu  1000 100 500")]              // truncated: no idle column
    [InlineData("cpu  1000 100 abc 8000 300")]     // non-numeric field
    [InlineData("cpu  1000 100 -5 8000 300")]      // negative: not a jiffy count
    [InlineData("")]
    public void TryParseCpuLine_Rejects(string line) {
        Assert.False(ProcStatParser.TryParseCpuLine(line, out var label, out var busy, out var total));
        Assert.Equal("", label);
        Assert.Equal(0UL, busy);
        Assert.Equal(0UL, total);
    }

    [Fact]
    public void ComputeUsage_HalfBusy_ReturnsFifty() {
        Assert.Equal(50.0, ProcStatParser.ComputeUsage(busyDelta: 500, totalDelta: 1000));
    }

    [Fact]
    public void ComputeUsage_FullyBusy_ReturnsHundred() {
        Assert.Equal(100.0, ProcStatParser.ComputeUsage(busyDelta: 1000, totalDelta: 1000));
    }

    [Fact]
    public void ComputeUsage_FullyIdle_ReturnsZero() {
        Assert.Equal(0.0, ProcStatParser.ComputeUsage(busyDelta: 0, totalDelta: 1000));
    }

    /// <summary>Two reads inside the same jiffy leave nothing to divide by.</summary>
    [Fact]
    public void ComputeUsage_EmptyInterval_ReturnsZero() {
        Assert.Equal(0.0, ProcStatParser.ComputeUsage(busyDelta: 0, totalDelta: 0));
    }

    [Fact]
    public void ComputeUsage_BusyExceedingTotal_ClampsToHundred() {
        Assert.Equal(100.0, ProcStatParser.ComputeUsage(busyDelta: 1500, totalDelta: 1000));
    }
}
