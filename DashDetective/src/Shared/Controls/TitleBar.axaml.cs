using Avalonia;
using Avalonia.Controls;

namespace DashDetective.Shared.Controls;

/// <summary>
/// A window's custom title bar: the app mark and the window's title, laid over the caption band.
/// It follows the window it sits in rather than any view model, so a second window gets one by placing
/// it, and it hides itself whenever that window is not extended — off Windows, under a contrast theme,
/// or wherever the platform refused.
///
/// It must sit OUTSIDE the window's <see cref="ScaleHost"/>: the caption buttons are drawn at the OS's
/// scale, and a bar that followed the interface size would stop lining up with them.
/// </summary>
public partial class TitleBar : UserControl {
    private Window? _window;

    public TitleBar() {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e) {
        base.OnAttachedToVisualTree(e);

        _window = TopLevel.GetTopLevel(this) as Window;
        if (_window is null)
            return;

        _window.PropertyChanged += OnWindowPropertyChanged;
        Refresh(_window);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e) {
        base.OnDetachedFromVisualTree(e);

        if (_window is not null)
            _window.PropertyChanged -= OnWindowPropertyChanged;
        _window = null;
    }

    private void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e) {
        if (sender is Window window &&
            (e.Property == Window.IsExtendedIntoWindowDecorationsProperty ||
             e.Property == Window.WindowDecorationMarginProperty ||
             e.Property == Window.OffScreenMarginProperty ||
             e.Property == Window.TitleProperty))
            Refresh(window);
    }

    /// <summary>A minimum rather than a height, so text grown by the text-size setting grows the bar
    /// instead of being clipped by it; the caption buttons keep the band's own height.</summary>
    private void Refresh(Window window) {
        IsVisible = TitleBarRules.IsShown(window.IsExtendedIntoWindowDecorations, window.WindowDecorationMargin);
        Bar.MinHeight = TitleBarRules.Height(window.WindowDecorationMargin, window.OffScreenMargin);
        Bar.Padding = TitleBarRules.Padding(window.OffScreenMargin);
        TitleText.Text = window.Title;
    }
}
