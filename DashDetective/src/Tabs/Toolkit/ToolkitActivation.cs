namespace DashDetective.Tabs.Toolkit;

/// <summary>
/// The Toolkit's activation decisions, kept free of Avalonia types so they are testable headlessly. The
/// view reports what it sees (what holds focus, how many clicks a press is); these decide what runs.
/// </summary>
internal static class ToolkitActivation {
    /// <summary>What holds keyboard focus when Enter is pressed.</summary>
    internal enum Focus {
        /// <summary>Nothing in the window holds focus.</summary>
        Nothing,

        /// <summary>A row body on this page.</summary>
        RowBody,

        /// <summary>Anything else — another button on the page, a shell control, the nav bar.</summary>
        Other,
    }

    /// <summary>
    /// The row Enter runs, or null to leave the key to whatever has focus. A focused row body runs its
    /// own row; with nothing focused the selection runs; any other focused control keeps Enter, because
    /// pressing it there must press that control and never run a command it says nothing about.
    /// </summary>
    internal static ToolkitEntry? EnterTarget(Focus focus, ToolkitEntry? focusedRow, ToolkitEntry? selected) =>
        focus switch {
            Focus.RowBody => focusedRow,
            Focus.Nothing => selected,
            _ => null,
        };

    /// <summary>Whether a press on the play button is the second (or later) of a multi-click. Ignored, so
    /// a double click on play runs once — a fast launch can finish before the second click lands.</summary>
    internal static bool IsRepeatPress(int clickCount) => clickCount > 1;
}
