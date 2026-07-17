using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using DashDetective.Shared;

namespace DashDetective.Tabs.Performance;

/// <summary>
/// The Performance tab: a Task-Manager-style live resource drill-down. A left resource-selector rail
/// swaps a right detail pane (one large utilization chart + a stat-tile strip).
///
/// This is the initial UI implementation, driven by static mock data — the resource list is seeded from
/// the design comp; the chart and stat tiles land in later UI phases. Live sampling/providers (and the
/// <see cref="IRefreshablePage"/> / <see cref="ILiveSamplingPage"/> wiring) are a later technical pass.
///
/// Implements <see cref="ISelfScrollingPage"/> so the shell hosts it in the bounded, non-scrolling
/// container: the tab fills the viewport and manages its own panes (like File Explorer).
/// </summary>
public partial class PerformanceViewModel : ViewModelBase, ISelfScrollingPage {
    // Fixed semantic per-metric legend colours (theme/accent-independent by design), matching the design
    // comp's palette — parsed like MainWindowViewModel's live dots.
    private static IBrush Brush(string hex) => new SolidColorBrush(Color.Parse(hex));

    /// <summary>The resource rows shown in the left rail, in display order.</summary>
    public ObservableCollection<ResourceRow> Resources { get; } = new();

    /// <summary>The currently selected resource, whose detail the right pane shows.</summary>
    [ObservableProperty] private ResourceRow _selectedResource = null!;

    public PerformanceViewModel() {
        // Static mock data from the design comp (perfDefs). Real values/history are a later technical
        // pass. Each chart series is a deterministic mock wave around the resource's level.
        Resources.Add(new ResourceRow("CPU", "24 cores · 3.2 GHz", "Intel Core i9-14900K",
                                      "23", "%", Brush("#4cc2ff"), MockPoints(23, 16, 0), Select));
        Resources.Add(new ResourceRow("Memory", "19.5 / 32 GB", "DDR5-6000 · 2 slots",
                                      "61", "%", Brush("#c58fff"), MockPoints(61, 6, 11), Select));
        Resources.Add(new ResourceRow("Disk 0 (C:)", "NVMe SSD", "Samsung 990 Pro 2TB",
                                      "4", "%", Brush("#ffcf4d"), MockPoints(9, 18, 5), Select));
        Resources.Add(new ResourceRow("GPU", "RTX 4080", "16 GB GDDR6X · 52°C",
                                      "12", "%", Brush("#6ccb5f"), MockPoints(14, 12, 23), Select));
        Resources.Add(new ResourceRow("Ethernet", "2.5 GbE", "Intel I225-V",
                                      "48", "Mbps", Brush("#4cc2ff"), MockPoints(40, 26, 7), Select));

        SelectedResource = Resources[0];
        SelectedResource.IsSelected = true;
    }

    /// <summary>
    /// Builds a 60-point mock utilization series as a Sparkline "x,y x,y …" string for a fixed 0–100
    /// axis. Values oscillate deterministically around <paramref name="level"/> (± ~<paramref
    /// name="amp"/>); each y is flipped to <c>100 − value</c> so higher utilization draws at the top,
    /// matching the Dashboard charts' convention. Placeholder data only — replaced by live history in
    /// the later technical pass.
    /// </summary>
    private static string MockPoints(double level, double amp, double seed) {
        var sb = new StringBuilder();
        for (var i = 0; i < 60; i++) {
            var value = level
                        + amp * Math.Sin((i + seed) * 0.40)
                        + amp * 0.5 * Math.Sin((i + seed) * 0.13);
            value = Math.Clamp(value, 0, 100);
            if (i > 0)
                sb.Append(' ');
            sb.Append(i.ToString(CultureInfo.InvariantCulture));
            sb.Append(',');
            sb.Append((100 - value).ToString("0.##", CultureInfo.InvariantCulture));
        }
        return sb.ToString();
    }

    /// <summary>Selects a resource (single-select) so the detail pane swaps to it. No-ops when the
    /// resource is already selected. Same idiom as <c>NavigationViewModel.Navigate</c>.</summary>
    private void Select(ResourceRow row) {
        if (row == SelectedResource)
            return;

        SelectedResource.IsSelected = false;
        SelectedResource = row;
        row.IsSelected = true;
    }
}
