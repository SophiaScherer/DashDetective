using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using DashDetective.Services.SystemMetrics;
using DashDetective.Shell;

namespace DashDetective;

public partial class App : Application {
    public override void Initialize() {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted() {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
            // Manual composition root: build the shared metrics service, hand it to the shell view model,
            // and dispose both on shutdown so timers, subscriptions and PDH handles are released.
            var metrics = new SystemMetricsService();
            var viewModel = new MainWindowViewModel(metrics);
            desktop.MainWindow = new MainWindow { DataContext = viewModel };
            desktop.ShutdownRequested += (_, _) => viewModel.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
