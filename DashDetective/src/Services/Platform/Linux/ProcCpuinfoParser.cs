using System;
using System.Collections.Generic;

namespace DashDetective.Services.Platform.Linux;

/// <summary>
/// Parses <c>/proc/cpuinfo</c> into one key/value block per logical processor — the blank-line-separated
/// records the kernel writes. Shared format knowledge rather than provider logic, so it lives beside
/// <see cref="ProcStatParser"/> and serves the CPU facts, the Processor card and the frequency sampler.
///
/// The real file separates key from value with <b>tabs</b>, and the count and padding of those tabs varies
/// by key length (<c>cpu MHz\t\t:</c> against <c>model name\t:</c>). Parsing therefore trims around the
/// colon rather than assuming a layout — a parser that splits on a fixed column passes a hand-written
/// space-separated fixture and fails on a real machine.
/// </summary>
internal static class ProcCpuinfoParser {
    /// <summary>
    /// Parses the file into its per-processor blocks, in file order. Keys are matched
    /// case-insensitively because they are prose rather than identifiers (<c>cpu MHz</c>,
    /// <c>model name</c>). Malformed lines are skipped and a blank block is never emitted, so a trailing
    /// newline does not add a phantom processor.
    /// </summary>
    internal static IReadOnlyList<IReadOnlyDictionary<string, string>> Parse(IReadOnlyList<string> lines) {
        var blocks = new List<IReadOnlyDictionary<string, string>>();
        var current = NewBlock();

        foreach (var line in lines) {
            // A blank line closes the current processor's record.
            if (line.AsSpan().Trim().IsEmpty) {
                if (current.Count > 0) {
                    blocks.Add(current);
                    current = NewBlock();
                }

                continue;
            }

            var colon = line.IndexOf(':');
            if (colon <= 0)
                continue;

            var key = line.AsSpan(0, colon).Trim();
            if (key.IsEmpty)
                continue;

            current[key.ToString()] = line.AsSpan(colon + 1).Trim().ToString();
        }

        // The last block usually has no blank line after it on a torn or truncated read.
        if (current.Count > 0)
            blocks.Add(current);

        return blocks;
    }

    /// <summary>The field's value, or "" when it is absent — the "not reported" contract every caller
    /// treats as a missing reading. Architectures differ wildly in which keys they write, so an absent
    /// key is routine rather than an error.</summary>
    internal static string Value(IReadOnlyDictionary<string, string> block, string key) =>
        block.TryGetValue(key, out var value) ? value : "";

    private static Dictionary<string, string> NewBlock() => new(StringComparer.OrdinalIgnoreCase);
}
