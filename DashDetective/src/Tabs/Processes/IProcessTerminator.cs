namespace DashDetective.Tabs.Processes;

/// <summary>
/// Ends a process. A seam purely so End task can be tested: the kill used to be a bare
/// <c>Process.Kill()</c> inside the view model, which no test could reach without actually killing
/// something on the machine running it. It is <b>not</b> a platform seam — see
/// <see cref="ProcessTerminator"/>.
/// </summary>
internal interface IProcessTerminator {
    /// <summary>Asks the process to end and reports what happened. Never throws — an already-exited or
    /// protected process is an outcome, not an exception. Returns as soon as the request is accepted;
    /// <see cref="HasExited"/> is what confirms it.</summary>
    ProcessEndOutcome Request(int pid);

    /// <summary>Whether the process is gone. A PID that cannot be opened at all reads as exited, since
    /// the only use of this is confirming a kill.</summary>
    bool HasExited(int pid);
}
