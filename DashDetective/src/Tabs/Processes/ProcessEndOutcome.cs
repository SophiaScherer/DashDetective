namespace DashDetective.Tabs.Processes;

/// <summary>
/// What ending one process did. A <c>bool</c> conflated "it was already gone" with "this session may not
/// touch it", which are opposite things to tell the user: the first needs no message at all, the second
/// needs elevation.
/// </summary>
internal enum ProcessEndOutcome {
    /// <summary>Termination was accepted. Not proof the process is gone — <c>Process.Kill</c> is
    /// asynchronous, so exit is confirmed separately.</summary>
    Ended,

    /// <summary>The process had already exited. Nothing to report; the row simply goes.</summary>
    AlreadyGone,

    /// <summary>Refused for lack of rights — a protected or another user's process.</summary>
    Denied,

    /// <summary>Termination failed for any other reason, or was accepted and the process did not
    /// exit.</summary>
    Failed,
}
