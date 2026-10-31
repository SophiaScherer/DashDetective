using Avalonia.Controls;
using Avalonia.Input;
using System;

namespace DashDetective.Tabs.Dashboard;

/// <summary>How a <see cref="DashboardLink"/> was pressed.</summary>
public enum LinkGesture {
    Click,
    DoubleClick,
    Key,
}

/// <summary>
/// A card or chart that opens its device on another tab. A single pointer click does nothing, because
/// leaving the page is not something a stray click should do; a double click or Enter/Space on the
/// focused link goes. Styled as a plain <see cref="Button"/>, so <c>Button.bare</c> and the
/// tooltip-to-name rule still apply.
/// </summary>
public class DashboardLink : Button {
    // Set by the press that will end in OnClick, so the click can tell a pointer from a key.
    private bool _pointerPress;

    public DashboardLink() {
        DoubleTapped += (_, e) => {
            Respond(LinkGesture.DoubleClick);
            e.Handled = true;
        };
    }

    protected override Type StyleKeyOverride => typeof(Button);

    /// <summary>Whether a gesture leaves the page. Only the single pointer click stays.</summary>
    internal static bool Goes(LinkGesture gesture) => gesture != LinkGesture.Click;

    /// <summary>Runs <see cref="Button.Command"/> when <paramref name="gesture"/> goes. The one place the
    /// link acts, so the gate cannot be bypassed by a second path.</summary>
    internal void Respond(LinkGesture gesture) {
        if (!Goes(gesture) || Command is not { } command || !command.CanExecute(CommandParameter))
            return;

        command.Execute(CommandParameter);
    }

    /// <summary>A pointer press began; the click it may end in is a pointer click.</summary>
    internal void NotePointerPress() => _pointerPress = true;

    /// <summary>The press is over (released, or capture lost), so a later click from the keyboard or UI
    /// Automation's Invoke is not misread as a pointer click.</summary>
    internal void NotePointerEnd() => _pointerPress = false;

    /// <summary>What the click now being raised came from, consuming the pointer state.</summary>
    internal LinkGesture TakeClickGesture() {
        var gesture = _pointerPress ? LinkGesture.Click : LinkGesture.Key;
        _pointerPress = false;
        return gesture;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e) {
        NotePointerPress();
        base.OnPointerPressed(e);
    }

    // The base raises the click from inside its release handler, so the state is cleared only after it.
    protected override void OnPointerReleased(PointerReleasedEventArgs e) {
        base.OnPointerReleased(e);
        NotePointerEnd();
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e) {
        base.OnPointerCaptureLost(e);
        NotePointerEnd();
    }

    protected override void OnKeyDown(KeyEventArgs e) {
        NotePointerEnd();
        base.OnKeyDown(e);
    }

    // The base would run the command on every click; this routes it through the gate instead.
    protected override void OnClick() => Respond(TakeClickGesture());
}
