using Avalonia.Media;
using DashDetective.Services.Theming;
using DashDetective.Tabs.Settings;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace DashDetective.Tests.Tabs.Settings;

/// <summary>
/// Covers <see cref="AccentPickerViewModel"/>: the wheel and the hex box stay in step, a pick or a revert
/// only drafts, nothing reaches the theme until Apply, and the modal closes only when told to.
/// </summary>
public class AccentPickerViewModelTests {
    private static (AccentPickerViewModel Picker, ThemeService Theme, Counter Applied) Create() {
        var theme = new ThemeService();
        var applied = new Counter();
        return (new AccentPickerViewModel(theme, () => applied.Count++), theme, applied);
    }

    private static (AccentPickerViewModel Picker, ThemeService Theme, Counter Applied) Opened() {
        var created = Create();
        created.Picker.OpenCommand.Execute(null);
        return created;
    }

    private sealed class Counter {
        public int Count;
    }

    [Fact]
    public void Ctor_DraftsTheAppliedAccentClosed() {
        var (picker, _, _) = Create();

        Assert.Equal(AccentPreset.Default, picker.Draft);
        Assert.Equal("#4cc2ff", picker.HexText);
        Assert.False(picker.IsDirty);
        Assert.False(picker.IsOpen);
        Assert.False(picker.CanRevert);
        Assert.Equal(AccentPreset.Default.Identity, ((ISolidColorBrush)picker.AppliedBrush).Color);
    }

    [Fact]
    public void Ctor_ACustomAccentApplied_DraftsItClean() {
        var theme = new ThemeService();
        theme.ApplyAccent(AccentPreset.FromHex("#1a3a8a"));

        var picker = new AccentPickerViewModel(theme, () => { });

        Assert.Equal("#1a3a8a", picker.HexText);
        Assert.False(picker.IsDirty);
        Assert.True(picker.CanRevert);
    }

    [Fact]
    public void Open_OpensOnTheAppliedAccent() {
        var (picker, _, _) = Create();
        picker.HexText = "#1a3a8a";

        picker.OpenCommand.Execute(null);

        Assert.True(picker.IsOpen);
        Assert.Equal(AccentPreset.Default, picker.Draft);
    }

    /// <summary>A second Open while the modal is up must not wipe the pick in progress.</summary>
    [Fact]
    public void Open_AlreadyOpen_KeepsThePendingPick() {
        var (picker, _, _) = Opened();
        picker.HexText = "#1a3a8a";

        picker.OpenCommand.Execute(null);

        Assert.Equal("#1a3a8a", picker.HexText);
        Assert.True(picker.IsDirty);
    }

    [Fact]
    public void WheelEdit_DraftsAndWritesTheHex() {
        var (picker, theme, _) = Opened();

        picker.Hue = 0;
        picker.Saturation = 1;
        picker.Brightness = 1;

        Assert.Equal("#ff0000", picker.HexText);
        Assert.Equal(Color.FromRgb(255, 0, 0), picker.Draft.Identity);
        Assert.Equal(AccentPreset.CustomName, picker.Draft.Name);
        Assert.True(picker.IsDirty);
        Assert.Same(AccentPreset.Default, theme.CurrentAccent);
    }

    [Fact]
    public void HexEdit_Valid_MovesTheWheelAndKeepsTheTypedText() {
        var (picker, _, _) = Opened();

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
        var (picker, _, _) = Opened();

        picker.HexText = "#12";

        Assert.Equal(AccentPreset.Default, picker.Draft);
        Assert.False(picker.IsDirty);
    }

    [Fact]
    public void ReconcileHex_AfterAnUnreadableEdit_PutsTheDraftBack() {
        var (picker, _, _) = Opened();
        picker.HexText = "#00FF00";
        picker.HexText = "#00F0";

        picker.ReconcileHex();

        Assert.Equal("#00ff00", picker.HexText);
        Assert.Equal(Color.FromRgb(0, 255, 0), picker.Draft.Identity);
    }

    [Fact]
    public void ReconcileHex_AShortForm_TidiesToSixDigits() {
        var (picker, _, _) = Opened();
        picker.HexText = "#00F";

        picker.ReconcileHex();

        Assert.Equal("#0000ff", picker.HexText);
    }

    /// <summary>Gray has no hue, so typing one must not spin the wheel back to red.</summary>
    [Fact]
    public void HexEdit_Gray_KeepsTheWheelsHue() {
        var (picker, _, _) = Opened();
        var hue = picker.Hue;

        picker.HexText = "#808080";

        Assert.Equal(hue, picker.Hue, 6);
        Assert.Equal(0, picker.Saturation, 6);
    }

    /// <summary>Revert drafts Blue for the preview; it does not apply it.</summary>
    [Fact]
    public void RevertToDefault_DraftsBlueWithoutApplying() {
        var theme = new ThemeService();
        theme.ApplyAccent(AccentPreset.FromHex("#1a3a8a"));
        var picker = new AccentPickerViewModel(theme, () => { });
        picker.OpenCommand.Execute(null);

        picker.RevertToDefaultCommand.Execute(null);

        Assert.Same(AccentPreset.Default, picker.Draft);
        Assert.Equal("#4cc2ff", picker.HexText);
        Assert.False(picker.CanRevert);
        Assert.True(picker.IsDirty);
        Assert.True(picker.IsOpen);
        Assert.Equal(Color.Parse("#1a3a8a"), theme.CurrentAccent.Identity);
    }

    [Fact]
    public void Apply_AppliesTheDraftClosesAndReportsOnce() {
        var (picker, theme, applied) = Opened();
        var closed = 0;
        picker.Closed += () => closed++;
        picker.HexText = "#1a3a8a";

        picker.ApplyCommand.Execute(null);

        Assert.Equal(Color.Parse("#1a3a8a"), theme.CurrentAccent.Identity);
        Assert.False(picker.IsDirty);
        Assert.False(picker.IsOpen);
        Assert.Equal(Color.Parse("#1a3a8a"), ((ISolidColorBrush)picker.AppliedBrush).Color);
        Assert.Equal(1, applied.Count);
        Assert.Equal(1, closed);
    }

    [Fact]
    public void Apply_NothingDrafted_DoesNothing() {
        var (picker, _, applied) = Opened();

        picker.ApplyCommand.Execute(null);

        Assert.Equal(0, applied.Count);
        Assert.True(picker.IsOpen);
    }

    /// <summary>The split AB#83 made holds from this side too.</summary>
    [Fact]
    public void Apply_LeavesTheGraphColorsAlone() {
        var (picker, theme, _) = Opened();
        var green = GraphColors.All.First(c => c.Name == "Green");
        theme.ApplyGraphColors(green);
        picker.HexText = "#1a3a8a";

        picker.ApplyCommand.Execute(null);

        Assert.Same(green, theme.CurrentGraphColors);
        Assert.Equal(green.Series, theme.CurrentSeries);
    }

    [Fact]
    public void Cancel_ReturnsToTheAppliedAccentAndCloses() {
        var (picker, theme, applied) = Opened();
        var closed = 0;
        picker.Closed += () => closed++;
        picker.HexText = "#1a3a8a";

        picker.CancelCommand.Execute(null);

        Assert.Equal(AccentPreset.Default, picker.Draft);
        Assert.Equal("#4cc2ff", picker.HexText);
        Assert.False(picker.IsOpen);
        Assert.Same(AccentPreset.Default, theme.CurrentAccent);
        Assert.Equal(0, applied.Count);
        Assert.Equal(1, closed);
    }

    /// <summary>An edit landing back on the applied color must not pull the modal out from under the user.</summary>
    [Fact]
    public void EditBackToTheApplied_StaysOpen() {
        var (picker, _, _) = Opened();
        picker.HexText = "#1a3a8a";

        picker.HexText = "#4cc2ff";

        Assert.False(picker.IsDirty);
        Assert.True(picker.IsOpen);
    }

    [Fact]
    public void DismissFromBackdrop_NothingPending_Closes() {
        var (picker, _, _) = Opened();

        picker.DismissFromBackdrop();

        Assert.False(picker.IsOpen);
    }

    /// <summary>A stray click on the scrim must not lose a pick.</summary>
    [Fact]
    public void DismissFromBackdrop_WithADraftPending_StaysOpen() {
        var (picker, _, _) = Opened();
        picker.HexText = "#1a3a8a";

        picker.DismissFromBackdrop();

        Assert.True(picker.IsOpen);
        Assert.True(picker.IsDirty);
    }

    [Fact]
    public void Closed_IsNotRaisedWhenAlreadyClosed() {
        var (picker, _, _) = Create();
        var closed = 0;
        picker.Closed += () => closed++;

        picker.CancelCommand.Execute(null);

        Assert.Equal(0, closed);
    }

    [Theory]
    [InlineData("#fff3b0", "Hard to see on the light theme")]
    [InlineData("#141a30", "Hard to see on the dark theme")]
    [InlineData("#4cc2ff", "")]
    public void Warning_NamesTheThemeAFillFadesOn(string hex, string expected) {
        var (picker, _, _) = Opened();

        picker.HexText = hex;

        Assert.Equal(expected, picker.Warning);
        Assert.Equal(expected.Length > 0, picker.HasWarning);
    }

    [Fact]
    public void Variants_FollowHighContrastAndAnnounceARefresh() {
        var (picker, theme, _) = Create();
        var raised = new List<string?>();
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
}
