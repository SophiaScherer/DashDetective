using Avalonia.Input;
using DashDetective.Tabs.Settings;
using DashDetective.Tests.Services.Theming;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace DashDetective.Tests.Tabs.Settings;

/// <summary>Covers the way out of a shortcut capture: <see cref="CaptureKeys.Classify"/> waits on a held
/// modifier, cancels on Escape and captures anything else, and the capture box carries a Cancel button for
/// a mouse user, since a click on empty page takes no focus and so never stood the capture down.</summary>
public class CaptureKeysTests {
    [Theory]
    [InlineData(Key.LeftCtrl)]
    [InlineData(Key.RightShift)]
    [InlineData(Key.LeftAlt)]
    [InlineData(Key.LWin)]
    public void Classify_HeldModifier_Waits(Key key) =>
        Assert.Equal(CaptureKeyAction.Wait, CaptureKeys.Classify(key));

    [Fact]
    public void Classify_Escape_Cancels() =>
        Assert.Equal(CaptureKeyAction.Cancel, CaptureKeys.Classify(Key.Escape));

    [Theory]
    [InlineData(Key.B)]
    [InlineData(Key.F5)]
    [InlineData(Key.Enter)]
    [InlineData(Key.Space)]
    public void Classify_AnyOtherKey_Captures(Key key) =>
        Assert.Equal(CaptureKeyAction.Capture, CaptureKeys.Classify(key));

    /// <summary>The control itself has no view model to test, so its markup is pinned instead: a Cancel
    /// button that starts hidden and cannot take focus, which would stand the capture down mid-click.</summary>
    [Fact]
    public void Markup_OffersANonFocusableCancelButton() {
        var file = Path.Combine(PaletteFile.SourceRoot(), "src/Tabs/Settings/ShortcutCaptureBox.axaml");
        var cancel = XDocument.Load(file).Root!.Descendants()
            .Single(e => e.Name.LocalName == "Button" && (string?)e.Attribute("Name") == "Cancel");

        Assert.Equal("False", (string?)cancel.Attribute("IsVisible"));
        Assert.Equal("False", (string?)cancel.Attribute("Focusable"));
        Assert.Equal("Cancel (Esc)", (string?)cancel.Attribute("ToolTip.Tip"));
    }
}
