using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform;
using Avalonia.Threading;
using System;
using System.Runtime.CompilerServices;

namespace DashDetective.Shared.Controls;

/// <summary>
/// Opts a window into the custom title bar. Set <c>WindowChrome.Custom="True"</c> on the window, give it
/// the <c>AppWindowDecorations</c> theme and put a <see cref="TitleBar"/> at the top of its content.
///
/// The platform is decided here and nowhere else, per <see cref="TitleBarRules.ShouldExtend"/>, and
/// decided again when the OS switches a contrast theme on or off, so the native caption comes back for it.
/// It also keeps Alt+F4 working while extended.
/// </summary>
public static class WindowChrome {
    /// <summary>Whether the window draws its own title bar where the platform allows it.</summary>
    public static readonly AttachedProperty<bool> CustomProperty =
        AvaloniaProperty.RegisterAttached<Window, bool>("Custom", typeof(WindowChrome));

    // One OS-settings subscription per window, however often the property is set.
    private static readonly ConditionalWeakTable<Window, EventHandler<PlatformColorValues>> Watchers = new();

    static WindowChrome() {
        CustomProperty.Changed.AddClassHandler<Window>(OnCustomChanged);
    }

    public static bool GetCustom(Window window) => window.GetValue(CustomProperty);

    public static void SetCustom(Window window, bool value) => window.SetValue(CustomProperty, value);

    private static void OnCustomChanged(Window window, AvaloniaPropertyChangedEventArgs e) {
        Apply(window);

        // Removed first either way, so setting the property twice cannot close the window twice.
        window.RemoveHandler(InputElement.KeyDownEvent, OnKeyDown);
        if (e.GetNewValue<bool>()) {
            Watch(window);
            window.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Bubble);
        }
    }

    /// <summary>Alt+F4, done by hand while extended. Avalonia greys Close out of the system menu when a
    /// window extends, and Windows routes Alt+F4 through that item. Handled, so the OS cannot close twice
    /// where it does still act on it.</summary>
    private static void OnKeyDown(object? sender, KeyEventArgs e) {
        if (e.Handled || sender is not Window { IsExtendedIntoWindowDecorations: true } window ||
            e.Key != Key.F4 || e.KeyModifiers != KeyModifiers.Alt)
            return;

        e.Handled = true;
        window.Close();
    }

    private static void Apply(Window window) {
        var contrast = Application.Current?.PlatformSettings?.GetColorValues().ContrastPreference
                       ?? ColorContrastPreference.NoPreference;

        window.ExtendClientAreaToDecorationsHint =
            GetCustom(window) && TitleBarRules.ShouldExtend(OperatingSystem.IsWindows(), contrast);
    }

    /// <summary>Re-decides when the OS colors change, which is how a contrast theme arrives. The OS
    /// raises the same event for its accent, and re-applying an unchanged hint is a no-op.</summary>
    private static void Watch(Window window) {
        if (Watchers.TryGetValue(window, out _) || Application.Current?.PlatformSettings is not { } settings)
            return;

        EventHandler<PlatformColorValues> handler = (_, _) => {
            if (Dispatcher.UIThread.CheckAccess())
                Apply(window);
            else
                Dispatcher.UIThread.Post(() => Apply(window));
        };

        Watchers.Add(window, handler);
        settings.ColorValuesChanged += handler;
        window.Closed += (_, _) => {
            settings.ColorValuesChanged -= handler;
            Watchers.Remove(window);
        };
    }
}
