using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DashDetective.Shared.Shortcuts;
using System;

namespace DashDetective.Shell.Shortcuts;

/// <summary>
/// The app's one keyboard-shortcut listener. Attaches a single tunneling <c>KeyDown</c> handler to the
/// window — the idiom the Help overlay and File Explorer already use for window-wide input — rather
/// than <c>Window.KeyBindings</c>, for two reasons: it can suppress bare-key gestures while a text box
/// has focus, and tunneling reaches the shell before Avalonia's focus manager claims Ctrl+Tab as
/// tab-group navigation.
/// </summary>
public sealed class ShellShortcutHandler : IDisposable {
    private readonly TopLevel _host;
    private readonly Func<ShortcutId, bool> _dispatch;

    /// <summary>Starts listening on <paramref name="host"/>, routing resolved shortcuts to
    /// <paramref name="dispatch"/>, which returns whether the action ran.</summary>
    public ShellShortcutHandler(TopLevel host, Func<ShortcutId, bool> dispatch) {
        _host = host;
        _dispatch = dispatch;
        _host.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    public void Dispose() => _host.RemoveHandler(InputElement.KeyDownEvent, OnKeyDown);

    private void OnKeyDown(object? sender, KeyEventArgs e) {
        var textInputFocused = KeyboardFocus.IsTextInputFocused(_host);
        if (!ShortcutCatalog.TryResolve(e.Key, e.KeyModifiers, textInputFocused, out var id))
            return;

        // Consume only what actually ran. A shortcut that doesn't apply right now (End Task with
        // nothing selected) must leave the key to whatever else would have handled it.
        e.Handled = _dispatch(id);
    }
}
