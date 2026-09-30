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
/// It also restores the system Close that Avalonia greys out of the system menu while extended.
/// </summary>
public static class WindowChrome {
    /// <summary>Whether the window draws its own title bar where the platform allows it.</summary>
    public static readonly AttachedProperty<bool> CustomProperty =
        AvaloniaProperty.RegisterAttached<Window, bool>("Custom", typeof(WindowChrome));

    // Windows already attached, so setting the property twice cannot subscribe or close twice.
    private static readonly ConditionalWeakTable<Window, object> Attached = new();

    static WindowChrome() {
        CustomProperty.Changed.AddClassHandler<Window>(OnCustomChanged);
    }

    public static bool GetCustom(Window window) => window.GetValue(CustomProperty);

    public static void SetCustom(Window window, bool value) => window.SetValue(CustomProperty, value);

    private static void OnCustomChanged(Window window, AvaloniaPropertyChangedEventArgs e) {
        Apply(window);

        if (e.GetNewValue<bool>())
            Attach(window);
    }

    private static void Apply(Window window) {
        var contrast = Application.Current?.PlatformSettings?.GetColorValues().ContrastPreference
                       ?? ColorContrastPreference.NoPreference;

        window.ExtendClientAreaToDecorationsHint =
            GetCustom(window) && TitleBarRules.ShouldExtend(OperatingSystem.IsWindows(), contrast);
    }

    /// <summary>Everything a window needs once, torn down when it closes. Each part checks
    /// <see cref="Window.IsExtendedIntoWindowDecorations"/> itself, so it is inert while the system title
    /// bar is showing.</summary>
    private static void Attach(Window window) {
        if (Attached.TryGetValue(window, out _))
            return;
        Attached.Add(window, window);

        var unwatch = WatchContrast(window);
        var unhook = HookSystemClose(window);
        window.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Bubble);

        window.Closed += (_, _) => {
            unwatch();
            unhook();
            window.RemoveHandler(InputElement.KeyDownEvent, OnKeyDown);
            Attached.Remove(window);
        };
    }

    /// <summary>Re-decides when the OS colors change, which is how a contrast theme arrives. The OS
    /// raises the same event for its accent, and re-applying an unchanged hint is a no-op.</summary>
    private static Action WatchContrast(Window window) {
        if (Application.Current?.PlatformSettings is not { } settings)
            return () => { };

        EventHandler<PlatformColorValues> handler = (_, _) => {
            if (Dispatcher.UIThread.CheckAccess())
                Apply(window);
            else
                Dispatcher.UIThread.Post(() => Apply(window));
        };

        settings.ColorValuesChanged += handler;
        return () => settings.ColorValuesChanged -= handler;
    }

    /// <summary>
    /// The taskbar's "Close window", its thumbnail × and Alt+F4 all arrive as SC_CLOSE, which Windows
    /// ignores while Avalonia has Close greyed in the system menu. The hook runs before Avalonia's own
    /// window procedure, closes the window the ordinary way, and swallows the command so it cannot act twice.
    /// Win32Properties carries no platform annotation and is inert off Windows, so nothing here needs one.
    /// </summary>
    private static Action HookSystemClose(Window window) {
        if (!OperatingSystem.IsWindows())
            return () => { };

        Win32Properties.CustomWndProcHookCallback hook = OnMessage;
        Win32Properties.AddWndProcHookCallback(window, hook);
        return () => Win32Properties.RemoveWndProcHookCallback(window, hook);

        IntPtr OnMessage(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam, ref bool handled) {
            if (!window.IsExtendedIntoWindowDecorations || !TitleBarRules.IsSystemClose(message, wParam))
                return IntPtr.Zero;

            handled = true;
            // Posted rather than run inside the window procedure, as a WM_CLOSE would have been.
            Dispatcher.UIThread.Post(window.Close);
            return IntPtr.Zero;
        }
    }

    /// <summary>Alt+F4 while focus is in the window, in case Windows drops the key before it becomes
    /// SC_CLOSE. Handled, so the key never reaches the hook above as well.</summary>
    private static void OnKeyDown(object? sender, KeyEventArgs e) {
        if (e.Handled || sender is not Window { IsExtendedIntoWindowDecorations: true } window ||
            e.Key != Key.F4 || e.KeyModifiers != KeyModifiers.Alt)
            return;

        e.Handled = true;
        // Posted for the same reason as the hook: this runs inside the window procedure's key dispatch.
        Dispatcher.UIThread.Post(window.Close);
    }
}
