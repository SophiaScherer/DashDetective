using System;
using System.Collections.Generic;

namespace DashDetective.Services.SystemMetrics;

/// <summary>
/// Turns raw <c>\GPU Engine(*)\Utilization Percentage</c> readings into Task Manager's figures. The rule:
/// an engine's load is the SUM over the process instances on that one physical engine (LUID + phys +
/// engine index), clamped to 0–100; an adapter's GPU % is its busiest engine; a per-type figure ("3D",
/// "Copy") is the busiest engine of that type; and a process's GPU % is its busiest engine.
///
/// <b>Engines of one type are never summed.</b> An adapter can carry several (six Copy engines on an
/// RTX 3060), and adding them inflated the type's figure past what any one engine was doing.
/// </summary>
internal static class GpuEngineLoad {
    private readonly record struct EngineKey(string Luid, int Phys, int Engine);

    private readonly record struct ProcessEngineKey(int Pid, EngineKey Engine);

    /// <summary>Per-adapter readings keyed by LUID token. Names that do not parse are skipped.</summary>
    internal static IReadOnlyDictionary<string, GpuAdapterSample> ByAdapter(
        IEnumerable<(string? Name, double Value)> readings) {
        var engines = new Dictionary<EngineKey, (string Type, double Load)>();
        foreach (var (name, value) in readings) {
            if (!GpuEngineInstance.TryParse(name, out var instance))
                continue;
            var key = new EngineKey(instance.Luid, instance.Phys, instance.Engine);
            var load = engines.TryGetValue(key, out var running) ? running.Load : 0;
            engines[key] = (instance.Type, load + value);
        }

        var perAdapter = new Dictionary<string, Dictionary<string, double>>(StringComparer.Ordinal);
        foreach (var (key, (type, load)) in engines) {
            if (!perAdapter.TryGetValue(key.Luid, out var byType))
                perAdapter[key.Luid] = byType = new Dictionary<string, double>(StringComparer.Ordinal);
            var clamped = Clamp(load);
            if (!byType.TryGetValue(type, out var busiest) || clamped > busiest)
                byType[type] = clamped;
        }

        var result = new Dictionary<string, GpuAdapterSample>(StringComparer.Ordinal);
        foreach (var (luid, byType) in perAdapter) {
            double overall = 0;
            foreach (var load in byType.Values)
                overall = Math.Max(overall, load);
            result[luid] = new GpuAdapterSample(overall, byType);
        }
        return result;
    }

    /// <summary>PID → that process's busiest engine across every adapter. Names without a PID are
    /// skipped.</summary>
    internal static IReadOnlyDictionary<int, double> ByProcess(IEnumerable<(string? Name, double Value)> readings) {
        var engines = new Dictionary<ProcessEngineKey, double>();
        foreach (var (name, value) in readings) {
            if (!GpuEngineInstance.TryParse(name, out var instance) || instance.Pid is not { } pid)
                continue;
            var key = new ProcessEngineKey(pid, new EngineKey(instance.Luid, instance.Phys, instance.Engine));
            engines[key] = engines.GetValueOrDefault(key) + value;
        }

        var result = new Dictionary<int, double>();
        foreach (var (key, load) in engines) {
            var clamped = Clamp(load);
            if (!result.TryGetValue(key.Pid, out var busiest) || clamped > busiest)
                result[key.Pid] = clamped;
        }
        return result;
    }

    private static double Clamp(double load) => Math.Clamp(load, 0, 100);
}
