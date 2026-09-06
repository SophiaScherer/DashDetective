using DashDetective.Services.Diagnostics;
using System;
using System.ComponentModel;
using System.Diagnostics;

namespace DashDetective.Tabs.Processes;

/// <summary>The real terminator. Managed and platform-neutral, so unlike <see cref="IProcessInterop"/>
/// there is nothing here to resolve per platform, and it touches no annotated platform API — hence no
/// <c>[SupportedOSPlatform]</c>.</summary>
internal sealed class ProcessTerminator : IProcessTerminator {
    public ProcessEndOutcome Request(int pid) {
        try {
            using var process = Process.GetProcessById(pid);
            process.Kill();
            return ProcessEndOutcome.Ended;
        } catch (Exception e) when (e is ArgumentException or InvalidOperationException) {
            // Not in the process list, or exited between the lookup and the kill.
            return ProcessEndOutcome.AlreadyGone;
        } catch (Win32Exception e) {
            Log.Warn($"Could not end process {pid}: access denied", e);
            return ProcessEndOutcome.Denied;
        } catch (NotSupportedException e) {
            // A remote process has no kill. Used to escape the filter entirely and fault the command.
            Log.Warn($"Could not end process {pid}", e);
            return ProcessEndOutcome.Failed;
        }
    }

    public bool HasExited(int pid) {
        try {
            using var process = Process.GetProcessById(pid);
            return process.HasExited;
        } catch (Exception e) when (e is ArgumentException or InvalidOperationException or Win32Exception) {
            // Gone, or no longer openable — either way there is nothing left to wait for.
            return true;
        }
    }
}
