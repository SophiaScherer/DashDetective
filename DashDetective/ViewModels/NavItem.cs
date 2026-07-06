using System;
using System.Windows.Input;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DashDetective.ViewModels;

/// <summary>
/// A single entry in the sidebar navigation. Carries the page it activates plus
/// the visual state (colours, indicator, weight) that changes with selection.
/// </summary>
public partial class NavItem : ObservableObject {
    private static readonly IBrush Accent = new SolidColorBrush(Color.Parse("#4cc2ff"));
    private static readonly IBrush SelectedBackground = new SolidColorBrush(Color.Parse("#4cc2ff"), 0.12);
    private static readonly IBrush SelectedText = new SolidColorBrush(Colors.White);
    private static readonly IBrush UnselectedText = new SolidColorBrush(Colors.White, 0.72);
    private static readonly IBrush UnselectedIcon = new SolidColorBrush(Colors.White, 0.60);

    public NavItem(string label, string title, string subtitle, Geometry icon,
                   ViewModelBase page, Action<NavItem> onSelected) {
        Label = label;
        Title = title;
        Subtitle = subtitle;
        Icon = icon;
        Page = page;
        SelectCommand = new RelayCommand(() => onSelected(this));
    }

    public string Label { get; }
    public string Title { get; }
    public string Subtitle { get; }
    public Geometry Icon { get; }
    public ViewModelBase Page { get; }
    public ICommand SelectCommand { get; }

    [ObservableProperty] private bool _isSelected;

    public IBrush RowBackground => IsSelected ? SelectedBackground : Brushes.Transparent;
    public IBrush TextForeground => IsSelected ? SelectedText : UnselectedText;
    public IBrush IconBrush => IsSelected ? Accent : UnselectedIcon;
    public double IndicatorHeight => IsSelected ? 18 : 0;
    public FontWeight LabelWeight => IsSelected ? FontWeight.SemiBold : FontWeight.Normal;

    partial void OnIsSelectedChanged(bool value) {
        OnPropertyChanged(nameof(RowBackground));
        OnPropertyChanged(nameof(TextForeground));
        OnPropertyChanged(nameof(IconBrush));
        OnPropertyChanged(nameof(IndicatorHeight));
        OnPropertyChanged(nameof(LabelWeight));
    }
}
