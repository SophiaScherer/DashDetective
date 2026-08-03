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
/// </summary>
internal sealed class SystemProcessLauncher : IProcessLauncher {
    public void Launch(string fileName, IReadOnlyList<string> arguments, bool elevated) {
        var info = new ProcessStartInfo(fileName) { UseShellExecute = true };
        foreach (var argument in arguments)
            info.ArgumentList.Add(argument);

        // The shell verb that raises the UAC consent dialog. The app itself never elevates; each
        // elevated entry asks separately, and the user can always decline.
        if (elevated)
            info.Verb = "runas";

        using var process = Process.Start(info);
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
