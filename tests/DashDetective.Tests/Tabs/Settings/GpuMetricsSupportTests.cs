using DashDetective.Tabs.Settings;
using System;
using Xunit;

namespace DashDetective.Tests.Tabs.Settings;

/// <summary>
/// Covers <see cref="GpuMetricsSupport"/> and the copy that depends on it. The bug it encodes is a
/// correctness one: the row was fully interactive on Windows while describing itself as "Linux only",
/// so the user could turn on a setting that does nothing and be told as much in the same sentence.
/// </summary>
public class GpuMetricsSupportTests {
    /// <summary>Only Linux reads the figure through a helper process; Windows takes it from a
    /// performance counter it is already polling.</summary>
    [Fact]
    public void NeedsHelperTool_OnlyWhereTheFigureCostsAProcess() =>
        Assert.Equal(OperatingSystem.IsLinux(), GpuMetricsSupport.NeedsHelperTool);

    /// <summary>Both arms asserted explicitly, so neither depends on which host runs the suite. Where
    /// there is no helper tool the copy must not describe one — a disabled toggle explaining a
    /// mechanism it will never run is worse than no explanation.</summary>
    [Fact]
    public void NvidiaGpuMetricsFor_DescribesWhatTheSettingActuallyDoes() {
        Assert.Equal("Runs a helper tool every 15 seconds",
                     SettingDescriptions.NvidiaGpuMetricsFor(needsHelperTool: true));
        Assert.Contains("already reads without it",
                        SettingDescriptions.NvidiaGpuMetricsFor(needsHelperTool: false),
                        StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NvidiaGpuMetrics_ResolvesForThisHost() =>
        Assert.Equal(SettingDescriptions.NvidiaGpuMetricsFor(GpuMetricsSupport.NeedsHelperTool),
                     SettingDescriptions.NvidiaGpuMetrics);

    /// <summary>The page's label and the text universal search matches are the same string, so the
    /// catalog must take it from the seam rather than keep the literal it used to hold.</summary>
    [Fact]
    public void Catalog_TakesItsNvidiaDescriptionFromTheSeam() =>
        Assert.Equal(SettingDescriptions.NvidiaGpuMetrics,
                     SettingCatalog.Instance.NvidiaGpuMetrics.Description);

    /// <summary>The reported bug: the description announced "Linux only" on a machine where the row was
    /// still operable. Wherever the helper tool is not what supplies the figure, the copy must not
    /// promise one.</summary>
    [Fact]
    public void Catalog_DoesNotOfferAHelperToolWhereThereIsNone() {
        if (GpuMetricsSupport.NeedsHelperTool)
            return;

        Assert.DoesNotContain("Linux only", SettingCatalog.Instance.NvidiaGpuMetrics.Description,
                              StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("helper tool", SettingCatalog.Instance.NvidiaGpuMetrics.Description,
                              StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The setting stays in the catalog wherever it runs — it is disabled, not removed — so the
    /// search index does not vary by platform and a preference carried between machines survives. A
    /// hidden row would also strand the search result, which reveals a row by finding it in the tree.</summary>
    [Fact]
    public void Catalog_StillOffersTheNvidiaSettingEverywhere() =>
        Assert.Contains(SettingCatalog.Instance.All, e => e.Id == SettingId.NvidiaGpuMetrics);
}
