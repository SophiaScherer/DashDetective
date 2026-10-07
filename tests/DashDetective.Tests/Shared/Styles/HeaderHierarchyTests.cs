using Avalonia.Media;
using DashDetective.Services.Accessibility;
using DashDetective.Tests.Services.Theming;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace DashDetective.Tests.Shared.Styles;

/// <summary>
/// The shell's page title outranks a widget's title in size and in weight, and the rank is set once in
/// SharedStyles.axaml. At 15 against 13.5, one weight, the two read as the same level.
/// </summary>
public class HeaderHierarchyTests {
    private const string Shell = "shellTitle";
    private const string Panel = "panelTitle";

    /// <summary>Well clear of a half-point nudge. Both sizes come off the one ladder, so the ratio is the
    /// same at every text scale; the theory states that rather than assuming it.</summary>
    [Theory]
    [InlineData(ScaleRange.MinPercent)]
    [InlineData(ScaleRange.DefaultPercent)]
    [InlineData(ScaleRange.MaxPercent)]
    public void ShellTitle_IsClearlyLargerThanPanelTitle_AtEveryTextScale(int percent) {
        var sizes = TextScale.Sizes(percent);

        Assert.True(sizes[SizeKey(Shell)] >= sizes[SizeKey(Panel)] * 1.25,
            $"{Shell} ({SizeKey(Shell)}) must be at least 1.25x {Panel} ({SizeKey(Panel)}).");
    }

    [Fact]
    public void ShellTitle_IsHeavierThanPanelTitle() =>
        Assert.True(Weight(Shell) > Weight(Panel), $"{Shell} must be a heavier weight than {Panel}.");

    /// <summary>A literal size would not follow text scale, and a static brush would not follow the
    /// theme, so either would let the hierarchy hold in one setting and break in another.</summary>
    [Theory]
    [InlineData(Shell)]
    [InlineData(Panel)]
    public void Header_TakesItsSizeFromTheLadderAndItsColorFromTheTheme(string cls) {
        Assert.True(TextScale.BaseSizes.ContainsKey(SizeKey(cls)), $"{cls} must size from the TextSize ladder.");

        var foreground = Setter(cls, "Foreground");
        if (foreground is null)
            return;

        var key = Regex.Match(foreground, @"^\{DynamicResource (\w+)\}$");
        Assert.True(key.Success, $"{cls} must color from a DynamicResource, not {foreground}.");
        foreach (var variant in PaletteFile.Tables.Keys)
            _ = PaletteFile.Resolve(variant, key.Groups[1].Value);
    }

    /// <summary>A local setter outranks the shared class, so the toolbar re-authoring its own size, weight
    /// or color would silently undo the hierarchy.</summary>
    [Fact]
    public void Toolbar_TitleTakesTheSharedClassWithNoLocalOverride() {
        var titles = WithClass(Path.Combine(PaletteFile.SourceRoot(), "src/Shell/MainWindow.axaml"), Shell).ToList();

        var title = Assert.Single(titles);
        foreach (var property in new[] { "FontSize", "FontWeight", "Foreground" })
            Assert.Null(title.Attribute(property));
    }

    /// <summary>The shell title belongs to the shell. A page heading drawn with it would be the confusion
    /// this hierarchy exists to remove.</summary>
    [Fact]
    public void NoTabBorrowsTheShellTitle() {
        var tabs = Path.Combine(PaletteFile.SourceRoot(), "src/Tabs");

        var offenders = Directory.EnumerateFiles(tabs, "*.axaml", SearchOption.AllDirectories)
            .Where(file => WithClass(file, Shell).Any())
            .Select(Path.GetFileName);

        Assert.Empty(offenders);
    }

    private static string SizeKey(string cls) {
        var value = Setter(cls, "FontSize");
        var key = Regex.Match(value ?? "", @"^\{DynamicResource (TextSize\w+)\}$");
        Assert.True(key.Success, $"{cls} must set FontSize from a TextSize* DynamicResource, not {value}.");
        return key.Groups[1].Value;
    }

    private static FontWeight Weight(string cls) {
        var value = Setter(cls, "FontWeight");
        Assert.NotNull(value);
        return Enum.Parse<FontWeight>(value!);
    }

    private static string? Setter(string cls, string property) {
        var styles = XDocument.Load(Path.Combine(PaletteFile.SourceRoot(), "src/Shared/Styles/SharedStyles.axaml"));

        var style = Assert.Single(styles.Descendants(),
            e => e.Name.LocalName == "Style" && (string?)e.Attribute("Selector") == $"TextBlock.{cls}");

        return style.Elements()
            .Where(s => (string?)s.Attribute("Property") == property)
            .Select(s => (string?)s.Attribute("Value"))
            .SingleOrDefault();
    }

    private static IEnumerable<XElement> WithClass(string file, string cls) =>
        XDocument.Load(file).Descendants()
            .Where(e => ((string?)e.Attribute("Classes"))?.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                        .Contains(cls, StringComparer.Ordinal) == true);
}
