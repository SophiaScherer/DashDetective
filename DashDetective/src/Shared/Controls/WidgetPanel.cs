using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace DashDetective.Shared.Controls;

/// <summary>
/// One widget: the panel surface, its header row, and whatever body the call site puts inside it.
/// Replaces the hand-rolled Border + StackPanel + panelTitle assembly every tab wrote out in full.
///
/// A <see cref="ContentControl"/>, not the <see cref="StatCard"/> UserControl pattern, because the
/// body is arbitrary markup; not a <c>HeaderedContentControl</c>, which gives two slots where this
/// needs three. Template in src/Shared/Styles/Widgets.axaml.
/// </summary>
public class WidgetPanel : ContentControl, IWidgetIdentity {
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

    public static readonly StyledProperty<WidgetCollapse?> CollapseProperty =
        AvaloniaProperty.Register<WidgetPanel, WidgetCollapse?>(nameof(Collapse));

    public static readonly StyledProperty<bool> IsCollapsedProperty =
        AvaloniaProperty.Register<WidgetPanel, bool>(nameof(IsCollapsed));

    private Button? _toggle;
    private WidgetCollapse? _listening;
    private bool _attached;
    private bool _applyingCollapse;

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

    /// <summary>The page's fold state, and the opt-in for the header chevron: no store, no chevron.
    /// The store rather than a second flag, so a page cannot offer the affordance without giving the
    /// state somewhere to live — and somewhere the page itself can reach it.</summary>
    public WidgetCollapse? Collapse {
        get => GetValue(CollapseProperty);
        set => SetValue(CollapseProperty, value);
    }

    /// <summary>Whether the body is folded away. Mirrors <see cref="Collapse"/>, which is the value
    /// that persists; setting it here writes back through the store.</summary>
    public bool IsCollapsed {
        get => GetValue(IsCollapsedProperty);
        set => SetValue(IsCollapsedProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e) {
        base.OnApplyTemplate(e);

        if (_toggle is not null)
            _toggle.Click -= OnToggleClick;

        _toggle = e.NameScope.Find<Button>("PART_Collapse");
        if (_toggle is not null)
            _toggle.Click += OnToggleClick;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e) {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        Listen(Collapse);
        ReadCollapse();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e) {
        _attached = false;
        Listen(null);
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change) {
        base.OnPropertyChanged(change);

        if (change.Property == CollapseProperty) {
            if (_attached)
                Listen(Collapse);
            ReadCollapse();
        } else if (change.Property == WidgetIdProperty) {
            ReadCollapse();
        } else if (change.Property == IsCollapsedProperty) {
            PseudoClasses.Set(":collapsed", IsCollapsed);
            if (!_applyingCollapse)
                Collapse?.Set(WidgetId, IsCollapsed);
        }
    }

    private void OnToggleClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        IsCollapsed = !IsCollapsed;

    /// <summary>Takes the fold state from the store, without writing it straight back.</summary>
    private void ReadCollapse() {
        _applyingCollapse = true;
        IsCollapsed = Collapse?.IsCollapsed(WidgetId) ?? false;
        _applyingCollapse = false;
        PseudoClasses.Set(":collapsed", IsCollapsed);
    }

    /// <summary>Follows one store at a time, so a page that reopens a card — a search jump landing in
    /// a folded one — is reflected on a panel already on screen.</summary>
    private void Listen(WidgetCollapse? store) {
        if (ReferenceEquals(_listening, store))
            return;

        if (_listening is not null)
            _listening.Changed -= OnCollapseChanged;

        _listening = store;

        if (_listening is not null)
            _listening.Changed += OnCollapseChanged;
    }

    private void OnCollapseChanged(string id) {
        if (id == WidgetId)
            ReadCollapse();
    }
}
