using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using DashDetective.Services.Settings;
using DashDetective.Services.SystemMetrics;
using DashDetective.Shell;
using System;

namespace DashDetective;

public partial class App : Application {
    // The single window, held so the tray menu (defined in App.axaml) can show/exit it.
    private MainWindow? _mainWindow;

    public override void Initialize() {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted() {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
            // Manual composition root: load persisted settings, build the shared metrics service, hand
            // both to the shell view model, and dispose it on shutdown so timers, subscriptions, PDH
            // handles and the (flushed) settings store are released.
            var store = new SettingsStore();
            var settings = store.Load();
            var metrics = new SystemMetricsService();
            var viewModel = new MainWindowViewModel(metrics, store, settings);

            _mainWindow = new MainWindow { DataContext = viewModel };
            desktop.MainWindow = _mainWindow;
            desktop.ShutdownRequested += (_, _) => viewModel.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>Tray "Show" (and left-click): restore and focus the window.</summary>
    private void OnTrayShow(object? sender, EventArgs e) => _mainWindow?.ShowFromTray();

    /// <summary>Tray "Exit": really close the window (bypassing hide-to-tray), which shuts the app down.</summary>
    private void OnTrayExit(object? sender, EventArgs e) => _mainWindow?.ExitFromTray();
}
