using System;
using Avalonia.Controls;

namespace DashDetective.Shell.Navigation;

/// <summary>
/// The shell's navigation bar. A self-contained component bound to a <see cref="NavigationViewModel"/>;
/// embedded directly by the shell rather than routed through the <c>ViewLocator</c>.
/// </summary>
public partial class NavigationView : UserControl {
    private NavigationViewModel? _viewModel;

    public NavigationView() {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    // Bridge the view model's UI-only PositionPicked signal to dismissing the picker flyout: selecting
    // a dock position (or the current one) closes the menu, matching normal menu behaviour. Click-off
    // dismissal is already handled by the flyout's light-dismiss.
    private void OnDataContextChanged(object? sender, EventArgs e) {
        if (_viewModel is not null)
            _viewModel.PositionPicked -= ClosePositionFlyout;

        _viewModel = DataContext as NavigationViewModel;

        if (_viewModel is not null)
            _viewModel.PositionPicked += ClosePositionFlyout;
    }

    private void ClosePositionFlyout() => PositionButton.Flyout?.Hide();
}
