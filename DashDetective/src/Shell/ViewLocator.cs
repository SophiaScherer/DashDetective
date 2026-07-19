using Avalonia.Controls;
using Avalonia.Controls.Templates;
using DashDetective.Shared;
using DashDetective.Tabs.Dashboard;
using DashDetective.Tabs.FileExplorer;
using DashDetective.Tabs.Hardware;
using DashDetective.Tabs.Network;
using DashDetective.Tabs.Performance;
using DashDetective.Tabs.Processes;
using DashDetective.Tabs.Settings;

namespace DashDetective.Shell;

/// <summary>
/// Maps a page view model to its view via an explicit switch — compile-time and trimming-safe (no
/// reflection). Keeps the "Not Found" fallback for any unmapped view model.
/// </summary>
public class ViewLocator : IDataTemplate {
    public Control? Build(object? param) => param switch {
        DashboardViewModel => new DashboardView(),
        FileExplorerViewModel => new FileExplorerView(),
        ProcessesViewModel => new ProcessesView(),
        PerformanceViewModel => new PerformanceView(),
        NetworkViewModel => new NetworkView(),
        HardwareViewModel => new HardwareView(),
        SettingsViewModel => new SettingsView(),
        null => null,
        _ => new TextBlock { Text = "Not Found: " + param.GetType().FullName },
    };

    public bool Match(object? data) => data is ViewModelBase;
}
