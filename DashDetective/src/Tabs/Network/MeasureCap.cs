using Avalonia;
using Avalonia.Controls;
using System;

namespace DashDetective.Tabs.Network;

/// <summary>
/// Caps how tall its child ASKS to be, then arranges it into whatever it is handed. A
/// <c>MaxHeight</c> caps both: a stretched child clamped by it is centered in a taller slot, which is
/// how the Adapters list ended up floating between two empty bands inside a card stretched to its
/// row. Here the cap only keeps a long list from growing the row; the card's height decides the rest.
/// </summary>
internal sealed class MeasureCap : Decorator {
    public static readonly StyledProperty<double> HeightCapProperty =
        AvaloniaProperty.Register<MeasureCap, double>(nameof(HeightCap), double.PositiveInfinity);

    static MeasureCap() => AffectsMeasure<MeasureCap>(HeightCapProperty);

    /// <summary>Tallest the child asks for while measuring. Not applied when arranging.</summary>
    public double HeightCap {
        get => GetValue(HeightCapProperty);
        set => SetValue(HeightCapProperty, value);
    }

    /// <summary>The height offered to the child: the available height, held to the cap. An unset
    /// (NaN) cap leaves it alone; a negative one offers nothing.</summary>
    internal static double CappedHeight(double available, double cap) =>
        double.IsNaN(cap) ? available : Math.Min(available, Math.Max(0, cap));

    protected override Size MeasureOverride(Size availableSize) {
        if (Child is null)
            return default;

        // Measured through Padding, as the inherited arrange deflates by it.
        var inner = availableSize.Deflate(Padding);
        Child.Measure(new Size(inner.Width, CappedHeight(inner.Height, HeightCap)));
        return Child.DesiredSize.Inflate(Padding);
    }
}
