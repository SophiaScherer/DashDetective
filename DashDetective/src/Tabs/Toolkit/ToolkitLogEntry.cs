namespace DashDetective.Tabs.Toolkit;

/// <summary>One line of the Execution Log: when a command ran, what ran, and what it printed.
/// Formatting is done at capture time so the row is a plain record the view binds directly.</summary>
/// <param name="Time">The 24-hour wall clock time the command ran.</param>
/// <param name="Command">The command, shown after a "$" prompt.</param>
/// <param name="Output">What the command reported.</param>
public sealed record ToolkitLogEntry(string Time, string Command, string Output);
