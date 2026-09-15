using DashDetective.Tests.Services.Theming;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Xunit;

namespace DashDetective.Tests.Shared.Styles;

/// <summary>
/// Scroller insets go on the content as a Margin (<c>ScrollGutter</c>), because Avalonia leaves
/// ScrollViewer Padding out of the scroll extent and the end of the content becomes unreachable.
/// </summary>
public class ScrollViewerInsetTests {
    [Fact]
    public void NoScrollViewerCarriesPadding() {
        var source = PaletteFile.SourceRoot();
        var offenders = new List<string>();

        foreach (var file in Directory.EnumerateFiles(source, "*.axaml", SearchOption.AllDirectories)) {
            var relative = Path.GetRelativePath(source, file).Replace('\\', '/');
            var root = XDocument.Load(file, LoadOptions.SetLineInfo).Root!;

            foreach (var element in root.DescendantsAndSelf()) {
                var name = element.Name.LocalName;

                if (name == "ScrollViewer" && element.Attribute("Padding") is not null)
                    offenders.Add($"{relative}:{Line(element)}: Padding attribute");

                if (name == "ScrollViewer.Padding")
                    offenders.Add($"{relative}:{Line(element)}: Padding property element");

                var styled = (name == "Style" && TargetsScrollViewer((string?)element.Attribute("Selector")))
                             || (name == "ControlTheme" && (string?)element.Attribute("TargetType") is "ScrollViewer" or "{x:Type ScrollViewer}");
                if (styled && element.Elements().Any(IsPaddingSetter))
                    offenders.Add($"{relative}:{Line(element)}: Padding setter");
            }
        }

        Assert.True(offenders.Count == 0,
            "Use ScrollGutter as the content's Margin:" + Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    /// <summary>The gutter assumes content sits beside the bar, which is only true with auto-hide off. TreeView
    /// template-binds the flag, so the ScrollViewer rule alone does not reach it.</summary>
    [Theory]
    [InlineData("ScrollViewer")]
    [InlineData("TreeView")]
    public void SharedStylesPinAutoHideOff(string selector) {
        var styles = XDocument.Load(Path.Combine(PaletteFile.SourceRoot(), "src/Shared/Styles/SharedStyles.axaml"));

        var pinned = styles.Descendants()
            .Where(e => e.Name.LocalName == "Style" && (string?)e.Attribute("Selector") == selector)
            .SelectMany(e => e.Elements())
            .Any(s => ((string?)s.Attribute("Property"))?.EndsWith("AllowAutoHide", StringComparison.Ordinal) == true
                      && (string?)s.Attribute("Value") == "False");

        Assert.True(pinned, $"SharedStyles.axaml must pin AllowAutoHide off for {selector}.");
    }

    [Theory]
    [InlineData("ScrollViewer", true)]
    [InlineData("ScrollViewer.page", true)]
    [InlineData("ScrollViewer#PageScroll", true)]
    [InlineData("ScrollViewer:pointerover", true)]
    [InlineData(":is(ScrollViewer)", true)]
    [InlineData("Border > ScrollViewer", true)]
    [InlineData("Border>ScrollViewer", true)]
    [InlineData("TreeView, ScrollViewer", true)]
    [InlineData("ScrollViewer /template/ ScrollContentPresenter", false)]
    [InlineData("ScrollViewer Border", false)]
    [InlineData("ScrollViewerHost", false)]
    [InlineData(null, false)]
    public void TargetsScrollViewer_ReadsTheStyledControl(string? selector, bool expected) =>
        Assert.Equal(expected, TargetsScrollViewer(selector));

    private static readonly Regex ScrollViewerCompound =
        new(@"^(:is\()?ScrollViewer(\)|$|[.:#\[])", RegexOptions.Compiled);

    // The last compound of the selector is the styled control; anything before it is an ancestor.
    private static bool TargetsScrollViewer(string? selector) =>
        selector is not null && selector.Split(',').Any(part => {
            var last = part.Split([' ', '>'], StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "";
            return ScrollViewerCompound.IsMatch(last);
        });

    private static bool IsPaddingSetter(XElement e) =>
        e.Name.LocalName == "Setter" && (string?)e.Attribute("Property") is "Padding" or "ScrollViewer.Padding";

    private static int Line(XElement e) => ((IXmlLineInfo)e).LineNumber;
}
