using DashDetective.Services.SystemMetrics;
using System.Collections.Generic;

namespace DashDetective.Tabs.Performance;

/// <summary>
/// A GPU's static details as the rows under its stat tiles, labeled as Task Manager labels them. A fact the
/// adapter does not publish has no row — no dash, no zero — so an adapter with none shows no list at all.
/// </summary>
internal static class GpuDetailRows {
    internal static IReadOnlyList<DetailRow> For(GpuDetails? details) {
        var rows = new List<DetailRow>(3);
        if (details is null)
            return rows;

        Add(rows, "Driver version", details.DriverVersion);
        Add(rows, "DirectX version", details.DirectXVersion);
        Add(rows, "Physical location", details.PhysicalLocation);
        return rows;
    }

    private static void Add(List<DetailRow> rows, string key, string? value) {
        if (!string.IsNullOrWhiteSpace(value))
            rows.Add(new DetailRow(key, value));
    }
}
