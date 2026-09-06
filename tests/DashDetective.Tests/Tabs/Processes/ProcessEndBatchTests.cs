using DashDetective.Tabs.Processes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DashDetective.Tests.Tabs.Processes;

/// <summary>Pins that a batch confirms its kills rather than trusting them: accepting a termination is
/// not proof of exit, one refusal does not stop the rest, and the wait is one budget for the whole batch
/// rather than one per process.</summary>
public class ProcessEndBatchTests {
    private static readonly TimeSpan Budget = TimeSpan.FromMilliseconds(300);

    /// <summary>Answers whatever it is told to, and counts the waits so a per-process budget would show
    /// up as a bigger number.</summary>
    private sealed class Terminator : IProcessTerminator {
        public List<int> Requested { get; } = [];
        public Dictionary<int, ProcessEndOutcome> Outcomes { get; } = [];

        /// <summary>PIDs that accept the kill and never exit.</summary>
        public HashSet<int> Lingering { get; } = [];

        /// <summary>PIDs that exit only after this many checks, so the wait has to actually wait.</summary>
        public Dictionary<int, int> ExitsAfter { get; } = [];

        private readonly Dictionary<int, int> _checks = [];

        public ProcessEndOutcome Request(int pid) {
            Requested.Add(pid);
            return Outcomes.TryGetValue(pid, out var outcome) ? outcome : ProcessEndOutcome.Ended;
        }

        public bool HasExited(int pid) {
            _checks[pid] = _checks.TryGetValue(pid, out var seen) ? seen + 1 : 1;
            if (Lingering.Contains(pid))
                return false;
            return !ExitsAfter.TryGetValue(pid, out var needed) || _checks[pid] > needed;
        }
    }

    /// <summary>A batch whose waits cost nothing, so a three-second budget runs instantly.</summary>
    private static ProcessEndBatch Batch(Terminator terminator, out int[] waits) {
        var counter = new int[1];
        waits = counter;
        return new ProcessEndBatch(terminator, Budget, (_, _) => {
            counter[0]++;
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task EndAsync_EverythingExits_ReportsNoFailures() {
        var terminator = new Terminator();
        var report = await Batch(terminator, out _).EndAsync([1, 2, 3], CancellationToken.None);

        Assert.Equal([1, 2, 3], report.Exited);
        Assert.Empty(report.Failures);
    }

    [Fact]
    public async Task EndAsync_OneDenied_StillAttemptsAndEndsTheRest() {
        var terminator = new Terminator();
        terminator.Outcomes[2] = ProcessEndOutcome.Denied;

        var report = await Batch(terminator, out _).EndAsync([1, 2, 3], CancellationToken.None);

        Assert.Equal([1, 2, 3], terminator.Requested);
        Assert.Equal([1, 3], report.Exited);
        Assert.Equal(new ProcessEndFailure(2, ProcessEndOutcome.Denied), Assert.Single(report.Failures));
    }

    /// <summary>The bug this class exists for: Process.Kill only requests termination, so a process that
    /// accepts and then does not go must be a failure, not a removed row.</summary>
    [Fact]
    public async Task EndAsync_AcceptedButNeverExits_IsReportedFailed() {
        var terminator = new Terminator();
        terminator.Lingering.Add(2);

        var report = await Batch(terminator, out _).EndAsync([1, 2], CancellationToken.None);

        Assert.Equal([1], report.Exited);
        Assert.Equal(new ProcessEndFailure(2, ProcessEndOutcome.Failed), Assert.Single(report.Failures));
    }

    [Fact]
    public async Task EndAsync_SlowToExit_IsWaitedForAndSucceeds() {
        var terminator = new Terminator();
        terminator.ExitsAfter[2] = 2;

        var report = await Batch(terminator, out _).EndAsync([1, 2], CancellationToken.None);

        Assert.Equal([1, 2], report.Exited.OrderBy(pid => pid));
        Assert.Empty(report.Failures);
    }

    /// <summary>An already-exited process is neither a failure nor something to wait for — its row goes
    /// and nothing is said about it.</summary>
    [Fact]
    public async Task EndAsync_AlreadyGone_CountsAsExitedWithoutWaiting() {
        var terminator = new Terminator();
        terminator.Outcomes[1] = ProcessEndOutcome.AlreadyGone;

        var report = await Batch(terminator, out var waits).EndAsync([1], CancellationToken.None);

        Assert.Equal([1], report.Exited);
        Assert.Empty(report.Failures);
        Assert.Equal(0, waits[0]);
    }

    /// <summary>The whole batch shares one budget. Ten lingering processes must cost the same wait as
    /// one, or thirty selected rows would hang the page for thirty budgets.</summary>
    [Fact]
    public async Task EndAsync_SeveralLingering_SpendsOneBudgetNotOnePerProcess() {
        var one = new Terminator();
        one.Lingering.Add(1);
        await Batch(one, out var singleWaits).EndAsync([1], CancellationToken.None);

        var many = new Terminator();
        for (var pid = 1; pid <= 10; pid++)
            many.Lingering.Add(pid);
        await Batch(many, out var manyWaits).EndAsync([.. Enumerable.Range(1, 10)], CancellationToken.None);

        Assert.Equal(singleWaits[0], manyWaits[0]);
    }

    [Fact]
    public async Task EndAsync_EverythingGoesAtOnce_NeverWaits() {
        var terminator = new Terminator();
        await Batch(terminator, out var waits).EndAsync([1, 2, 3], CancellationToken.None);

        Assert.Equal(0, waits[0]);
    }

    [Fact]
    public async Task EndAsync_NothingToEnd_ReportsNothing() {
        var report = await Batch(new Terminator(), out _).EndAsync([], CancellationToken.None);

        Assert.Empty(report.Exited);
        Assert.Empty(report.Failures);
    }
}
