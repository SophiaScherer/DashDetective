using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using System.Windows.Input;

namespace DashDetective.Shared.Controls;

/// <summary>
/// A search / filter field: magnifier, input, and a clear × that appears once something is typed. The
/// toolbar search, the process filter and the Toolkit command filter each drew this by hand, repeating
/// the same two path geometries; it lives here once instead.
///
/// Sizes are properties because the three differ by role rather than by accident — a toolbar field is
/// deliberately larger than an inline list filter. Set <see cref="Control.Height"/> and
/// <see cref="Layoutable.Width"/> on the control itself.
/// </summary>
public partial class SearchField : UserControl {
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<SearchField, string>(
            nameof(Text), defaultValue: "", defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The full string to complete to, or null for a field that offers no suggestions.</summary>
    public static readonly StyledProperty<string?> CompletionProperty =
        AvaloniaProperty.Register<SearchField, string?>(nameof(Completion));

    public static readonly StyledProperty<string?> PlaceholderTextProperty =
        AvaloniaProperty.Register<SearchField, string?>(nameof(PlaceholderText));

    /// <summary>Run by the clear ×.</summary>
    public static readonly StyledProperty<ICommand?> ClearCommandProperty =
        AvaloniaProperty.Register<SearchField, ICommand?>(nameof(ClearCommand));

    /// <summary>Run when Enter is pressed in the field.</summary>
    public static readonly StyledProperty<ICommand?> EnterCommandProperty =
        AvaloniaProperty.Register<SearchField, ICommand?>(nameof(EnterCommand));

    public static readonly StyledProperty<string?> ClearToolTipProperty =
        AvaloniaProperty.Register<SearchField, string?>(nameof(ClearToolTip), defaultValue: "Clear");

    public static readonly StyledProperty<double> IconSizeProperty =
        AvaloniaProperty.Register<SearchField, double>(nameof(IconSize), defaultValue: 14);

    public static readonly StyledProperty<double> TextSizeProperty =
        AvaloniaProperty.Register<SearchField, double>(nameof(TextSize), defaultValue: 13);

    /// <summary>Inset from the field's border to its contents.</summary>
    public static readonly StyledProperty<Thickness> ContentPaddingProperty =
        AvaloniaProperty.Register<SearchField, Thickness>(
            nameof(ContentPadding), defaultValue: new Thickness(11, 0));

    /// <summary>Gap between the magnifier and the input.</summary>
    public static readonly StyledProperty<Thickness> IconGapProperty =
        AvaloniaProperty.Register<SearchField, Thickness>(
            nameof(IconGap), defaultValue: new Thickness(9, 0, 0, 0));

    public SearchField() {
        InitializeComponent();
    }

    public string Text {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string? Completion {
        get => GetValue(CompletionProperty);
        set => SetValue(CompletionProperty, value);
    }

    public string? PlaceholderText {
        get => GetValue(PlaceholderTextProperty);
        set => SetValue(PlaceholderTextProperty, value);
    }

    public ICommand? ClearCommand {
        get => GetValue(ClearCommandProperty);
        set => SetValue(ClearCommandProperty, value);
    }

    public ICommand? EnterCommand {
        get => GetValue(EnterCommandProperty);
        set => SetValue(EnterCommandProperty, value);
    }

    public string? ClearToolTip {
        get => GetValue(ClearToolTipProperty);
        set => SetValue(ClearToolTipProperty, value);
    }

    public double IconSize {
        get => GetValue(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }

    public double TextSize {
        get => GetValue(TextSizeProperty);
        set => SetValue(TextSizeProperty, value);
    }

    public Thickness ContentPadding {
        get => GetValue(ContentPaddingProperty);
        set => SetValue(ContentPaddingProperty, value);
    }

    public Thickness IconGap {
        get => GetValue(IconGapProperty);
        set => SetValue(IconGapProperty, value);
    }

    /// <summary>Forwarded from the inner box: puts the caret in the field and selects what is there, so
    /// a second press of the focus shortcut replaces the term rather than appending to it.</summary>
    public void FocusAndSelectAll() => Box.FocusAndSelectAll();

    /// <summary>Forwarded from the inner box: takes the caret out without moving it into anything else,
    /// so the shell stops suppressing bare-key shortcuts.</summary>
    public void ReleaseFocus() => Box.ReleaseFocus();
}
