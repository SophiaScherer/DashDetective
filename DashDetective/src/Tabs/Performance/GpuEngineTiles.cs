using System;
using System.Collections.Generic;

namespace DashDetective.Tabs.Performance;

/// <summary>
/// Which engines get a stat tile beside "3D": Task Manager's other three default graphs, Copy, Video Encode
/// and Video Decode. Pure, so the mapping and its omissions are tested without a page.
///
/// <b>Only unambiguous engine names map.</b> AMD's "Video Codec" engine serves encoding and, on some parts,
/// decoding too, so calling it either would be a guess; it stays in the Per engine grid under its own name.
/// An adapter that publishes none of an engine type gets no tile for it, never a 0 %.
/// </summary>
internal static class GpuEngineTiles {
    /// <summary>The tile labels, in Task Manager's order.</summary>
    internal static readonly IReadOnlyList<string> Labels = ["Copy", "Video Encode", "Video Decode"];

    /// <summary>The tile label for a folded engine key ("copy", "videoencode", "video decode"), or null for
    /// an engine with no tile.</summary>
    internal static string? LabelFor(string engineKey) => engineKey switch {
        "copy" => "Copy",
        "videoencode" or "video encode" => "Video Encode",
        "videodecode" or "video decode" => "Video Decode",
        _ => null,
    };

    /// <summary>Each tiled label's reading from a folded engine map: the busiest engine under that label,
    /// as the per-type rule reads it. Labels whose engines the map lacks are absent, not 0.</summary>
    internal static Dictionary<string, double> Readings(IReadOnlyDictionary<string, double> foldedEngines) {
        var readings = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var (key, value) in foldedEngines) {
            if (LabelFor(key) is { } label)
                readings[label] = Math.Max(readings.GetValueOrDefault(label), value);
        }
        return readings;
    }
}
