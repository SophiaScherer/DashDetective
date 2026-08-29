using Avalonia.Input;
using System.Collections.Generic;

namespace DashDetective.Shared.Shortcuts;

/// <summary>One entry in the <see cref="ShortcutCatalog"/>: the gestures bound to an action, plus the
/// copy the Help modal shows for it.</summary>
/// <param name="Id">The action the gestures trigger.</param>
/// <param name="Gestures">Every gesture bound to the action (e.g. F5 and Ctrl+R both refresh).</param>
/// <param name="Keys">How the binding reads in Help, e.g. "F5 / Ctrl+R".</param>
/// <param name="Description">What the action does, in one short phrase.</param>
/// <param name="Scope">Global, or the tab that owns the shortcut.</param>
/// <param name="AllowInTextInput">Whether the shortcut still fires while a text box has focus. False
/// for bare keys, which must reach the text box instead of triggering an app action.</param>
/// <param name="ShowInHelp">Whether Help lists this entry. False for members of a run that a single
/// row already covers — Ctrl+2…Ctrl+9 are described by the Ctrl+1 row.</param>
public sealed record Shortcut(
    ShortcutId Id,
    IReadOnlyList<KeyGesture> Gestures,
    string Keys,
    string Description,
    ShortcutScope Scope,
    bool AllowInTextInput = true,
    bool ShowInHelp = true);
