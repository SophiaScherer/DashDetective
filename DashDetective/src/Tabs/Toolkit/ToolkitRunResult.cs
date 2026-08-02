namespace DashDetective.Tabs.Toolkit;

/// <summary>What one run produced: whether it worked and the text the Execution Log shows for it.
/// Display-ready — <see cref="ToolkitRunner"/> has already merged, capped and worded the output, so the
/// view model turns this into a <see cref="ToolkitLogEntry"/> without interpreting anything.</summary>
/// <param name="Success">Whether the command ran to a clean finish. A non-zero exit, a timeout, a
/// refused launch and a cancelled elevation prompt are all failures.</param>
/// <param name="Output">The log stanza's body.</param>
/// <param name="ExitCode">The process's exit code, where there was one to read.</param>
public sealed record ToolkitRunResult(bool Success, string Output, int? ExitCode = null) {
    public static ToolkitRunResult Ok(string output, int? exitCode = null) =>
        new(true, output, exitCode);

    public static ToolkitRunResult Failure(string output, int? exitCode = null) =>
        new(false, output, exitCode);
}
