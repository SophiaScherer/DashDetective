using Avalonia;
using Avalonia.Controls;

namespace DashDetective.Shared.Controls;

/// <summary>
/// One widget: the panel surface, its header row, and whatever body the call site puts inside it.
/// Replaces the hand-rolled <c>Border Classes="panel"</c> + <c>StackPanel</c> +
/// <c>TextBlock Classes="panelTitle"</c> assembly that every tab had been writing out in full, each
/// with its own header grid and its own header-to-body gap (8, 10 and 12 for the same shape).
///
/// A <see cref="ContentControl"/> rather than a <c>UserControl</c> with properties (the
/// <see cref="StatCard"/> pattern) because a widget's body is arbitrary markup: a UserControl's own
/// Content is taken by its .axaml root, so the body would need a second content property and every
/// call site would have to name it. It is also not a <c>HeaderedContentControl</c>, which offers two
/// slots where a header here needs three — see <see cref="HeaderLead"/>.
///
/// The template lives in src/Shared/Styles/Widgets.axaml, following the one ControlTemplate the repo
/// already had (SharedStyles' <c>ToggleButton.toggle</c>), so no ControlTheme is introduced.
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

    /// <summary>The widget's name, drawn as the shared <c>panelTitle</c>. Empty on a panel that is a
    /// surface rather than a titled widget, which collapses the row.</summary>
    public string? Title {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>A caption directly under the title (what the widget is showing, or over what span).
    /// Sits tight to the title rather than in the body, so it reads as part of the heading.</summary>
    public string? Subtitle {
        get => GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    /// <summary>Content immediately after the title, left-aligned — the Performance tab's jump link and
    /// the Storage tab's drive picker, both of which belong to the title rather than to the far end of
    /// the row. This is the third slot a HeaderedContentControl could not give.</summary>
    public object? HeaderLead {
        get => GetValue(HeaderLeadProperty);
        set => SetValue(HeaderLeadProperty, value);
    }

    /// <summary>Content at the far end of the header row — a live readout or a row count.</summary>
    public object? HeaderContent {
        get => GetValue(HeaderContentProperty);
        set => SetValue(HeaderContentProperty, value);
    }

    /// <summary>Stable identity for this widget, as <c>{page}.{slug}</c>. Set at the call site so a
    /// saved layout can name a widget without the page keeping a parallel list.</summary>
    public string? WidgetId {
        get => GetValue(WidgetIdProperty);
        set => SetValue(WidgetIdProperty, value);
    }
}
