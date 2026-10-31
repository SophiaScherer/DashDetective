using CommunityToolkit.Mvvm.ComponentModel;
using DashDetective.Shared;
using System;

namespace DashDetective.Tabs.Toolkit;

/// <summary>
/// One line of the Execution Log: when a command ran, what ran, and what it printed.
///
/// Observable rather than a plain record, and it keeps the raw <see cref="Timestamp"/> alongside the
/// formatted <see cref="Time"/>: the clock-format preference can change while rows are on screen, and a
/// row that had only ever stored its pre-formatted string could not be re-stamped.
/// </summary>
public partial class ToolkitLogEntry : ObservableObject {
    public ToolkitLogEntry(
        DateTime timestamp, string command, string output, ClockFormat format, bool isRunning = false) {
        Timestamp = timestamp;
        IsRunning = isRunning;
        Command = command;
        Output = output;
        _time = TimeOfDayFormatter.Format(timestamp, format);
    }

    /// <summary>The wall-clock moment the command started.</summary>
    public DateTime Timestamp { get; }

    /// <summary>The command, shown after a "$" prompt.</summary>
    public string Command { get; }

    /// <summary>Whether this is the placeholder for a captured command still in flight, which is the only
    /// stanza that offers Cancel. Fixed at construction: the finished stanza is a new instance.</summary>
    public bool IsRunning { get; }

    /// <summary>What the command reported.</summary>
    public string Output { get; }

    /// <summary>The formatted time the row shows. Re-stamped by <see cref="Restamp"/>.</summary>
    [ObservableProperty] private string _time;

    /// <summary>Re-renders the timestamp after the clock-format preference changes.</summary>
    public void Restamp(ClockFormat format) => Time = TimeOfDayFormatter.Format(Timestamp, format);
}
