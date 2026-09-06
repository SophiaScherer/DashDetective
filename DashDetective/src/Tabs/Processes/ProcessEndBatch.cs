using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DashDetective.Tabs.Processes;

/// <summary>One process End task could not end, and why.</summary>
internal readonly record struct ProcessEndFailure(int Pid, ProcessEndOutcome Reason);

/// <summary>What a batch actually achieved: the PIDs confirmed gone, and the ones that are not.</summary>
internal sealed record ProcessEndReport(
    IReadOnlyList<int> Exited, IReadOnlyList<ProcessEndFailure> Failures);

/// <summary>
/// Ends a set of processes and confirms they went. <c>Process.Kill</c> only <i>requests</i> termination,
/// so accepting one is not proof of anything — the rows used to be dropped on that alone, which is how a
/// process that never died disappeared from the list and returned on the next poll.
///
/// Two passes, because the budget is shared: every kill is issued first, then the survivors are watched
/// together until one deadline. Waiting per process would cost thirty budgets for thirty processes.
/// </summary>
internal sealed class ProcessEndBatch {
    /// <summary>How long the whole batch has to prove it worked. Long enough for a large process to
    /// unwind, short enough that the button doesn't feel stuck.</summary>
    internal static readonly TimeSpan DefaultBudget = TimeSpan.FromSeconds(3);

    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);

    private readonly IProcessTerminator _terminator;
    private readonly TimeSpan _budget;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;

    /// <summary>The budget and the delay are injectable so the wait can be tested without spending it.</summary>
    internal ProcessEndBatch(IProcessTerminator terminator, TimeSpan? budget = null,
                             Func<TimeSpan, CancellationToken, Task>? delay = null) {
        _terminator = terminator;
        _budget = budget ?? DefaultBudget;
        _delay = delay ?? Task.Delay;
    }

    internal async Task<ProcessEndReport> EndAsync(IReadOnlyList<int> pids, CancellationToken token) {
        var exited = new List<int>(pids.Count);
        var failures = new List<ProcessEndFailure>();
        var pending = new List<int>(pids.Count);

        foreach (var pid in pids) {
            switch (_terminator.Request(pid)) {
                // Nothing to wait for and nothing to say: the row goes either way.
                case ProcessEndOutcome.AlreadyGone: exited.Add(pid); break;
                case ProcessEndOutcome.Ended: pending.Add(pid); break;
                case var refused: failures.Add(new ProcessEndFailure(pid, refused)); break;
            }
        }

        // Most processes are gone by the time the last kill is issued, so check before waiting at all.
        Collect(pending, exited);
        for (var elapsed = TimeSpan.Zero; pending.Count > 0 && elapsed < _budget; elapsed += PollInterval) {
            await _delay(PollInterval, token).ConfigureAwait(false);
            Collect(pending, exited);
        }

        // Accepted the kill and outlived the budget — reported as a failure so the row stays put.
        foreach (var pid in pending)
            failures.Add(new ProcessEndFailure(pid, ProcessEndOutcome.Failed));

        return new ProcessEndReport(exited, failures);
    }

    /// <summary>Moves everything that has gone out of <paramref name="pending"/>.</summary>
    private void Collect(List<int> pending, List<int> exited) {
        var kept = 0;
        for (var i = 0; i < pending.Count; i++) {
            if (_terminator.HasExited(pending[i]))
                exited.Add(pending[i]);
            else
                pending[kept++] = pending[i];
        }

        pending.RemoveRange(kept, pending.Count - kept);
    }
}
