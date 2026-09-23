using Avalonia.Input;
using DashDetective.Shared.Shortcuts;

namespace DashDetective.Tabs.Settings;

/// <summary>What an armed <see cref="ShortcutCaptureBox"/> does with a key press. Kept out of the control so
/// the rule is testable without a render backend.</summary>
internal enum CaptureKeyAction {
    /// <summary>A held modifier: part of the gesture being built, not a gesture of its own.</summary>
    Wait,

    /// <summary>Escape: stand down and leave the existing binding untouched.</summary>
    Cancel,

    /// <summary>Anything else: the new binding.</summary>
    Capture,
}

internal static class CaptureKeys {
    /// <summary>Decides what a key press means to an armed capture box. Escape cancels whatever modifiers
    /// are held with it, so it can never itself be bound: it is the one way out of a capture.</summary>
    internal static CaptureKeyAction Classify(Key key) =>
        GestureFormatter.IsModifierKey(key) ? CaptureKeyAction.Wait
        : key == Key.Escape ? CaptureKeyAction.Cancel
        : CaptureKeyAction.Capture;
}
