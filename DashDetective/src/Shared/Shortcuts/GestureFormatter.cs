using Avalonia.Input;
using System.Text;

namespace DashDetective.Shared.Shortcuts;

/// <summary>
/// Renders a <see cref="KeyGesture"/> the way the Help copy spells it — "Ctrl+Shift+T", "Alt+↑", "Esc".
///
/// The catalog's default entries carry hand-written <c>Keys</c> strings, which say things a formatter
/// cannot ("Ctrl+1 … Ctrl+9" covers nine bindings in one row). A <b>rebound</b> shortcut has no such
/// string, so this generates one, which is what keeps Help and the Settings list honest about a binding
/// the user chose rather than leaving them describing the default it replaced.
/// </summary>
public static class GestureFormatter {
    /// <summary>The gesture as one display string.</summary>
    public static string Describe(KeyGesture gesture) {
        var sb = new StringBuilder();

        // Ctrl, Shift, Alt — the order the catalog's own strings use, and the one Windows shows.
        if (gesture.KeyModifiers.HasFlag(KeyModifiers.Control))
            sb.Append("Ctrl+");
        if (gesture.KeyModifiers.HasFlag(KeyModifiers.Shift))
            sb.Append("Shift+");
        if (gesture.KeyModifiers.HasFlag(KeyModifiers.Alt))
            sb.Append("Alt+");
        if (gesture.KeyModifiers.HasFlag(KeyModifiers.Meta))
            sb.Append("Win+");

        return sb.Append(Describe(gesture.Key)).ToString();
    }

    /// <summary>The key's own label. The named cases are the ones whose enum name is not what anyone
    /// calls the key — the OEM punctuation especially, where "OemQuestion" is the "/" key.</summary>
    public static string Describe(Key key) => key switch {
        Key.Up => "↑",
        Key.Down => "↓",
        Key.Left => "←",
        Key.Right => "→",
        Key.Back => "Backspace",
        Key.Escape => "Esc",
        Key.Return or Key.Enter => "Enter",
        Key.Space => "Space",
        Key.OemComma => ",",
        Key.OemPeriod => ".",
        Key.OemQuestion => "/",
        Key.OemMinus => "-",
        Key.OemPlus => "+",
        Key.OemPipe => "\\",
        Key.OemOpenBrackets => "[",
        Key.OemCloseBrackets => "]",
        Key.OemSemicolon => ";",
        Key.OemQuotes => "'",
        Key.OemTilde => "`",
        Key.PageUp => "PgUp",
        Key.PageDown => "PgDn",
        >= Key.D0 and <= Key.D9 => ((char)('0' + (key - Key.D0))).ToString(),
        >= Key.NumPad0 and <= Key.NumPad9 => $"Num {(char)('0' + (key - Key.NumPad0))}",
        _ => key.ToString(),
    };

    /// <summary>Whether a key is only a modifier. Capturing one as a binding would produce a shortcut
    /// that fires the moment Ctrl is touched, so the capture control refuses them.</summary>
    public static bool IsModifierKey(Key key) => key is
        Key.LeftCtrl or Key.RightCtrl or
        Key.LeftShift or Key.RightShift or
        Key.LeftAlt or Key.RightAlt or
        Key.LWin or Key.RWin;
}
