using DashDetective.Services.Diagnostics;
using DashDetective.Services.SystemMetrics;
using System;
using System.Collections.Generic;

namespace DashDetective.Tabs.Performance;

/// <summary>
/// Reads AMD GPU temperature via ADL's PMLOG snapshot (see <see cref="AdlInterop"/>). Adapters are identified
/// from their PNP device string, since ADL's own vendor field is unusable.
///
/// <b>Temperature only — power is deliberately not reported.</b> ADL's power sensors were measured against a
/// pure CPU load on a Radeon integrated GPU: <c>GFX_POWER</c> climbed to ~50 W while <c>INFO_ACTIVITY_GFX</c>
/// stayed pinned at 0 %, i.e. it reports whole-package power rather than the GPU's, and <c>ASIC_POWER</c>
/// swung erratically between 0 and 64 W on an idle part. Publishing either as "GPU Power" would be actively
/// misleading, so the Power tile stays "—" for AMD. Temperature does rise with CPU load on an integrated part,
/// but that is physically honest: the GPU shares the CPU's die.
///
/// The sensor-selection and parsing logic is factored into pure static methods (here and in
/// <see cref="PnpPciParser"/>), so it is unit-tested while the interop is left to the smoke run.
/// </summary>
internal sealed class AmdGpuSensorReader : IGpuSensorReader {
    /// <summary>PCI vendor id for AMD/ATI.</summary>
    private const uint AmdVendorId = 0x1002;

    // ADLSensorType indices, from AMD's published adl_defines.h. The preference order matters: EDGE is the
    // conventional "GPU temperature" on discrete boards, but integrated parts report GFX instead (verified on
    // a Radeon iGPU, where EDGE is absent and GFX reads a plausible 40-49 °C).
    private const int SensorTemperatureEdge = 8;
    private const int SensorTemperatureHotspot = 27;
    private const int SensorTemperatureGfx = 28;

    // Plausible window; anything outside means "not really reported".
    private const int MinCelsius = 1;
    private const int MaxCelsius = 150;

    private readonly List<int> _adapterIndices = [];
    private readonly List<VendorPciId> _adapterPci = [];
    private readonly Dictionary<string, int> _byAdapter = new(StringComparer.Ordinal);
    private readonly HashSet<int> _claimed = [];

    // Reused across ticks so the per-second read allocates nothing.
    private readonly int[] _sensorSupported = new int[AdlInterop.MaxSensors];
    private readonly int[] _sensorValues = new int[AdlInterop.MaxSensors];

    private bool _initialized;
    private bool _ready;

    public uint VendorId => AmdVendorId;

    public GpuSensorSample Read(string adapterKey, GpuPciId pci) {
        EnsureInitialized();
        // Power is intentionally never reported for AMD — see the class remarks.
        return new GpuSensorSample(ReadTemperature(adapterKey, pci), null);
    }

    /// <summary>Creates the ADL context and snapshots its AMD adapters. Runs once; ADL lists one physical GPU
    /// per display output, so only the first index for each distinct PCI identity is kept.</summary>
    private void EnsureInitialized() {
        if (_initialized)
            return;
        _initialized = true;

        try {
            if (!AdlInterop.Initialize())
                return;

            var seen = new HashSet<VendorPciId>();
            foreach (var (index, pnpString) in AdlInterop.EnumAdapters()) {
                if (PnpPciParser.ReadVendorId(pnpString) != AmdVendorId)
                    continue;   // ADL enumerates other vendors' adapters too
                if (PnpPciParser.Parse(pnpString) is not { } pci || !seen.Add(pci))
                    continue;

                _adapterIndices.Add(index);
                _adapterPci.Add(pci);
            }
            _ready = _adapterIndices.Count > 0;
        } catch (Exception e) {
            Log.Warn("AmdGpuSensorReader ADL initialization failed", e);
        }
    }

    private double? ReadTemperature(string adapterKey, GpuPciId pci) {
        if (!_ready)
            return null;
        if (Resolve(adapterKey, pci) is not { } index)
            return null;
        if (!AdlInterop.ReadSensors(_adapterIndices[index], _sensorSupported, _sensorValues))
            return null;

        return SelectTemperature(_sensorSupported, _sensorValues);
    }

    /// <summary>Resolves (and remembers) which ADL adapter a row is. A negative entry records "not one of
    /// ours", so the match isn't retried every tick.</summary>
    private int? Resolve(string adapterKey, GpuPciId pci) {
        if (_byAdapter.TryGetValue(adapterKey, out var cached))
            return cached >= 0 ? cached : null;

        var index = GpuPciMatcher.Match(pci, _adapterPci, _claimed);
        _byAdapter[adapterKey] = index ?? -1;
        if (index is { } resolved)
            _claimed.Add(resolved);
        return index;
    }

    /// <summary>Picks a GPU temperature out of a PMLOG snapshot: the edge sensor if the board has one, else
    /// the graphics-core sensor, else the hotspot. Returns <c>null</c> when none is supported or the reading
    /// is outside a plausible range. Pure; unit-tested.</summary>
    internal static double? SelectTemperature(IReadOnlyList<int> supported, IReadOnlyList<int> values) {
        foreach (var sensor in (int[])[SensorTemperatureEdge, SensorTemperatureGfx, SensorTemperatureHotspot]) {
            if (sensor >= supported.Count || sensor >= values.Count || supported[sensor] == 0)
                continue;
            if (values[sensor] is >= MinCelsius and <= MaxCelsius)
                return values[sensor];
        }
        return null;
    }

    public void Dispose() {
        if (_ready || _initialized)
            AdlInterop.Shutdown();
        _ready = false;
    }
}
