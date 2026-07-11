using System;
using System.Windows.Input;
using Avalonia.Layout;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DashDetective.Shell.Navigation;

/// <summary>
/// A selectable navigation-position entry (Left / Top / Right / Bottom), used by both the on-bar
/// orientation flyout and the Settings → Appearance control. Mirrors the <c>ThemeOption</c> pattern:
/// an observable <see cref="IsSelected"/> flag plus a command that reports the click back to the
/// owning view-model. The <c>Bar*</c> members describe a little "panel on this edge" preview tile
/// (an accent bar aligned to the docked edge) with no value converters.
/// </summary>
public partial class NavPositionOption : ObservableObject {
    public NavPositionOption(string label, NavOrientation value, Action<NavPositionOption> onSelected) {
        Label = label;
        Value = value;
        SelectCommand = new RelayCommand(() => onSelected(this));
    }

    public string Label { get; }
    public NavOrientation Value { get; }
    public ICommand SelectCommand { get; }

    [ObservableProperty] private bool _isSelected;

    private bool IsVerticalEdge => Value is NavOrientation.Left or NavOrientation.Right;

    /// <summary>Preview-tile accent bar width: a thin vertical strip for left/right, else full width.</summary>
    public double BarWidth => IsVerticalEdge ? 4 : double.NaN;

    /// <summary>Preview-tile accent bar height: a thin horizontal strip for top/bottom, else full height.</summary>
    public double BarHeight => IsVerticalEdge ? double.NaN : 4;

    public HorizontalAlignment BarHAlign => Value switch {
        NavOrientation.Left => HorizontalAlignment.Left,
        NavOrientation.Right => HorizontalAlignment.Right,
        _ => HorizontalAlignment.Stretch,
    };

    public VerticalAlignment BarVAlign => Value switch {
        NavOrientation.Top => VerticalAlignment.Top,
        NavOrientation.Bottom => VerticalAlignment.Bottom,
        _ => VerticalAlignment.Stretch,
    };
}
