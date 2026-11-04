using DashDetective.Tabs.Hardware;
using System.Linq;
using Xunit;

namespace DashDetective.Tests.Tabs.Hardware;

/// <summary>
/// Covers <see cref="HardwareViewModel.ReportRowsOf"/>: the exported report carries one row per card, so
/// every Graphics adapter appears, in the order the page shows them.
/// </summary>
public sealed class HardwareViewModelTests {
    // Null icons: building the real ones needs a render backend, and the rows never read them.
    private static HardwareCard Card(string id, string title, string subtitle) =>
        new(id, title, subtitle, null!, null!, null!, []);

    [Fact]
    public void ReportRowsOf_TwoGraphicsCards_HoldsBothInPageOrder() {
        var cards = new[] {
            Card("hardware.processor", "Processor", "Ryzen"),
            Card("hardware.graphics.0", "Graphics", "Radeon iGPU"),
            Card("hardware.graphics.1", "Graphics", "GeForce RTX"),
            Card("hardware.motherboard", "Motherboard", "B550"),
        };

        var rows = HardwareViewModel.ReportRowsOf(cards);

        Assert.Equal(
            new[] { ("Processor", "Ryzen"), ("Graphics", "Radeon iGPU"), ("Graphics", "GeForce RTX"), ("Motherboard", "B550") },
            rows.ToArray());
    }

    [Fact]
    public void ReportRowsOf_ReadsTheSubtitleAsItIsNow() {
        var card = Card("hardware.graphics.0", "Graphics", "—");
        card.Subtitle = "GeForce RTX";

        Assert.Equal(("Graphics", "GeForce RTX"), Assert.Single(HardwareViewModel.ReportRowsOf([card])));
    }
}
