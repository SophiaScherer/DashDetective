using Avalonia.Input;

namespace DashDetective.Tabs.Processes;

/// <summary>The rules for what a click on a process row's body does beyond selecting it. Pure, so the
/// decision is testable without a pointer.</summary>
internal static class ProcessRowClick {
    /// <summary>
    /// Whether a click should clear the selection instead of re-selecting. Only a plain, single, left
    /// click on the row that is the whole selection qualifies. A row inside a multi-selection still
    /// collapses to itself, Ctrl and Shift keep their toggle and range meaning, and the second click of
    /// a double-click is the expand gesture, not a deselect.
    /// </summary>
    public static bool ShouldDeselect(bool isOnlySelection, KeyModifiers modifiers, int clickCount, MouseButton button) =>
        isOnlySelection && modifiers == KeyModifiers.None && clickCount == 1 && button == MouseButton.Left;
}
