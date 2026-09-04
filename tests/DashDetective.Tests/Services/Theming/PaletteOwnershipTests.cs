using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Xunit;

namespace DashDetective.Tests.Services.Theming;

/// <summary>
/// Guards the rule that Palette.axaml owns every colour in the app. Colours had drifted into 20 files
/// as raw hex — three near-identical ambers, two tint alphas, five shadow alphas — which is why the
/// accent reached only half the UI. Tokenising fixed that once; this keeps it fixed, because a hex
/// literal costs nothing to add and nothing else would catch it.
///
/// The theme files themselves are exempt: Palette.axaml is the source of truth, and the three C#
/// mirrors beside it exist precisely to hold the same values for code that cannot reach
/// {StaticResource}.
/// </summary>
public class PaletteOwnershipTests {
    /// <summary>Palette.axaml, plus the C# mirrors that deliberately restate it, plus the HTML export.</summary>
    private static readonly string[] Allowed = [
        "src/Shared/Styles/Palette.axaml",
        "src/Services/Theming/ChartPalette.cs",
        "src/Services/Theming/SemanticBrushes.cs",
        "src/Services/Theming/AccentPreset.cs",
        // The colour-blind-safe tables. Their hues cannot come from Palette.axaml: they are chosen by
        // SEARCH against a dichromacy simulation, per theme and per deficiency, and the tests that verify
        // them read this file. See ColorVisionTests.
        "src/Services/Theming/ColorVision.cs",
        // The HTML report is a standalone document rendered by a browser, not app UI. It has no access
        // to the palette at all, and it must not: an exported file that only looked right inside
        // DashDetective would be the bug. Its colours are the document's, not the theme's.
        "src/Services/Diagnostics/ReportFormatters.cs",
    ];

    /// <summary>Six- and eight-digit hex, the only forms the app has ever used. Deliberately not
    /// matching three-digit shorthand, which would collide with XAML element references (<c>#Root</c>).</summary>
    private static readonly Regex HexColor =
        new(@"#(?:[0-9a-fA-F]{8}|[0-9a-fA-F]{6})\b", RegexOptions.Compiled);

    [Fact]
    public void NoSourceFileOutsideTheThemeSpellsAHexColor() {
        var source = SourceRoot();

        var offenders = new List<string>();
        foreach (var file in Directory.EnumerateFiles(source, "*.*", SearchOption.AllDirectories)) {
            if (Path.GetExtension(file) is not (".cs" or ".axaml"))
                continue;

            var relative = Path.GetRelativePath(source, file).Replace('\\', '/');
            if (Allowed.Any(a => relative.EndsWith(a, StringComparison.Ordinal)))
                continue;

            foreach (var (line, number) in File.ReadLines(file).Select((l, i) => (l, i + 1)))
                if (HexColor.Match(line) is { Success: true } match)
                    offenders.Add($"{relative}:{number}: {match.Value}");
        }

        Assert.True(offenders.Count == 0,
            "Colours belong in src/Shared/Styles/Palette.axaml, referenced with {StaticResource} or " +
            "{DynamicResource} (or via SemanticBrushes/ChartPalette from code):" +
            Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    /// <summary>Walks up to the repository from this file's own compile-time path. Anchoring to the
    /// binaries instead would break under <c>--artifacts-path</c>, which puts them outside the repo.</summary>
    private static string SourceRoot([CallerFilePath] string thisFile = "") {
        var dir = new DirectoryInfo(Path.GetDirectoryName(thisFile)!);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "DashDetective.sln")))
            dir = dir.Parent;

        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "DashDetective");
    }
}
