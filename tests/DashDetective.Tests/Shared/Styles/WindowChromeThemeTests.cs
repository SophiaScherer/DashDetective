using DashDetective.Tests.Services.Theming;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace DashDetective.Tests.Shared.Styles;

/// <summary>
/// Pins the markup that keeps the custom title bar native in behavior. Each of these is a single
/// attribute whose loss builds, formats and renders exactly as before — the only symptom is that
/// Windows stops treating a region as caption, so snap, the snap flyout or a caption button goes quiet.
/// </summary>
public class WindowChromeThemeTests {
    /// <summary>Avalonia wires a caption button by its PART_ name and answers the OS hit test by its role
    /// (HTMINBUTTON, HTMAXBUTTON, HTCLOSE); the maximize role is what brings up the snap flyout.</summary>
    [Theory]
    [InlineData("PART_MinimizeButton", "MinimizeButton")]
    [InlineData("PART_MaximizeButton", "MaximizeButton")]
    [InlineData("PART_CloseButton", "CloseButton")]
    public void CaptionButton_CarriesItsPartNameAndRole(string part, string role) {
        var button = new Regex($@"<Button x:Name=""{part}""[^>]*>", RegexOptions.Singleline)
            .Match(Read("src/Shared/Styles/WindowChrome.axaml"));

        Assert.True(button.Success, $"WindowChrome.axaml no longer declares {part}.");
        Assert.Contains($@"WindowDecorationProperties.ElementRole=""{role}""", button.Value);
    }

    /// <summary>The bar's role is what makes Avalonia answer HTCAPTION, which is what hands drag, snap
    /// and double-click to Windows rather than to a pointer handler.</summary>
    [Fact]
    public void TitleBar_IsMarkedAsCaption() =>
        Assert.Contains(@"WindowDecorationProperties.ElementRole=""TitleBar""",
                        Read("src/Shared/Controls/TitleBar.axaml"));

    /// <summary>The theme reads its sizes from TitleBarRules, so the bar beneath cannot drift from the
    /// buttons drawn over it.</summary>
    [Theory]
    [InlineData("DefaultTitleBarHeight", "TitleBarRules.DefaultHeight")]
    [InlineData("Width", "TitleBarRules.CaptionButtonWidth")]
    public void Theme_TakesItsSizesFromTheRules(string property, string source) =>
        Assert.Contains($@"<Setter Property=""{property}"" Value=""{{x:Static controls:{source}}}""/>",
                        Read("src/Shared/Styles/WindowChrome.axaml"));

    private static string Read(string relative) =>
        File.ReadAllText(Path.Combine(PaletteFile.SourceRoot(), relative));
}
