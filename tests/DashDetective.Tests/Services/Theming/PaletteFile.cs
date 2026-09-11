using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Xunit;

namespace DashDetective.Tests.Services.Theming;

/// <summary>
/// Reads the authored Palette.axaml, rather than a copy of its numbers, so a palette test cannot pass
/// against values the app does not ship. Deliberately a plain parse: loading it through Avalonia would
/// need a render backend, which these tests do not have.
/// </summary>
internal static class PaletteFile {
    /// <summary>One brush as authored: its colour and the opacity it is drawn at.</summary>
    internal readonly record struct Brush((int R, int G, int B) Color, double Opacity);

    /// <summary>One brush table per theme dictionary, keyed by variant name.</summary>
    internal static Dictionary<string, Dictionary<string, Brush>> Tables => Lazy.Value;

    /// <summary>A variant's value for a key, falling back to the variant it inherits from — which is how
    /// the high-contrast dictionaries get away with authoring only their differences.</summary>
    internal static Brush Resolve(string variant, string key) {
        if (Tables[variant].TryGetValue(key, out var brush))
            return brush;

        var parent = variant switch {
            "HighContrastDark" => "Dark",
            "HighContrastLight" => "Light",
            _ => throw new InvalidOperationException($"{variant} has no value for {key} and inherits nothing."),
        };

        return Tables[parent][key];
    }

    private static readonly Lazy<Dictionary<string, Dictionary<string, Brush>>> Lazy = new(Read);

    private static Dictionary<string, Dictionary<string, Brush>> Read() {
        var xaml = File.ReadAllText(Path.Combine(SourceRoot(), "src/Shared/Styles/Palette.axaml"));
        var tables = new Dictionary<string, Dictionary<string, Brush>>(StringComparer.Ordinal);

        // Each theme dictionary opens with its key — "Dark"/"Light" plainly, the high-contrast pair
        // through an x:Static reference to AppVariants.
        var blocks = Regex.Matches(
            xaml,
            """<ResourceDictionary x:Key="(?:\{x:Static theming:AppVariants\.)?(\w+?)\}?">(.*?)</ResourceDictionary>""",
            RegexOptions.Singleline);

        foreach (Match block in blocks) {
            var table = new Dictionary<string, Brush>(StringComparer.Ordinal);

            foreach (Match entry in Regex.Matches(
                         block.Groups[2].Value,
                         """<SolidColorBrush x:Key="(\w+)" Color="#([0-9A-Fa-f]{6})"(?: Opacity="([\d.]+)")?\s*/>""")) {
                var hex = entry.Groups[2].Value;
                var opacity = entry.Groups[3].Success
                    ? double.Parse(entry.Groups[3].Value, CultureInfo.InvariantCulture)
                    : 1.0;

                table[entry.Groups[1].Value] = new Brush(
                    (Convert.ToInt32(hex[..2], 16), Convert.ToInt32(hex[2..4], 16), Convert.ToInt32(hex[4..], 16)),
                    opacity);
            }

            tables[block.Groups[1].Value] = table;
        }

        // A silent parse failure would make every assertion built on this vacuously true.
        foreach (var variant in new[] { "Dark", "Light", "HighContrastDark", "HighContrastLight" }) {
            Assert.True(tables.ContainsKey(variant), $"No theme dictionary parsed for {variant}.");
            Assert.NotEmpty(tables[variant]);
        }

        return tables;
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
