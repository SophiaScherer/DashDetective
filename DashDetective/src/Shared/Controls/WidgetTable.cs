using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace DashDetective.Shared.Controls;

/// <summary>
/// A table's chrome inside a widget: a column header that stays put above a body that scrolls, with
/// the scrollbar gutter applied to both so the columns cannot drift apart.
///
/// Only the chrome. Columns, sorting and the row template stay with the call site, which is what keeps
/// them compile-checked; the four tables in the app agree on far less than they appear to.
/// </summary>
public class WidgetTable : ContentControl {
    /// <summary>The column header row. Sits outside the scroller, so it spans the full width.</summary>
    public static readonly StyledProperty<object?> HeaderProperty =
        AvaloniaProperty.Register<WidgetTable, object?>(nameof(Header));

    /// <summary>Right inset the header and the rows share. Avalonia lays content out underneath the
    /// scrollbar, so without it the bar sits over the last column.</summary>
    public static readonly StyledProperty<Thickness> GutterProperty =
        AvaloniaProperty.Register<WidgetTable, Thickness>(nameof(Gutter), new Thickness(0, 0, 14, 0));

    /// <summary>Height at which the body starts scrolling. Unset lets it grow and the page scroll.</summary>
    public static readonly StyledProperty<double> MaxBodyHeightProperty =
        AvaloniaProperty.Register<WidgetTable, double>(
            nameof(MaxBodyHeight), double.PositiveInfinity);

    /// <summary>Disabled by default, so an unbounded table does not nest a second scroller that would
    /// swallow the wheel from the page.</summary>
    public static readonly StyledProperty<ScrollBarVisibility> BodyScrollBarsProperty =
        AvaloniaProperty.Register<WidgetTable, ScrollBarVisibility>(
            nameof(BodyScrollBars), ScrollBarVisibility.Disabled);

    public object? Header {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public Thickness Gutter {
        get => GetValue(GutterProperty);
        set => SetValue(GutterProperty, value);
    }

    public double MaxBodyHeight {
        get => GetValue(MaxBodyHeightProperty);
        set => SetValue(MaxBodyHeightProperty, value);
    }

    public ScrollBarVisibility BodyScrollBars {
        get => GetValue(BodyScrollBarsProperty);
        set => SetValue(BodyScrollBarsProperty, value);
    }

    /// <summary>Returns the body to the top. The Network pager calls it so an explicit page change
    /// starts at row one, where the live refresh deliberately keeps the offset.</summary>
    public void ScrollToTop() {
        if (_body is not null)
            _body.Offset = new Vector(_body.Offset.X, 0);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e) {
        base.OnApplyTemplate(e);
        _body = e.NameScope.Find<ScrollViewer>("PART_BodyScroll");
    }

    private ScrollViewer? _body;
}
