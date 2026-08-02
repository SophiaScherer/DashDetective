using DashDetective.Services.Diagnostics;
using System;
using System.ComponentModel;
using System.Threading.Tasks;

namespace DashDetective.Tabs.Toolkit;

/// <summary>
/// Runs a <see cref="ToolkitAction"/> and reports what happened, in words the Execution Log can show as
/// they are. The whole feature's execution goes through here.
///
/// **The safety boundary.** This takes a <see cref="ToolkitAction"/> — authored in
/// <see cref="ToolkitCatalog"/> — never a string a user typed, and arguments reach the OS as a list
/// rather than a command line (see <see cref="SystemProcessLauncher"/>). There is no free-form command
/// entry anywhere in the app, so there is nothing to inject into.
///
/// Never throws: a missing tool, a refused launch, a dismissed UAC prompt and an overrunning command all
/// become failure results, logged through <see cref="Log.Warn"/> like every other soft-fail here.
/// </summary>
public sealed class ToolkitRunner {
    // ERROR_CANCELLED — the user dismissed the UAC consent dialog. Expected, not a fault.
    private const int ElevationCancelledCode = 1223;

    private readonly IProcessLauncher _launcher;

    public ToolkitRunner() : this(new SystemProcessLauncher()) { }

    /// <param name="launcher">The process seam. Faked in tests; production uses the system launcher.</param>
    internal ToolkitRunner(IProcessLauncher launcher) => _launcher = launcher;

    /// <summary>
    /// Runs the action off the UI thread and returns a display-ready result. Even the launch path is
    /// moved off the thread: an elevated start blocks until the user answers the UAC prompt, which would
    /// otherwise freeze the window behind it.
    /// </summary>
    public async Task<ToolkitRunResult> RunAsync(ToolkitAction action) {
        // A documentation entry may only ever reach the browser. Checked here rather than when the
        // catalog is built, so a typo surfaces as a visible refusal instead of a startup crash.
        if (action.Kind == ToolkitActionKind.OpenUrl && !IsHttps(action.Target))
            return ToolkitRunResult.Failure(ToolkitOutputFormatter.BlockedUrl(action.Target));

        return action.CapturesOutput
            ? await CaptureAsync(action).ConfigureAwait(false)
            : await Task.Run(() => Launch(action)).ConfigureAwait(false);
    }

    private ToolkitRunResult Launch(ToolkitAction action) {
        try {
            _launcher.Launch(Resolve(action.Target), action.Arguments, action.RequiresElevation);
            return ToolkitRunResult.Ok(SuccessNote(action.Kind));
        } catch (Win32Exception error) when (error.NativeErrorCode == ElevationCancelledCode) {
            // Declining the prompt is a decision, not a fault — no log noise for it.
            return ToolkitRunResult.Failure(ToolkitOutputFormatter.ElevationCancelled);
        } catch (Exception error) {
            Log.Warn($"Toolkit launch failed: {action.Target}", error);
            return ToolkitRunResult.Failure(ToolkitOutputFormatter.Failed(error.Message));
        }
    }

    private async Task<ToolkitRunResult> CaptureAsync(ToolkitAction action) {
        try {
            var capture = await _launcher
                .CaptureAsync(Resolve(action.Target), action.Arguments, action.Timeout)
                .ConfigureAwait(false);

            var body = ToolkitOutputFormatter.Combine(capture.StandardOutput, capture.StandardError);

            if (capture.TimedOut)
                return ToolkitRunResult.Failure(
                    ToolkitOutputFormatter.TimedOut(action.Timeout, body), capture.ExitCode);

            if (capture.ExitCode != 0)
                return ToolkitRunResult.Failure(
                    ToolkitOutputFormatter.ExitedWith(capture.ExitCode, body), capture.ExitCode);

            return ToolkitRunResult.Ok(
                body.Length == 0 ? ToolkitOutputFormatter.NoOutput : body, capture.ExitCode);
        } catch (Exception error) {
            Log.Warn($"Toolkit capture failed: {action.Target}", error);
            return ToolkitRunResult.Failure(ToolkitOutputFormatter.Failed(error.Message));
        }
    }

    // Only the target is expanded — a parameterised entry's argument is user-supplied and stays literal.
    // Shared with the rows, which resolve the same target to offer it to the in-app File Explorer.
    private static string Resolve(string target) => ToolkitPaths.Resolve(target);

    private static bool IsHttps(string target) =>
        target.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

    private static string SuccessNote(ToolkitActionKind kind) => kind switch {
        ToolkitActionKind.OpenPath or ToolkitActionKind.OpenUrl => ToolkitOutputFormatter.Opened,
        ToolkitActionKind.Elevated => ToolkitOutputFormatter.LaunchedElevated,
        _ => ToolkitOutputFormatter.Launched,
    };
}
