namespace DashDetective.Tabs.Processes;

/// <summary>
/// Ends a process. A seam purely so End task can be tested: the kill used to be a bare
/// <c>Process.Kill()</c> inside the view model, which no test could reach without actually killing
/// something on the machine running it.
/// </summary>
internal interface IProcessTerminator {
    /// <summary>Ends the process, or reports that it could not be. Never throws — a process that has
    /// already exited, or a protected one this session cannot touch without elevation, is
    /// <c>false</c>, not an exception.</summary>
    bool TryEnd(int pid);
}
