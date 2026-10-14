using DashDetective.Tabs.Toolkit;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DashDetective.Tests.Fakes;

/// <summary>
/// Hand-rolled <see cref="IProcessLauncher"/> for headless tests: records what it was asked to start
/// and hands back whatever the test staged, so <see cref="ToolkitRunner"/>'s routing and wording can be
/// pinned without any process ever existing.
/// </summary>
internal sealed class FakeProcessLauncher : IProcessLauncher {
    /// <summary>Every launch/capture, in order, exactly as the runner asked for it.</summary>
    public List<LaunchRecord> Calls { get; } = [];

    /// <summary>Thrown by the next call, if set — how a test stages a missing tool or a declined
    /// elevation prompt.</summary>
    public Exception? ThrowOnCall { get; set; }

    /// <summary>What the next capture reports back. Defaults to a clean, silent run.</summary>
    public ProcessCapture NextCapture { get; set; } = new(0, "", "", false);

    /// <summary>Whether a capture stays in flight until <see cref="Complete"/> (or a cancel) ends it,
    /// instead of returning <see cref="NextCapture"/> at once.</summary>
    public bool HoldCaptures { get; set; }

    /// <summary>What a held capture had printed when a cancel killed it.</summary>
    public string PartialOutput { get; set; } = "";

    /// <summary>A launcher that never looks at its token — the cancel is a no-op and the capture ends only
    /// when the test completes it.</summary>
    public bool IgnoreCancellation { get; set; }

    /// <summary>A launcher that throws <see cref="OperationCanceledException"/> on cancel rather than
    /// reporting it as a result.</summary>
    public bool ThrowOnCancellation { get; set; }

    private TaskCompletionSource<ProcessCapture>? _held;

    /// <summary>Ends a held capture with the given result, as if the process had finished on its own.</summary>
    public void Complete(ProcessCapture capture) => _held?.TrySetResult(capture);

    /// <summary>The only call, when a test expects exactly one.</summary>
    public LaunchRecord Single => Calls.Count == 1
        ? Calls[0]
        : throw new InvalidOperationException($"Expected exactly one call, saw {Calls.Count}.");

    public void Launch(string fileName, IReadOnlyList<string> arguments, bool elevated) {
        Calls.Add(new LaunchRecord(fileName, [.. arguments], elevated, Captured: false, null));
        Throw();
    }

    public Task<ProcessCapture> CaptureAsync(
        string fileName, IReadOnlyList<string> arguments, TimeSpan timeout,
        CancellationToken cancellationToken = default) {
        Calls.Add(new LaunchRecord(fileName, [.. arguments], Elevated: false, Captured: true, timeout));
        Throw();
        if (!HoldCaptures)
            return Task.FromResult(NextCapture);

        var held = new TaskCompletionSource<ProcessCapture>(TaskCreationOptions.RunContinuationsAsynchronously);
        _held = held;
        if (!IgnoreCancellation)
            cancellationToken.Register(() => {
                if (ThrowOnCancellation)
                    held.TrySetException(new OperationCanceledException(cancellationToken));
                else
                    held.TrySetResult(new ProcessCapture(-1, PartialOutput, "", TimedOut: false, Cancelled: true));
            });

        return held.Task;
    }

    private void Throw() {
        if (ThrowOnCall is not { } error)
            return;

        ThrowOnCall = null; // one-shot, so a test can stage a single failure
        throw error;
    }

    /// <summary>One thing the runner asked to be started.</summary>
    internal sealed record LaunchRecord(
        string FileName, IReadOnlyList<string> Arguments, bool Elevated, bool Captured,
        TimeSpan? Timeout);
}
