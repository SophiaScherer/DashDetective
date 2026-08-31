using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DashDetective.Shared.Shortcuts;
using System;

namespace DashDetective.Tabs.Settings;

/// <summary>
/// The rebinding control: shows a shortcut's current keys, and once armed captures the next key press as
/// its new binding.
///
/// <b>The shell sees the key first.</b> Its listener is a tunnelling handler on the window, so it runs
/// before this control ever gets the event — arming the box and pressing Ctrl+1 would navigate away
/// instead of capturing. <see cref="IsCapturing"/> is what the shell watches to stand down; it is raised
/// through <see cref="CapturingChanged"/> so the Settings view model can hold that flag while a capture
/// is live.
///
/// Modifier-only presses are ignored rather than captured, because a binding of "Ctrl" alone would fire
/// the moment the key was touched. Escape abandons the capture.
/// </summary>
public partial class ShortcutCaptureBox : UserControl {
    public static readonly StyledProperty<string> KeysProperty =
        AvaloniaProperty.Register<ShortcutCaptureBox, string>(nameof(Keys), defaultValue: "");

    public static readonly StyledProperty<bool> IsCustomProperty =
        AvaloniaProperty.Register<ShortcutCaptureBox, bool>(nameof(IsCustom));

    /// <summary>The binding to show when not capturing.</summary>
    public string Keys {
        get => GetValue(KeysProperty);
        set => SetValue(KeysProperty, value);
    }

    /// <summary>Whether this shortcut is off its default, which is what offers the reset button.</summary>
    public bool IsCustom {
        get => GetValue(IsCustomProperty);
        set => SetValue(IsCustomProperty, value);
    }

    /// <summary>Whether this box is waiting for a key press.</summary>
    public bool IsCapturing { get; private set; }

    /// <summary>Raised with the captured gesture. The handler decides whether to accept it — a clash
    /// inside the same scope is refused — so this control never assumes the rebind took.</summary>
    public event EventHandler<KeyGesture>? GestureCaptured;

    /// <summary>Raised when this box arms or stands down, so the shell can stop claiming key presses.</summary>
    public event EventHandler<bool>? CapturingChanged;

    /// <summary>Raised when the reset button is pressed.</summary>
    public event EventHandler? ResetRequested;

    public ShortcutCaptureBox() {
        InitializeComponent();

        Trigger.Click += (_, _) => StartCapture();
        Reset.Click += (_, _) => ResetRequested?.Invoke(this, EventArgs.Empty);

        // Tunnelled and on the control itself: once armed, the press must not reach the button's own
        // key handling (Space and Enter would otherwise re-trigger the click).
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        LostFocus += (_, _) => StopCapture();

        Render();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change) {
        base.OnPropertyChanged(change);

        if (change.Property == KeysProperty || change.Property == IsCustomProperty)
            Render();
    }

    private void StartCapture() {
        if (IsCapturing)
            return;

        IsCapturing = true;
        CapturingChanged?.Invoke(this, true);
        Render();
    }

    private void StopCapture() {
        if (!IsCapturing)
            return;

        IsCapturing = false;
        CapturingChanged?.Invoke(this, false);
        Render();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e) {
        if (!IsCapturing)
            return;

        // Held modifiers are how a gesture is built, not a gesture of their own.
        if (GestureFormatter.IsModifierKey(e.Key)) {
            e.Handled = true;
            return;
        }

        e.Handled = true;

        if (e.Key == Key.Escape) {
            StopCapture();
            return;
        }

        var gesture = new KeyGesture(e.Key, e.KeyModifiers);
        StopCapture();
        GestureCaptured?.Invoke(this, gesture);
    }

    private void Render() {
        Label.Text = IsCapturing ? "Press keys…" : Keys;
        Box.Classes.Set("capturing", IsCapturing);
        Reset.IsVisible = IsCustom && !IsCapturing;
    }
}
