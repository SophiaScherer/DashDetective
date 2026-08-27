using Avalonia;
using Avalonia.Controls;

namespace DashDetective.Shared.Controls;

/// <summary>
/// One widget: the panel surface, its header row, and whatever body the call site puts inside it.
/// Replaces the hand-rolled Border + StackPanel + panelTitle assembly every tab wrote out in full.
///
/// A <see cref="ContentControl"/>, not the <see cref="StatCard"/> UserControl pattern, because the
/// body is arbitrary markup; not a <c>HeaderedContentControl</c>, which gives two slots where this
/// needs three. Template in src/Shared/Styles/Widgets.axaml.
/// </summary>
public class WidgetPanel : ContentControl {
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<WidgetPanel, string?>(nameof(Title));

    public static readonly StyledProperty<string?> SubtitleProperty =
        AvaloniaProperty.Register<WidgetPanel, string?>(nameof(Subtitle));

    public static readonly StyledProperty<object?> HeaderLeadProperty =
        AvaloniaProperty.Register<WidgetPanel, object?>(nameof(HeaderLead));

    public static readonly StyledProperty<object?> HeaderContentProperty =
        AvaloniaProperty.Register<WidgetPanel, object?>(nameof(HeaderContent));

    public static readonly StyledProperty<string?> WidgetIdProperty =
        AvaloniaProperty.Register<WidgetPanel, string?>(nameof(WidgetId));

    /// <summary>The widget's name, drawn as the shared <c>panelTitle</c>. Empty collapses it.</summary>
    public string? Title {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>Caption under the title, tight to it so it reads as part of the heading.</summary>
    public string? Subtitle {
        get => GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    /// <summary>Content against the title: Performance's jump link, Storage's drive picker. The
    /// third slot a HeaderedContentControl could not give.</summary>
    public object? HeaderLead {
        get => GetValue(HeaderLeadProperty);
        set => SetValue(HeaderLeadProperty, value);
    }

    /// <summary>Content at the far end of the header row — a live readout or a row count.</summary>
    public object? HeaderContent {
        get => GetValue(HeaderContentProperty);
        set => SetValue(HeaderContentProperty, value);
    }

    /// <summary>Stable identity, as <c>{page}.{slug}</c>, for naming this widget in a saved
    /// layout.</summary>
    public string? WidgetId {
        get => GetValue(WidgetIdProperty);
        set => SetValue(WidgetIdProperty, value);
    }
}
