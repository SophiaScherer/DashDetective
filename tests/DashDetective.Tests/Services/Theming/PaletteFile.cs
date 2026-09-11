using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
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

    /// <summary>The brush keys declared outside the theme dictionaries — the accent and chart-series sets,
    /// which <c>ThemeService</c> swaps at runtime. Names only: these are authored as
    /// <c>{StaticResource}</c> references to the colour primitives rather than as hex, so there is no
    /// value here to read.</summary>
    internal static HashSet<string> TopLevel => LazyTopLevel.Value;

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

    private static readonly Lazy<HashSet<string>> LazyTopLevel = new(ReadTopLevel);

    private static HashSet<string> ReadTopLevel() {
        var xaml = File.ReadAllText(Path.Combine(SourceRoot(), "src/Shared/Styles/Palette.axaml"));

        // Everything before the theme dictionaries open is the top-level set.
        var cut = xaml.IndexOf("<ResourceDictionary.ThemeDictionaries>", StringComparison.Ordinal);
        Assert.True(cut > 0, "Palette.axaml no longer opens a ThemeDictionaries block.");

        var keys = Regex.Matches(xaml[..cut], """<SolidColorBrush x:Key="(\w+)""")
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

        Assert.NotEmpty(keys);
        return keys;
    }

    private static Dictionary<string, Dictionary<string, Brush>> Read() {
        var xaml = File.ReadAllText(Path.Combine(SourceRoot(), "src/Shared/Styles/Palette.axaml"));
        var tables = new Dictionary<string, Dictionary<string, Brush>>(StringComparer.Ordinal);
        var primitives = Primitives(xaml);

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
                         """<SolidColorBrush x:Key="(\w+)" Color="(#[0-9A-Fa-f]{6}|\{StaticResource \w+\})"(?: Opacity="([\d.]+)")?\s*/>""")) {
                var opacity = entry.Groups[3].Success
                    ? double.Parse(entry.Groups[3].Value, CultureInfo.InvariantCulture)
                    : 1.0;

                table[entry.Groups[1].Value] = new Brush(Rgb(entry.Groups[2].Value, primitives), opacity);
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

    /// <summary>The authored <c>&lt;Color&gt;</c> hues, which a brush may name instead of spelling a hex.
    /// Every tint in the file hangs off one of these, so a colour has a single definition.</summary>
    private static Dictionary<string, string> Primitives(string xaml) =>
        Regex.Matches(xaml, """<Color x:Key="(\w+)">(#[0-9A-Fa-f]{6})</Color>""")
            .ToDictionary(match => match.Groups[1].Value, match => match.Groups[2].Value, StringComparer.Ordinal);

    /// <summary>A brush's authored colour, whether it spells a hex or names a primitive.</summary>
    private static (int R, int G, int B) Rgb(string authored, Dictionary<string, string> primitives) {
        if (!authored.StartsWith('#')) {
            var key = authored["{StaticResource ".Length..^1];
            Assert.True(primitives.ContainsKey(key), $"Palette.axaml names an undeclared colour: {key}.");
            authored = primitives[key];
        }

        return (Convert.ToInt32(authored[1..3], 16),
                Convert.ToInt32(authored[3..5], 16),
                Convert.ToInt32(authored[5..], 16));
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
