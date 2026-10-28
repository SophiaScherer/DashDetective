using System;
using System.Collections.Generic;

namespace DashDetective.Services.SystemMetrics;

/// <summary>
/// Turns raw <c>\GPU Adapter Memory(*)</c> readings — <c>Dedicated Usage</c> or <c>Shared Usage</c>, bytes,
/// one instance per <c>luid_0x…_0x…_phys_N</c> — into one figure per adapter, and joins both onto the
/// engine-counter samples. Task Manager's dedicated and shared figures are these sums, with no other
/// arithmetic.
///
/// <b>The join never adds an adapter.</b> The inventory keys GPUs off the engine counter's LUIDs, so a
/// memory-only LUID is dropped; an adapter with no memory reading keeps a null usage, never 0.
///
/// Pure and outside the Windows-gated sampler, so it is tested on every CI leg.
/// </summary>
internal static class GpuMemoryUsage {
    private const string LuidToken = "luid_0x";
    private const string PhysToken = "_phys_";

    /// <summary>Bytes in use per adapter LUID token, summed across physical indices — for either memory
    /// counter. Names that do not parse, and readings that are negative or not a number, are skipped.</summary>
    internal static IReadOnlyDictionary<string, ulong> ByAdapter(IEnumerable<(string? Name, double Value)> readings) {
        var result = new Dictionary<string, ulong>(StringComparer.Ordinal);
        foreach (var (name, value) in readings) {
            if (!TryParseLuid(name, out var luid) || double.IsNaN(value) || value < 0)
                continue;

            var bytes = value >= ulong.MaxValue ? ulong.MaxValue : (ulong)Math.Round(value);
            result[luid] = result.TryGetValue(luid, out var running) ? SaturatingAdd(running, bytes) : bytes;
        }
        return result;
    }

    /// <summary>Returns <paramref name="samples"/> with each adapter's
    /// <see cref="GpuAdapterSample.DedicatedUsedBytes"/> and <see cref="GpuAdapterSample.SharedUsedBytes"/>
    /// filled from <paramref name="dedicated"/> and <paramref name="shared"/> where each has an entry; a null
    /// map is a counter that could not be read. Adapters absent from the samples stay absent.</summary>
    internal static IReadOnlyDictionary<string, GpuAdapterSample> Join(
        IReadOnlyDictionary<string, GpuAdapterSample> samples,
        IReadOnlyDictionary<string, ulong>? dedicated, IReadOnlyDictionary<string, ulong>? shared) {
        var joined = new Dictionary<string, GpuAdapterSample>(samples.Count, StringComparer.Ordinal);
        foreach (var (luid, sample) in samples) {
            joined[luid] = sample with {
                DedicatedUsedBytes = Lookup(dedicated, luid) ?? sample.DedicatedUsedBytes,
                SharedUsedBytes = Lookup(shared, luid) ?? sample.SharedUsedBytes,
            };
        }
        return joined;
    }

    private static ulong? Lookup(IReadOnlyDictionary<string, ulong>? memory, string luid) =>
        memory is not null && memory.TryGetValue(luid, out var used) ? used : null;

    private static ulong SaturatingAdd(ulong a, ulong b) => a > ulong.MaxValue - b ? ulong.MaxValue : a + b;

    /// <summary>Parses "<c>luid_0x{high}_0x{low}_phys_{n}</c>" down to the adapter token; false for anything
    /// else.</summary>
    private static bool TryParseLuid(string? instanceName, out string luid) {
        luid = "";
        if (string.IsNullOrEmpty(instanceName))
            return false;

        var name = instanceName.AsSpan();
        var luidAt = name.IndexOf(LuidToken, StringComparison.OrdinalIgnoreCase);
        var physAt = name.LastIndexOf(PhysToken, StringComparison.OrdinalIgnoreCase);
        if (luidAt < 0 || physAt < luidAt + LuidToken.Length)
            return false;

        return GpuEngineInstance.TryParseLuid(name[(luidAt + LuidToken.Length)..physAt], out luid);
    }
}
