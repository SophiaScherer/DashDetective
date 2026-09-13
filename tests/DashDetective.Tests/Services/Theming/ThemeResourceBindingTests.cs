using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace DashDetective.Tests.Services.Theming;

/// <summary>
/// A theme-dictionary key bound with {StaticResource} resolves once and never follows a theme switch —
/// how the console insets stayed dark in light mode. Such keys must be {DynamicResource}.
/// </summary>
public class ThemeResourceBindingTests {
    private static readonly Regex StaticReference = new(@"\{StaticResource (\w+)\}", RegexOptions.Compiled);

    [Fact]
    public void NoViewBindsAThemeKeyStatically() {
        var themeKeys = PaletteFile.Tables.Values.SelectMany(table => table.Keys).ToHashSet(StringComparer.Ordinal);
        var source = PaletteFile.SourceRoot();
        var offenders = new List<string>();

        foreach (var file in Directory.EnumerateFiles(source, "*.axaml", SearchOption.AllDirectories)) {
            var relative = Path.GetRelativePath(source, file).Replace('\\', '/');

            foreach (var (line, number) in File.ReadLines(file).Select((l, i) => (l, i + 1)))
                foreach (Match match in StaticReference.Matches(line))
                    if (themeKeys.Contains(match.Groups[1].Value))
                        offenders.Add($"{relative}:{number}: {match.Groups[1].Value}");
        }

        Assert.True(offenders.Count == 0,
            "Theme keys must be {DynamicResource}:" + Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }
}
