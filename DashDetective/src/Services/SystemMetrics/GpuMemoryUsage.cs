using System;
using System.Collections.Generic;

namespace DashDetective.Services.SystemMetrics;

/// <summary>
/// Turns raw <c>\GPU Adapter Memory(*)\Dedicated Usage</c> readings (bytes, one instance per
/// <c>luid_0x…_0x…_phys_N</c>) into one figure per adapter, and joins them onto the engine-counter samples.
///
/// <b>The join never adds an adapter.</b> The inventory keys GPUs off the engine counter's LUIDs, so a
/// memory-only LUID is dropped; an adapter with no memory reading keeps a null usage, never 0.
///
/// Pure and outside the Windows-gated sampler, so it is tested on every CI leg.
/// </summary>
internal static class GpuMemoryUsage {
    private const string LuidToken = "luid_0x";
    private const string PhysToken = "_phys_";

    /// <summary>Dedicated bytes in use per adapter LUID token, summed across physical indices. Names that
    /// do not parse, and readings that are negative or not a number, are skipped.</summary>
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
    /// <see cref="GpuAdapterSample.DedicatedUsedBytes"/> filled from <paramref name="memory"/> where it has an
    /// entry. Adapters absent from the samples stay absent.</summary>
    internal static IReadOnlyDictionary<string, GpuAdapterSample> Join(
        IReadOnlyDictionary<string, GpuAdapterSample> samples, IReadOnlyDictionary<string, ulong> memory) {
        var joined = new Dictionary<string, GpuAdapterSample>(samples.Count, StringComparer.Ordinal);
        foreach (var (luid, sample) in samples)
            joined[luid] = memory.TryGetValue(luid, out var used) ? sample with { DedicatedUsedBytes = used } : sample;
        return joined;
    }

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
