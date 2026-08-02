using DashDetective.Tabs.Toolkit;
using System;
using System.Collections.Generic;
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

    /// <summary>The only call, when a test expects exactly one.</summary>
    public LaunchRecord Single => Calls.Count == 1
        ? Calls[0]
        : throw new InvalidOperationException($"Expected exactly one call, saw {Calls.Count}.");

    public void Launch(string fileName, IReadOnlyList<string> arguments, bool elevated) {
        Calls.Add(new LaunchRecord(fileName, [.. arguments], elevated, Captured: false, null));
        Throw();
    }

    public Task<ProcessCapture> CaptureAsync(
        string fileName, IReadOnlyList<string> arguments, TimeSpan timeout) {
        Calls.Add(new LaunchRecord(fileName, [.. arguments], Elevated: false, Captured: true, timeout));
        Throw();
        return Task.FromResult(NextCapture);
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
