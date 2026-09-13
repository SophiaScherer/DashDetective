using Avalonia.Media;
using DashDetective.Services.Network;
using DashDetective.Services.SystemMetrics;
using DashDetective.Services.Theming;
using DashDetective.Tabs.Performance;
using DashDetective.Tests.Fakes;
using System.Linq;
using Xunit;

namespace DashDetective.Tests.Tabs.Performance;

/// <summary>
/// Covers the Performance rail's colours. The tab used to parse its own hex literals, so the app held two
/// answers to "what colour is CPU" — one that followed the graph colors (the Dashboard's ChartCpu key) and
/// one that never did. These pin the single answer: every row resolves through <see cref="ChartPalette"/>,
/// and re-resolves when the graph colors move it.
/// </summary>
public class PerformancePaletteTests {
    private static (PerformanceViewModel ViewModel, ThemeService Theme) Page() {
        var samplers = new MetricSamplers(
            () => 0, () => new MemorySample(0, 0, 0, 0, 0), () => new NetworkSample(0, 0), () => "TestNIC");
        var theme = new ThemeService();
        var viewModel = new PerformanceViewModel(
            new SystemMetricsService(samplers, () => new FakeUiTimer()),
            StubHardwareProviders.With(),
            () => new FakeGpuUsageSampler(),
            theme);
        return (viewModel, theme);
    }

    private static ResourceRow Row(PerformanceViewModel viewModel, ChartSeries series) =>
        viewModel.Resources.First(r => r.Series == series);

    private static Color ColorOf(IBrush? brush) => Assert.IsAssignableFrom<ISolidColorBrush>(brush).Color;

    [Fact]
    public void Rows_StartOnTheAuthoredPalette() {
        var (viewModel, _) = Page();

        Assert.Equal(ChartPalette.Default.Cpu, ColorOf(Row(viewModel, ChartSeries.Cpu).ValueBrush));
        Assert.Equal(ChartPalette.Default.Memory, ColorOf(Row(viewModel, ChartSeries.Memory).ValueBrush));
        Assert.Equal(ChartPalette.Default.NetDown, ColorOf(Row(viewModel, ChartSeries.NetDown).ValueBrush));
    }

    [Fact]
    public void ApplyGraphColors_RetintsEveryRow() {
        var (viewModel, theme) = Page();
        var colors = GraphColors.All.First(c => c.Name == "Orange");

        theme.ApplyGraphColors(colors);

        var expected = ChartPalette.Derive(colors.Hue);
        Assert.Equal(expected.Cpu, ColorOf(Row(viewModel, ChartSeries.Cpu).ValueBrush));
        Assert.Equal(expected.Memory, ColorOf(Row(viewModel, ChartSeries.Memory).ValueBrush));
    }

    /// <summary>The two-series row is the one the old behaviour ruined: receive and send share an axis, so
    /// one colour for both left them indistinguishable.</summary>
    [Fact]
    public void ApplyGraphColors_LeavesTheNetworkRowsTwoSeriesOnDifferentColours() {
        var (viewModel, theme) = Page();

        foreach (var colors in GraphColors.All) {
            theme.ApplyGraphColors(colors);

            var row = Row(viewModel, ChartSeries.NetDown);
            Assert.NotEqual(ColorOf(row.ValueBrush), ColorOf(row.ValueBrush2));
        }
    }

    [Fact]
    public void ApplyGraphColors_Null_RestoresTheAuthoredPalette() {
        var (viewModel, theme) = Page();
        theme.ApplyGraphColors(GraphColors.All.First(c => c.Name == "Green"));

        theme.ApplyGraphColors(null);

        Assert.Equal(ChartPalette.Default.Cpu, ColorOf(Row(viewModel, ChartSeries.Cpu).ValueBrush));
        Assert.Equal(ChartPalette.Default.Memory, ColorOf(Row(viewModel, ChartSeries.Memory).ValueBrush));
    }

    /// <summary>A disposed page must stop hearing the service, or a re-created tab would leave the old one
    /// alive through the event.</summary>
    [Fact]
    public void Dispose_StopsFollowingThePalette() {
        var (viewModel, theme) = Page();
        var before = Row(viewModel, ChartSeries.Cpu).ValueBrush;

        viewModel.Dispose();
        theme.ApplyGraphColors(GraphColors.All.First(c => c.Name == "Purple"));

        Assert.Same(before, Row(viewModel, ChartSeries.Cpu).ValueBrush);
    }
}
