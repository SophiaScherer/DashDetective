using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using DashDetective.Services.Theming;

namespace DashDetective.Tabs.Settings;

/// <summary>
/// Renders its subtree in a chosen theme with a draft accent. The accent keys are top-level, so this
/// scope's own resources shadow the application's and the shared styles inside draw the draft.
/// </summary>
public sealed class AccentPreviewScope : ThemeVariantScope {
    public static readonly StyledProperty<AccentPreset?> AccentProperty =
        AvaloniaProperty.Register<AccentPreviewScope, AccentPreset?>(nameof(Accent));

    public AccentPreset? Accent {
        get => GetValue(AccentProperty);
        set => SetValue(AccentProperty, value);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e) {
        base.OnAttachedToVisualTree(e);
        if (Application.Current is { } app)
            app.ActualThemeVariantChanged += OnAppThemeChanged;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e) {
        base.OnDetachedFromVisualTree(e);
        if (Application.Current is { } app)
            app.ActualThemeVariantChanged -= OnAppThemeChanged;
    }

    /// <summary>An app theme switch re-resolves inline {DynamicResource} theme keys against the app's
    /// variant rather than this scope's, so the scope re-asserts its own to pull them back.</summary>
    private void OnAppThemeChanged(object? sender, System.EventArgs e) {
        var variant = RequestedThemeVariant;
        SetCurrentValue(RequestedThemeVariantProperty, null);
        SetCurrentValue(RequestedThemeVariantProperty, variant);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change) {
        base.OnPropertyChanged(change);
        if (change.Property == AccentProperty || change.Property == RequestedThemeVariantProperty)
            WriteAccent();
    }

    private void WriteAccent() {
        if (Accent is not { } accent)
            return;

        var variant = RequestedThemeVariant;
        var dark = variant == ThemeVariant.Dark || variant == AppVariants.HighContrastDark;
        AccentResources.Write(Resources, accent, dark);
    }
}
