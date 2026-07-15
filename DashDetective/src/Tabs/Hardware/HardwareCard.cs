using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DashDetective.Tabs.Hardware;

/// <summary>
/// One component card in the Hardware grid: a tinted icon tile + title/subtitle header over a list
/// of <see cref="HardwareSpec"/> rows (matching the design comp's card). The <see cref="Title"/>,
/// icon and colours are fixed; the <see cref="Subtitle"/> and each row's value are observable so a
/// provider can populate them after the async read. <see cref="Rows"/> is an
/// <see cref="ObservableCollection{T}"/> so a card whose rows vary at runtime (Storage Devices —
/// one row per detected drive) can rebuild them.
/// </summary>
public sealed partial class HardwareCard : ObservableObject {
    public string Title { get; }

    [ObservableProperty]
    private string _subtitle;

    public Geometry Icon { get; }
    public IBrush IconColor { get; }
    public IBrush IconBackground { get; }
    public ObservableCollection<HardwareSpec> Rows { get; }

    public HardwareCard(
        string title, string subtitle, Geometry icon, IBrush iconColor, IBrush iconBackground,
        IEnumerable<HardwareSpec> rows) {
        Title = title;
        _subtitle = subtitle;
        Icon = icon;
        IconColor = iconColor;
        IconBackground = iconBackground;
        Rows = new ObservableCollection<HardwareSpec>(rows);
    }

    /// <summary>Sets the value of the row with the given key, if present. No-op if the key is absent,
    /// so a provider can address rows by their fixed labels without index bookkeeping.</summary>
    public void SetRow(string key, string value) {
        foreach (var row in Rows) {
            if (row.Key == key) {
                row.Value = value;
                return;
            }
        }
    }
}
