using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DashDetective.Tabs.Storage;

/// <summary>
/// One drive summary card in the Storage tab's top row: name + health pill, model subtitle, a usage bar,
/// the used/free split, and Read / Write / Temp readouts (matching the design comp).
///
/// Identity (<see cref="Name"/>, <see cref="Model"/>) is fixed; the health, usage and rate members are
/// observable so the later live pass can update them in place. The pill and usage-bar brushes are fixed
/// semantic colours (green = healthy, amber = caution) seeded with the card by the owning view model.
/// </summary>
public sealed partial class DriveCard : ObservableObject {
    /// <summary>Drive display name, e.g. "Local Disk (C:)".</summary>
    public string Name { get; init; } = "";

    /// <summary>Drive model subtitle, e.g. "Samsung 990 Pro 2TB".</summary>
    public string Model { get; init; } = "";

    /// <summary>Health label shown in the pill ("Healthy" / "Caution").</summary>
    [ObservableProperty] private string _health = "";

    /// <summary>Pill text colour (green = healthy, amber = caution) — a fixed semantic colour.</summary>
    [ObservableProperty] private IBrush? _healthForeground;

    /// <summary>Pill fill colour (a soft tint of the health colour) — a fixed semantic colour.</summary>
    [ObservableProperty] private IBrush? _healthBackground;

    /// <summary>Used fraction of the drive, 0–100, driving the usage bar's value.</summary>
    [ObservableProperty] private double _usagePercent;

    /// <summary>Usage-bar fill colour, keyed to the drive's state — a fixed semantic colour.</summary>
    [ObservableProperty] private IBrush? _barBrush;

    /// <summary>Used capacity, formatted (e.g. "1.36 TB").</summary>
    [ObservableProperty] private string _used = "";

    /// <summary>Free capacity, formatted (e.g. "640 GB").</summary>
    [ObservableProperty] private string _free = "";

    /// <summary>Current read throughput (e.g. "48 MB/s").</summary>
    [ObservableProperty] private string _read = "";

    /// <summary>Current write throughput (e.g. "12 MB/s").</summary>
    [ObservableProperty] private string _write = "";

    /// <summary>Current drive temperature (e.g. "41°C").</summary>
    [ObservableProperty] private string _temp = "";
}
