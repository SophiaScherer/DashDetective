using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace DashDetective.Tabs.Toolkit;

/// <summary>
/// The real <see cref="IProcessLauncher"/> — **the only place in the app that starts a process**
/// (File Explorer's <c>IShellInterop.Open</c> aside, which opens what the user picked in its own tree and
/// deliberately fails invisibly).
///
/// Arguments always go through <c>ProcessStartInfo.ArgumentList</c>, never the joined
/// <c>Arguments</c> string, so nothing is concatenated into a command line: there is no quoting to get
/// wrong and no interpolation to exploit. Exceptions are left to propagate — wording a failure is
/// <see cref="ToolkitRunner"/>'s job, and swallowing here would cost it the reason.
///
/// <b>A declined prompt is silent on Linux, unlike Windows.</b> <c>runas</c> fails synchronously inside
/// <c>Process.Start</c> with <c>ERROR_CANCELLED</c>, which is why <see cref="ToolkitRunner"/> can word
/// it. <c>pkexec</c> forks and reports refusal as exit code 126 *after* the launch returns, and this
/// method deliberately does not wait: the one elevated Windows row (<c>sfc /scannow</c>) runs for many
/// minutes, and waiting would hold its Execution Log entry open for all of it. Reading 126 would also
/// not be conclusive — it cannot be told apart from the elevated program itself exiting 126.
/// </summary>
internal sealed class SystemProcessLauncher : IProcessLauncher {
    /// <summary>The program run to raise a privilege prompt on Linux. Resolved off the PATH like every
    /// other target here; polkit installs it at <c>/usr/bin/pkexec</c>.</summary>
    internal const string ElevationProgram = "pkexec";

    public void Launch(string fileName, IReadOnlyList<string> arguments, bool elevated) {
        using var process = Process.Start(
            BuildLaunchInfo(fileName, arguments, elevated, OperatingSystem.IsLinux()));
    }

    /// <summary>
    /// How one launch reaches the OS. Takes the platform explicitly so both arms are exercised from
    /// either dev machine — the <c>ToolkitPaths.Expand</c> seam shape.
    ///
    /// <b>The two platforms elevate through different mechanisms, not different flags.</b> Windows has a
    /// shell verb, so the target stays the target and <c>UseShellExecute</c> stays on. Linux has a
    /// wrapper program: <c>pkexec</c> becomes the file name and the real target becomes its first
    /// argument, with <c>UseShellExecute</c> off — left on, the launch would go to <c>xdg-open</c>,
    /// which cannot carry arguments at all.
    ///
    /// Arguments still go through <see cref="ProcessStartInfo.ArgumentList"/> on both paths, so nothing
    /// is concatenated into a command line and the safety boundary is unchanged.
    /// </summary>
    internal static ProcessStartInfo BuildLaunchInfo(
        string fileName, IReadOnlyList<string> arguments, bool elevated, bool linux) {
        if (elevated && linux) {
            var elevatedInfo = new ProcessStartInfo(ElevationProgram) { UseShellExecute = false };
            elevatedInfo.ArgumentList.Add(fileName);
            foreach (var argument in arguments)
                elevatedInfo.ArgumentList.Add(argument);

            return elevatedInfo;
        }

        var info = new ProcessStartInfo(fileName) { UseShellExecute = true };
        foreach (var argument in arguments)
            info.ArgumentList.Add(argument);

        // The shell verb that raises the UAC consent dialog. The app itself never elevates; each
        // elevated entry asks separately, and the user can always decline.
        if (elevated)
            info.Verb = "runas";

        return info;
    }

    public async Task<ProcessCapture> CaptureAsync(
        string fileName, IReadOnlyList<string> arguments, TimeSpan timeout) {
        var info = new ProcessStartInfo(fileName) {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (var argument in arguments)
            info.ArgumentList.Add(argument);

        // Encoding is left at the console default on purpose: forcing UTF-8 mangles the output of the
        // in-box tools (systeminfo, ipconfig) on the OEM code pages they actually write in.
        using var process = Process.Start(info)
            ?? throw new InvalidOperationException($"Could not start {fileName}.");

        // Both streams are drained concurrently and *without* the timeout token: a redirected pipe that
        // fills up blocks the child forever, and these must still complete after a kill (which closes
        // the pipes) so a timed-out command keeps whatever it managed to print.
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();

        var timedOut = false;
        using var cts = new CancellationTokenSource(timeout);
        try {
            await process.WaitForExitAsync(cts.Token);
        } catch (OperationCanceledException) {
            timedOut = true;
            TryKill(process);
        }

        var output = await stdout;
        var error = await stderr;
        return new ProcessCapture(timedOut ? -1 : process.ExitCode, output, error, timedOut);
    }

    // The process may have exited between the timeout firing and the kill landing, which throws.
    private static void TryKill(Process process) {
        try {
            process.Kill(entireProcessTree: true);
        } catch (Exception) {
            // Already gone, or access denied on a child — nothing further to do either way.
        }
    }
}
