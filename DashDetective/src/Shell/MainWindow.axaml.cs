using Avalonia.Controls;
using Avalonia.Interactivity;
using DashDetective.Services.Diagnostics;
using DashDetective.Shared;
using DashDetective.Shared.Shortcuts;
using DashDetective.Shell.Shortcuts;
using DashDetective.Shell.TrayNotice;
using System;
using System.ComponentModel;
using System.Threading.Tasks;

namespace DashDetective.Shell;

public partial class MainWindow : Window {
    // Set by the tray "Exit" so a subsequent close actually exits instead of hiding to tray.
    private bool _exitRequested;

    // The window-wide keyboard listener. The view model is dispatched to through the DataContext at
    // press time rather than captured here, because the composition root assigns it after construction.
    private readonly ShellShortcutHandler _shortcuts;

    // The view model the export request is currently wired to. Tracked because the composition root
    // assigns the DataContext after construction.
    private MainWindowViewModel? _viewModel;

    public MainWindow() {
        InitializeComponent();
        Closing += OnClosing;
        DataContextChanged += OnDataContextChanged;
        _shortcuts = new ShellShortcutHandler(
            this,
            () => (DataContext as MainWindowViewModel)?.ActiveScope ?? ShortcutScope.Global,
            id => (DataContext as MainWindowViewModel)?.HandleShortcut(id) ?? false);
        Closed += (_, _) => _shortcuts.Dispose();
    }

    // An expanded 236px rail leaves too little for the page on a narrow window, so the bar folds
    // itself away. Reported from here because there is no converter-free path from the window's size
    // to a view model property.
    private void OnShellSizeChanged(object? sender, SizeChangedEventArgs e) =>
        _viewModel?.Nav.SetShellWidth(e.NewSize.Width);

    private void OnDataContextChanged(object? sender, EventArgs e) {
        if (_viewModel is not null)
            _viewModel.ExportRequested -= OnExportRequested;

        _viewModel = DataContext as MainWindowViewModel;

        if (_viewModel is not null)
            _viewModel.ExportRequested += OnExportRequested;
    }

    /// <summary>
    /// Close handler: when the "Show in system tray" setting is on, closing the window hides it to the
    /// tray (the app keeps running) rather than exiting. A close driven by the tray "Exit" item, or a
    /// close while the setting is off, proceeds normally — the last window closing shuts the app down,
    /// which runs the composition root's disposal (flushing settings, releasing timers/PDH handles).
    ///
    /// The very first such hide says so first: an app that goes on sampling behind a closed window has
    /// to disclose that at least once.
    /// </summary>
    private void OnClosing(object? sender, WindowClosingEventArgs e) {
        if (_exitRequested || DataContext is not MainWindowViewModel { ShowInTray: true } vm)
            return;

        e.Cancel = true;
        if (vm.NeedsTrayNotice)
            _ = ConfirmTrayAsync(vm);
        else
            HideToTray(vm);
    }

    /// <summary>Shows the one-time tray notice and acts on the answer. Split out of
    /// <see cref="OnClosing"/> because a closing handler cannot await — the same split
    /// <see cref="ExportReportAsync"/> gets. The close is already cancelled, so this window stays on
    /// screen underneath the dialog, which is the whole point of asking before hiding rather than after.
    /// </summary>
    private async Task ConfirmTrayAsync(MainWindowViewModel vm) {
        var keepRunning = await TrayNoticeWindow.AskAsync(this);
        vm.MarkTrayNoticeShown();

        if (keepRunning)
            HideToTray(vm);
        else
            ExitFromTray();
    }

    /// <summary>Hides the window and idles the pages behind it — nothing should sample while nobody can
    /// see it.</summary>
    private void HideToTray(MainWindowViewModel vm) {
        Hide();
        vm.SetWindowVisible(false);
    }

    /// <summary>Restores and focuses the window from the tray, resuming the current page.</summary>
    public void ShowFromTray() {
        Show();
        Activate();
        (DataContext as MainWindowViewModel)?.SetWindowVisible(true);
    }

    /// <summary>Really exits from the tray: closes the window (bypassing hide-to-tray).</summary>
    public void ExitFromTray() {
        _exitRequested = true;
        Close();
    }

    /// <summary>The toolbar's Export button.</summary>
    private async void OnExportClick(object? sender, RoutedEventArgs e) => await ExportReportAsync();

    /// <summary>The Export keyboard shortcut, routed here by the view model. Not <c>async void</c>: this
    /// is a plain <c>Action</c> subscriber, not a routed event handler, so the fire-and-forget is written
    /// out rather than hidden in the signature. <see cref="ExportReportAsync"/> never throws.</summary>
    private void OnExportRequested() => _ = ExportReportAsync();

    /// <summary>Exports the current system snapshot, in whichever format the chosen filename asks for.
    /// The dialog itself is <see cref="FileSave"/>, shared with the Settings export buttons.</summary>
    private Task ExportReportAsync() {
        if (DataContext is not MainWindowViewModel vm)
            return Task.CompletedTask;

        return FileSave.SaveAsync(
            this,
            title: "Export system report",
            suggestedName: $"DashDetective-report-{DateTime.Now:yyyyMMdd-HHmmss}",
            formats: DiagnosticsFormats.Offered,
            content: vm.BuildReport);
    }
}
