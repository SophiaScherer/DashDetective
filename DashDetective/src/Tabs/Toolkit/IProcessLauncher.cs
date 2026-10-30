using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DashDetective.Tabs.Toolkit;

/// <summary>What one captured run reported back.</summary>
/// <param name="ExitCode">The process's exit code, or -1 when it was killed on timeout or cancel.</param>
/// <param name="StandardOutput">Everything the command wrote to stdout, raw.</param>
/// <param name="StandardError">Everything it wrote to stderr, raw.</param>
/// <param name="TimedOut">Whether the timeout killed it rather than it finishing on its own.</param>
/// <param name="Cancelled">Whether the caller's token killed it. Exclusive with <paramref name="TimedOut"/>.</param>
public sealed record ProcessCapture(
    int ExitCode, string StandardOutput, string StandardError, bool TimedOut, bool Cancelled = false);

/// <summary>
/// Minimal seam over starting a process, so <see cref="ToolkitRunner"/>'s decisions — which path an
/// action takes, how a failure is worded, what a timeout produces — can be exercised headlessly. The
/// <c>IUiTimer</c> precedent: production always uses <see cref="SystemProcessLauncher"/>, so behaviour
/// is unchanged; this exists purely for testability.
///
/// Both methods take the arguments as a **list** and may throw (a missing file, no association, a
/// dismissed UAC prompt); the runner is what turns those into results.
/// </summary>
internal interface IProcessLauncher {
    /// <summary>Hands the target to the shell and returns once it has been started, not once it has
    /// finished. Blocks while an elevation prompt is on screen, so callers keep it off the UI thread.</summary>
    void Launch(string fileName, IReadOnlyList<string> arguments, bool elevated);

    /// <summary>Runs the target with its streams redirected, waiting up to <paramref name="timeout"/>
    /// and killing it if it overruns or <paramref name="cancellationToken"/> fires. Whatever it printed
    /// before being killed still comes back, and a cancel is reported as a result, not thrown.</summary>
    Task<ProcessCapture> CaptureAsync(
        string fileName, IReadOnlyList<string> arguments, TimeSpan timeout,
        CancellationToken cancellationToken = default);
}
