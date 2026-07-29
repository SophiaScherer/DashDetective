using System.Collections.Generic;

namespace DashDetective.Tabs.FileExplorer;

/// <summary>
/// The File Explorer's back/forward trail, following the browser rule: moving somewhere new pushes the
/// place you left onto the back stack and discards the forward trail, while going back or forward moves
/// entries between the two stacks. Holds paths only — no UI types and no file-system access — so the
/// navigation rules are testable on their own, like <c>PagerMath</c>.
/// </summary>
public sealed class NavigationHistory {
    private readonly Stack<string> _back = new();
    private readonly Stack<string> _forward = new();

    /// <summary>Whether there is somewhere to go back to.</summary>
    public bool CanGoBack => _back.Count > 0;

    /// <summary>Whether a forward trail survives (nothing new has been visited since going back).</summary>
    public bool CanGoForward => _forward.Count > 0;

    /// <summary>Records a move away from <paramref name="from"/> to somewhere new. A blank origin (the
    /// very first folder opened, before anything was showing) starts no trail.</summary>
    public void Record(string from) {
        if (string.IsNullOrEmpty(from))
            return;

        _back.Push(from);
        _forward.Clear();
    }

    /// <summary>Steps back from <paramref name="current"/>, handing back the folder to open.</summary>
    public bool TryGoBack(string current, out string target) => TryStep(_back, _forward, current, out target);

    /// <summary>Steps forward from <paramref name="current"/>, handing back the folder to open.</summary>
    public bool TryGoForward(string current, out string target) => TryStep(_forward, _back, current, out target);

    private static bool TryStep(Stack<string> from, Stack<string> onto, string current, out string target) {
        if (from.Count == 0) {
            target = "";
            return false;
        }

        target = from.Pop();
        onto.Push(current);
        return true;
    }
}
