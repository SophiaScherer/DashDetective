using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace DashDetective.Shared.Controls;

/// <summary>
/// Scales everything inside it by <see cref="Scale"/>, measuring the content at the reduced size and
/// rendering it enlarged — so text and chrome grow together and no view has to know its own font size.
///
/// One is needed per visual root: a popup, a flyout and a second window are each their own tree and do
/// not inherit the shell's.
/// </summary>
public sealed class ScaleHost : LayoutTransformControl {
    public static readonly StyledProperty<double> ScaleProperty =
        AvaloniaProperty.Register<ScaleHost, double>(nameof(Scale), 1);

    /// <summary>The factor to scale by; 1 leaves the content exactly as authored.</summary>
    public double Scale {
        get => GetValue(ScaleProperty);
        set => SetValue(ScaleProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change) {
        base.OnPropertyChanged(change);

        // A non-positive factor would collapse the content, so it is refused rather than clamped here —
        // Services/Accessibility/UiScale owns the ladder and has already clamped a real selection.
        if (change.Property == ScaleProperty)
            LayoutTransform = Scale is > 0 and not 1 ? new ScaleTransform(Scale, Scale) : null;
    }
}
