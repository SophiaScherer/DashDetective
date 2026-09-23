using DashDetective.Tests.Services.Theming;
using System;
using System.IO;
using Xunit;

namespace DashDetective.Tests.Shell;

/// <summary>
/// Pins how MainWindow wires the custom title bar. Each of these builds and runs either way; the symptom
/// of losing one is a window that quietly keeps the system title bar, draws Fluent's caption buttons, or
/// scales its title bar away from the buttons beside it.
/// </summary>
public class MainWindowTests {
    private static readonly string Markup =
        File.ReadAllText(Path.Combine(PaletteFile.SourceRoot(), "src/Shell/MainWindow.axaml"));

    [Fact]
    public void Window_OptsIntoTheCustomTitleBar() =>
        Assert.Contains(@"controls:WindowChrome.Custom=""True""", Markup);

    /// <summary>The theme is set before the opt-in, so the decorations are never first built from
    /// Fluent's theme.</summary>
    [Fact]
    public void Window_SetsTheDecorationsThemeBeforeOptingIn() {
        var theme = Markup.IndexOf(@"WindowDecorationsTheme=""{StaticResource AppWindowDecorations}""",
                                   StringComparison.Ordinal);
        var optIn = Markup.IndexOf("controls:WindowChrome.Custom=", StringComparison.Ordinal);

        Assert.True(theme >= 0, "MainWindow no longer sets the AppWindowDecorations theme.");
        Assert.True(theme < optIn, "The decorations theme must be set before WindowChrome.Custom.");
    }

    /// <summary>The bar comes before the scale host rather than inside it: the caption buttons are drawn
    /// at the OS's scale, and a bar inside the host would stop lining up with them.</summary>
    [Fact]
    public void TitleBar_SitsOutsideAndAboveTheScaleHost() {
        var bar = Markup.IndexOf("<controls:TitleBar", StringComparison.Ordinal);
        var host = Markup.IndexOf("<controls:ScaleHost", StringComparison.Ordinal);
        var hostEnd = Markup.IndexOf("</controls:ScaleHost>", StringComparison.Ordinal);

        Assert.True(bar >= 0, "MainWindow no longer places a TitleBar.");
        Assert.True(bar < host, "The TitleBar must come before the ScaleHost, not inside it.");
        Assert.Equal(-1, Markup.IndexOf("<controls:TitleBar", host, hostEnd - host, StringComparison.Ordinal));
    }
}
