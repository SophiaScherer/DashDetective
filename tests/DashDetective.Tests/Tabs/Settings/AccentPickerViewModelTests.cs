using Avalonia.Media;
using DashDetective.Services.Theming;
using DashDetective.Tabs.Settings;
using System.Linq;
using Xunit;

namespace DashDetective.Tests.Tabs.Settings;

/// <summary>
/// Covers <see cref="AccentPickerViewModel"/>: the wheel and the hex box stay in step, a preset or a pick
/// only drafts, nothing reaches the theme until Apply, and Cancel returns to what is applied.
/// </summary>
public class AccentPickerViewModelTests {
    private static (AccentPickerViewModel Picker, ThemeService Theme, Counter Applied) Create() {
        var theme = new ThemeService();
        var applied = new Counter();
        return (new AccentPickerViewModel(theme, () => applied.Count++), theme, applied);
    }

    private sealed class Counter {
        public int Count;
    }

    private static AccentSwatchOption Swatch(AccentPickerViewModel picker, string name) =>
        picker.Swatches.First(s => s.Name == name);

    [Fact]
    public void Ctor_DraftsTheAppliedAccent() {
        var (picker, _, _) = Create();

        Assert.Equal(AccentPreset.Default, picker.Draft);
        Assert.Equal("#4cc2ff", picker.HexText);
        Assert.False(picker.IsDirty);
        Assert.False(picker.IsEditorOpen);
        Assert.True(Swatch(picker, "Blue").IsSelected);
        Assert.Single(picker.Swatches, s => s.IsSelected);
    }

    [Fact]
    public void Swatches_ArePresetsThenCustom() {
        var (picker, _, _) = Create();

        Assert.Equal(["Blue", "Green", "Purple", "Orange", AccentPreset.CustomName], picker.Swatches.Select(s => s.Name));
        Assert.True(picker.Swatches[^1].IsCustom);
    }

    [Fact]
    public void WheelEdit_DraftsAndWritesTheHex() {
        var (picker, theme, _) = Create();
        Swatch(picker, AccentPreset.CustomName).SelectCommand.Execute(null);

        picker.Hue = 0;
        picker.Saturation = 1;
        picker.Brightness = 1;

        Assert.Equal("#ff0000", picker.HexText);
        Assert.Equal(Color.FromRgb(255, 0, 0), picker.Draft.Identity);
        Assert.True(picker.IsDirty);
        Assert.True(picker.IsEditorOpen);
        Assert.True(Swatch(picker, AccentPreset.CustomName).IsSelected);
        Assert.Same(AccentPreset.Default, theme.CurrentAccent);
    }

    [Fact]
    public void HexEdit_Valid_MovesTheWheelAndKeepsTheTypedText() {
        var (picker, _, _) = Create();

        picker.HexText = "#00FF00";

        Assert.Equal(120, picker.Hue, 3);
        Assert.Equal(1, picker.Saturation, 3);
        Assert.Equal(1, picker.Brightness, 3);
        Assert.Equal(Color.FromRgb(0, 255, 0), picker.Draft.Identity);
        Assert.Equal("#00FF00", picker.HexText);
    }

    /// <summary>A half-typed hex is on its way somewhere; it must not move the draft.</summary>
    [Fact]
    public void HexEdit_Unreadable_LeavesTheDraft() {
        var (picker, _, _) = Create();

        picker.HexText = "#12";

        Assert.Equal(AccentPreset.Default, picker.Draft);
        Assert.False(picker.IsDirty);
    }

    [Fact]
    public void ReconcileHex_AfterAnUnreadableEdit_PutsTheDraftBack() {
        var (picker, _, _) = Create();
        picker.HexText = "#00FF00";
        picker.HexText = "#00F0";

        picker.ReconcileHex();

        Assert.Equal("#00ff00", picker.HexText);
        Assert.Equal(Color.FromRgb(0, 255, 0), picker.Draft.Identity);
    }

    [Fact]
    public void ReconcileHex_AShortForm_TidiesToSixDigits() {
        var (picker, _, _) = Create();
        picker.HexText = "#00F";

        picker.ReconcileHex();

        Assert.Equal("#0000ff", picker.HexText);
    }

    /// <summary>Gray has no hue, so typing one must not spin the wheel back to red.</summary>
    [Fact]
    public void HexEdit_Gray_KeepsTheWheelsHue() {
        var (picker, _, _) = Create();
        var hue = picker.Hue;

        picker.HexText = "#808080";

        Assert.Equal(hue, picker.Hue, 6);
        Assert.Equal(0, picker.Saturation, 6);
    }

    [Fact]
    public void PresetClick_DraftsWithoutApplying() {
        var (picker, theme, applied) = Create();

        Swatch(picker, "Green").SelectCommand.Execute(null);

        Assert.Equal("Green", picker.Draft.Name);
        Assert.True(Swatch(picker, "Green").IsSelected);
        Assert.True(picker.IsEditorOpen);
        Assert.Same(AccentPreset.Default, theme.CurrentAccent);
        Assert.Equal(0, applied.Count);
    }

    [Fact]
    public void CustomClick_TogglesTheEditorWithoutDrafting() {
        var (picker, _, _) = Create();
        var custom = Swatch(picker, AccentPreset.CustomName);

        custom.SelectCommand.Execute(null);
        Assert.True(picker.IsEditorOpen);
        Assert.False(picker.IsDirty);

        custom.SelectCommand.Execute(null);
        Assert.False(picker.IsEditorOpen);
    }

    [Fact]
    public void Apply_AppliesTheDraftAndReportsOnce() {
        var (picker, theme, applied) = Create();
        picker.HexText = "#1a3a8a";

        picker.ApplyCommand.Execute(null);

        Assert.Equal(Color.Parse("#1a3a8a"), theme.CurrentAccent.Identity);
        Assert.False(picker.IsDirty);
        Assert.False(picker.IsEditorOpen);
        Assert.Equal(Color.Parse("#1a3a8a"), ((ISolidColorBrush)picker.AppliedBrush).Color);
        Assert.Equal(1, applied.Count);
    }

    [Fact]
    public void Apply_NothingDrafted_DoesNothing() {
        var (picker, _, applied) = Create();

        picker.ApplyCommand.Execute(null);

        Assert.Equal(0, applied.Count);
    }

    /// <summary>The split AB#83 made holds from this side too.</summary>
    [Fact]
    public void Apply_LeavesTheGraphColorsAlone() {
        var (picker, theme, _) = Create();
        var green = GraphColors.All.First(c => c.Name == "Green");
        theme.ApplyGraphColors(green);
        picker.HexText = "#1a3a8a";

        picker.ApplyCommand.Execute(null);

        Assert.Same(green, theme.CurrentGraphColors);
        Assert.Equal(green.Series, theme.CurrentSeries);
    }

    [Fact]
    public void Cancel_ReturnsToTheAppliedAccentAndCloses() {
        var (picker, theme, applied) = Create();
        picker.HexText = "#1a3a8a";

        picker.CancelCommand.Execute(null);

        Assert.Equal(AccentPreset.Default, picker.Draft);
        Assert.Equal("#4cc2ff", picker.HexText);
        Assert.False(picker.IsEditorOpen);
        Assert.Same(AccentPreset.Default, theme.CurrentAccent);
        Assert.Equal(0, applied.Count);
    }

    [Theory]
    [InlineData("#fff3b0", "Hard to see on the light theme")]
    [InlineData("#141a30", "Hard to see on the dark theme")]
    [InlineData("#4cc2ff", "")]
    public void Warning_NamesTheThemeAFillFadesOn(string hex, string expected) {
        var (picker, _, _) = Create();

        picker.HexText = hex;

        Assert.Equal(expected, picker.Warning);
        Assert.Equal(expected.Length > 0, picker.HasWarning);
    }

    [Fact]
    public void Variants_FollowHighContrastAndAnnounceARefresh() {
        var (picker, theme, _) = Create();
        var raised = new System.Collections.Generic.List<string?>();
        picker.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        theme.ApplyContrast(true);
        try {
            picker.RefreshVariants();

            Assert.Equal(AppVariants.HighContrastDark, picker.DarkVariant);
            Assert.Equal(AppVariants.HighContrastLight, picker.LightVariant);
            Assert.Contains(nameof(AccentPickerViewModel.DarkVariant), raised);
            Assert.Contains(nameof(AccentPickerViewModel.LightVariant), raised);
        } finally {
            theme.ApplyContrast(false);
        }
    }

    [Fact]
    public void Ctor_ACustomAccentApplied_SelectsCustomAndIsClean() {
        var theme = new ThemeService();
        theme.ApplyAccent(AccentPreset.FromHex("#1a3a8a"));

        var picker = new AccentPickerViewModel(theme, () => { });

        Assert.Equal("#1a3a8a", picker.HexText);
        Assert.False(picker.IsDirty);
        Assert.True(Swatch(picker, AccentPreset.CustomName).IsSelected);
        Assert.Single(picker.Swatches, s => s.IsSelected);
    }

    /// <summary>An edit that lands back on the applied color must not pull the editor out from under the
    /// user — it used to, mid-keystroke.</summary>
    [Fact]
    public void EditBackToTheApplied_KeepsTheEditorOpen() {
        var (picker, _, _) = Create();
        Swatch(picker, "Green").SelectCommand.Execute(null);

        picker.HexText = "#4cc2ff";

        Assert.False(picker.IsDirty);
        Assert.True(picker.IsEditorOpen);
    }

    /// <summary>Custom will not silently discard a pending pick.</summary>
    [Fact]
    public void CustomClick_WithADraftPending_KeepsTheEditorOpen() {
        var (picker, _, _) = Create();
        picker.HexText = "#1a3a8a";
        Swatch(picker, AccentPreset.CustomName).SelectCommand.Execute(null);

        Swatch(picker, AccentPreset.CustomName).SelectCommand.Execute(null);

        Assert.True(picker.IsEditorOpen);
        Assert.True(picker.IsDirty);
    }

    [Fact]
    public void CustomThenCancel_ClosesTheEditor() {
        var (picker, _, _) = Create();
        Swatch(picker, AccentPreset.CustomName).SelectCommand.Execute(null);

        picker.CancelCommand.Execute(null);

        Assert.False(picker.IsEditorOpen);
        Assert.True(Swatch(picker, "Blue").IsSelected);
    }

    /// <summary>A typed color equal to a preset is that preset, so its swatch lights up.</summary>
    [Fact]
    public void Apply_AHexEqualToAPreset_SelectsThePreset() {
        var (picker, theme, _) = Create();
        var green = AccentPreset.All.First(a => a.Name == "Green");

        picker.HexText = green.Hex;
        picker.ApplyCommand.Execute(null);

        Assert.Same(green, theme.CurrentAccent);
        Assert.True(Swatch(picker, "Green").IsSelected);
        Assert.Single(picker.Swatches, s => s.IsSelected);
    }
}
