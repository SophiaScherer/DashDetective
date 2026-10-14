using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DashDetective.Shared.Shortcuts;
using System;
using System.Collections.Generic;

namespace DashDetective.Shell.Shortcuts;

/// <summary>
/// The app's one keyboard-shortcut listener. Attaches a single tunneling <c>KeyDown</c> handler to the
/// window — the idiom the Help overlay and File Explorer already use for window-wide input — rather
/// than <c>Window.KeyBindings</c>, for two reasons: it can suppress bare-key gestures while a text box
/// has focus, and tunneling reaches the shell before Avalonia's focus manager claims Ctrl+Tab as
/// tab-group navigation. The mouse's back button is listened for here too, through the same dispatch,
/// so it obeys the same chain (capture, modal, page, global) as Alt+←.
/// </summary>
public sealed class ShellShortcutHandler : IDisposable {
    private readonly TopLevel _host;
    private readonly Func<ShortcutBindings> _bindings;
    private readonly Func<ShortcutScope> _activeScope;
    private readonly Func<ShortcutId, bool> _dispatch;

    /// <summary>Starts listening on <paramref name="host"/>. <paramref name="bindings"/> is asked for the
    /// live bindings at press time — a delegate rather than an instance, matching the other two, because
    /// the window is constructed before its <c>DataContext</c> exists. <paramref name="activeScope"/> is
    /// asked which tab's bindings apply, and <paramref name="dispatch"/> runs the resolved action,
    /// returning whether it did anything.</summary>
    public ShellShortcutHandler(
        TopLevel host, Func<ShortcutBindings> bindings, Func<ShortcutScope> activeScope,
        Func<ShortcutId, bool> dispatch) {
        _host = host;
        _bindings = bindings;
        _activeScope = activeScope;
        _dispatch = dispatch;
        _host.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
        _host.AddHandler(InputElement.PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel,
                         handledEventsToo: true);
    }

    public void Dispose() {
        _host.RemoveHandler(InputElement.KeyDownEvent, OnKeyDown);
        _host.RemoveHandler(InputElement.PointerPressedEvent, OnPointerPressed);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e) {
        var textInputFocused = KeyboardFocus.IsTextInputFocused(_host);
        var ids = _bindings().Resolve(e.Key, e.KeyModifiers, textInputFocused, _activeScope());

        // Consume only what actually ran. A shortcut that doesn't apply right now (End Task with
        // nothing selected) must leave the key to whatever else would have handled it.
        e.Handled = RunFirst(ids);
    }

    // Only the press that changed XButton1, so holding it while clicking another button runs nothing.
    private void OnPointerPressed(object? sender, PointerPressedEventArgs e) {
        if (e.GetCurrentPoint(_host).Properties.PointerUpdateKind != PointerUpdateKind.XButton1Pressed)
            return;

        e.Handled = RunFirst(ShortcutCatalog.MouseBack);
    }

    /// <summary>Offers each action in turn and stops at the first that acts.</summary>
    private bool RunFirst(IReadOnlyList<ShortcutId> ids) {
        foreach (var id in ids)
            if (_dispatch(id))
                return true;

        return false;
    }
}
