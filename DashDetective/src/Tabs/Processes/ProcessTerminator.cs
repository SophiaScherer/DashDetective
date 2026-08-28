using System;
using System.ComponentModel;
using System.Diagnostics;

namespace DashDetective.Tabs.Processes;

/// <summary>The real terminator. Managed and platform-neutral, so unlike <see cref="IProcessInterop"/>
/// there is nothing here to resolve per platform.</summary>
internal sealed class ProcessTerminator : IProcessTerminator {
    public bool TryEnd(int pid) {
        try {
            using var process = Process.GetProcessById(pid);
            process.Kill();
            return true;
        } catch (Exception e) when (e is ArgumentException or Win32Exception or InvalidOperationException) {
            // Already exited (ArgumentException / InvalidOperationException) or access denied without
            // elevation (Win32Exception).
            return false;
        }
    }
}
