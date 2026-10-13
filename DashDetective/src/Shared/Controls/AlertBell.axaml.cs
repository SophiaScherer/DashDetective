using Avalonia;
using Avalonia.Controls;
using DashDetective.Services.SystemMetrics;

namespace DashDetective.Shared.Controls;

/// <summary>
/// The bell that switches one resource's alert on and off, placed on the surface showing that resource.
/// Takes no room when <see cref="Switch"/> is null, so a call site whose resource has no alert (a network
/// adapter) needs no visibility binding of its own.
/// </summary>
public partial class AlertBell : UserControl {
    public static readonly StyledProperty<AlertSwitch?> SwitchProperty =
        AvaloniaProperty.Register<AlertBell, AlertSwitch?>(nameof(Switch));

    public AlertBell() {
        InitializeComponent();
    }

    /// <summary>The shared switch this bell shows and flips.</summary>
    public AlertSwitch? Switch {
        get => GetValue(SwitchProperty);
        set => SetValue(SwitchProperty, value);
    }
}
