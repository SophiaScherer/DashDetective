using System;
using System.Diagnostics;

namespace DashDetective.Tabs.Processes;

/// <summary>
/// The OS-level process operations the process table needs beyond the managed <see cref="Process"/> API.
/// Implementations must never throw: a protected or exited process yields <c>false</c> or does nothing.
/// </summary>
internal interface IProcessInterop {
    /// <summary>A process's cumulative read+write transfer bytes for the Disk column; the caller diffs
    /// these over the sample interval to get a rate. <c>false</c> when the process denies a handle
    /// (protected/elevated) or has exited.</summary>
    bool TryGetIoBytes(Process process, out ulong totalBytes);

    /// <summary>Shows the native Properties dialog for a process's executable. Needs the owning window
    /// handle, so it is reached from the view code-behind through the view model.</summary>
    void ShowProperties(IntPtr owner, int pid);

    /// <summary>The interop for this machine, or one that reports no I/O and shows no dialog.</summary>
    static IProcessInterop ForCurrentPlatform() =>
        OperatingSystem.IsWindows() ? new WindowsProcessInterop() : new UnsupportedProcessInterop();
}
