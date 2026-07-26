using DashDetective.Services.Diagnostics;
using DashDetective.Services.SystemMetrics;
using System;
using System.Collections.Generic;

namespace DashDetective.Tabs.Performance;

/// <summary>
/// Reads AMD GPU temperature via ADL's PMLOG snapshot (see <see cref="AdlInterop"/>). Adapters are identified
/// from their PNP device string, since ADL's own vendor field is unusable.
///
/// <b>Power is reported only on discrete boards, and only from <c>BOARD_POWER</c>.</b> Two AMD-specific traps
/// force that narrowness:
/// <list type="bullet">
/// <item><b>Integrated parts report package power, not GPU power.</b> Measured against a pure CPU load on a
/// Radeon iGPU, <c>GFX_POWER</c> climbed to ~50 W while <c>INFO_ACTIVITY_GFX</c> stayed pinned at 0 %, and
/// <c>ASIC_POWER</c> swung erratically between 0 and 64 W on an idle part. Hence the discrete gate
/// (<see cref="IsDiscrete"/>), which is verified: ADL reports INTEGRATED|FUSION for the iGPU it was
/// developed against.</item>
/// <item><b><c>ASIC_POWER</c> is not board power.</b> On older discrete cards it is *chip* power, excluding
/// some rails — a plausible-looking number that understates real draw and would not mean the same thing as
/// the NVIDIA tile beside it. There is deliberately <b>no fallback to it</b>: a board that doesn't report
/// <c>BOARD_POWER</c> shows "—" rather than a quietly wrong figure.</item>
/// </list>
/// Temperature rising with CPU load on an integrated part is not a bug — the GPU shares the CPU's die.
///
/// <b>Unverified:</b> no discrete Radeon was available, so the <c>BOARD_POWER</c> path has never produced a
/// reading on real hardware — the read mechanism is the same verified <c>QueryPMLogData</c> call the
/// temperature uses, but that the sensor carries whole watts of total board power is taken from AMD's header
/// enum, not measured. The plausibility window rejects a grossly wrong unit scale (a milliwatt or centiwatt
/// reading lands outside it and blanks the tile), but would not catch a small one.
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

    /// <summary>PMLOG_BOARD_POWER — total board power in watts. The only power sensor read; see the class
    /// remarks for why <c>ASIC_POWER</c> is not used as a fallback.</summary>
    private const int SensorBoardPower = 73;

    // ADL_ASIC_* family-type bits from adl_defines.h.
    private const int AsicDiscrete = 1 << 0;
    private const int AsicIntegrated = 1 << 1;
    private const int AsicFusion = 1 << 5;

    // Plausible windows; anything outside means "not really reported".
    private const int MinCelsius = 1;
    private const int MaxCelsius = 150;
    private const int MinWatts = 1;
    private const int MaxWatts = 2000;

    private readonly List<int> _adapterIndices = [];
    private readonly List<VendorPciId> _adapterPci = [];
    private readonly List<bool> _adapterIsDiscrete = [];
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
        if (!_ready)
            return GpuSensorSample.None;
        if (Resolve(adapterKey, pci) is not { } index)
            return GpuSensorSample.None;
        // One PMLOG snapshot feeds both tiles — it is the expensive part of the tick (~0.5 ms).
        if (!AdlInterop.ReadSensors(_adapterIndices[index], _sensorSupported, _sensorValues))
            return GpuSensorSample.None;

        var celsius = SelectTemperature(_sensorSupported, _sensorValues);
        // Integrated parts report package power, so power is discrete-only — see the class remarks.
        double? watts = _adapterIsDiscrete[index] ? SelectBoardPower(_sensorSupported, _sensorValues) : null;
        return new GpuSensorSample(celsius, watts);
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
                // "ADL won't say" is treated as not discrete, so an unknown board never reports power.
                _adapterIsDiscrete.Add(AdlInterop.ReadAsicFamilyType(index) is { } types && IsDiscrete(types));
            }
            _ready = _adapterIndices.Count > 0;
        } catch (Exception e) {
            Log.Warn("AmdGpuSensorReader ADL initialization failed", e);
        }
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

    /// <summary>Whether ADL's <c>ADL_ASIC_*</c> bits describe a discrete board. Integrated and Fusion (APU)
    /// parts are excluded even if the discrete bit is also set, since on those the power sensors read the
    /// whole package rather than the GPU. Pure; unit-tested.</summary>
    internal static bool IsDiscrete(int asicTypes) =>
        (asicTypes & AsicDiscrete) != 0 && (asicTypes & (AsicIntegrated | AsicFusion)) == 0;

    /// <summary>Reads total board power out of a PMLOG snapshot, or <c>null</c> when the board doesn't report
    /// it or the value is outside a plausible range. There is deliberately no <c>ASIC_POWER</c> fallback —
    /// see the class remarks. Pure; unit-tested.</summary>
    internal static double? SelectBoardPower(IReadOnlyList<int> supported, IReadOnlyList<int> values) {
        if (SensorBoardPower >= supported.Count || SensorBoardPower >= values.Count)
            return null;
        if (supported[SensorBoardPower] == 0)
            return null;

        return values[SensorBoardPower] is >= MinWatts and <= MaxWatts ? values[SensorBoardPower] : null;
    }

    public void Dispose() {
        if (_ready || _initialized)
            AdlInterop.Shutdown();
        _ready = false;
    }
}
