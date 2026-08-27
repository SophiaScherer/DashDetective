using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Media;
using DashDetective.Shared.Controls;
using System.Windows.Input;

namespace DashDetective.Tabs.Network;

/// <summary>
/// A network probe panel: an editable target, a submit button, and two lines of terminal output. Ping
/// and DNS Lookup are the same panel with different labels and colours, so the markup lives here once.
///
/// Kept in the Network tab folder rather than <c>src/Shared</c>: both users are this one feature, which
/// is the promotion bar the architecture doc sets.
/// </summary>
public partial class ConsolePanel : UserControl, IWidgetIdentity {
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<ConsolePanel, string?>(nameof(Title));

    /// <summary>Forwarded to the inner WidgetPanel: the two instances are separate widgets, so each
    /// needs its own identity rather than sharing this control's.</summary>
    public static readonly StyledProperty<string?> WidgetIdProperty =
        AvaloniaProperty.Register<ConsolePanel, string?>(nameof(WidgetId));

    public static readonly StyledProperty<string> TargetProperty =
        AvaloniaProperty.Register<ConsolePanel, string>(
            nameof(Target), defaultValue: "", defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<string?> PlaceholderProperty =
        AvaloniaProperty.Register<ConsolePanel, string?>(nameof(Placeholder));

    public static readonly StyledProperty<string?> ButtonTextProperty =
        AvaloniaProperty.Register<ConsolePanel, string?>(nameof(ButtonText));

    /// <summary>Run by the button.</summary>
    public static readonly StyledProperty<ICommand?> SubmitCommandProperty =
        AvaloniaProperty.Register<ConsolePanel, ICommand?>(nameof(SubmitCommand));

    /// <summary>Run by Enter in the target field. Not always the same as
    /// <see cref="SubmitCommand"/>: ping applies the target and starts, where the button toggles.</summary>
    public static readonly StyledProperty<ICommand?> EnterCommandProperty =
        AvaloniaProperty.Register<ConsolePanel, ICommand?>(nameof(EnterCommand));

    public static readonly StyledProperty<string?> ConsoleTextProperty =
        AvaloniaProperty.Register<ConsolePanel, string?>(nameof(ConsoleText));

    public static readonly StyledProperty<IBrush?> ConsoleBrushProperty =
        AvaloniaProperty.Register<ConsolePanel, IBrush?>(nameof(ConsoleBrush));

    /// <summary>The second, summary line under the output.</summary>
    public static readonly StyledProperty<string?> FooterTextProperty =
        AvaloniaProperty.Register<ConsolePanel, string?>(nameof(FooterText));

    public static readonly StyledProperty<IBrush?> FooterBrushProperty =
        AvaloniaProperty.Register<ConsolePanel, IBrush?>(nameof(FooterBrush));

    public ConsolePanel() {
        InitializeComponent();
    }

    public string? Title {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string? WidgetId {
        get => GetValue(WidgetIdProperty);
        set => SetValue(WidgetIdProperty, value);
    }

    public string Target {
        get => GetValue(TargetProperty);
        set => SetValue(TargetProperty, value);
    }

    public string? Placeholder {
        get => GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public string? ButtonText {
        get => GetValue(ButtonTextProperty);
        set => SetValue(ButtonTextProperty, value);
    }

    public ICommand? SubmitCommand {
        get => GetValue(SubmitCommandProperty);
        set => SetValue(SubmitCommandProperty, value);
    }

    public ICommand? EnterCommand {
        get => GetValue(EnterCommandProperty);
        set => SetValue(EnterCommandProperty, value);
    }

    public string? ConsoleText {
        get => GetValue(ConsoleTextProperty);
        set => SetValue(ConsoleTextProperty, value);
    }

    public IBrush? ConsoleBrush {
        get => GetValue(ConsoleBrushProperty);
        set => SetValue(ConsoleBrushProperty, value);
    }

    public string? FooterText {
        get => GetValue(FooterTextProperty);
        set => SetValue(FooterTextProperty, value);
    }

    public IBrush? FooterBrush {
        get => GetValue(FooterBrushProperty);
        set => SetValue(FooterBrushProperty, value);
    }
}
