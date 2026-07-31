using Avalonia.Controls;
using System;

namespace DashDetective.Tabs.Toolkit;

public partial class ToolkitView : UserControl {
    private ToolkitViewModel? _viewModel;

    public ToolkitView() {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e) {
        if (_viewModel is not null)
            _viewModel.SearchFocusRequested -= FocusSearch;

        _viewModel = DataContext as ToolkitViewModel;

        if (_viewModel is not null)
            _viewModel.SearchFocusRequested += FocusSearch;
    }

    // Focusing selects what's already typed, so a second "/" replaces the term rather than appending
    // to it — as the Processes filter does.
    private void FocusSearch() {
        SearchBox.Focus();
        SearchBox.SelectAll();
    }
}
