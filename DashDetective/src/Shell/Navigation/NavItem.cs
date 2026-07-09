using System;
using System.Windows.Input;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashDetective.Shared;

namespace DashDetective.Shell.Navigation;

/// <summary>
/// A single entry in the sidebar navigation: the page it activates plus its selection state.
/// The selected-state visuals (accent indicator/icon, highlight, weight) are driven from theme
/// resources in MainWindow.axaml via <see cref="IsSelected"/>, so they follow the current theme
/// and accent automatically.
/// </summary>
public partial class NavItem : ObservableObject {
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
}
