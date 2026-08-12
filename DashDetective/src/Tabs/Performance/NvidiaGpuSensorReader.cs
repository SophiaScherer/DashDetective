using DashDetective.Services.Diagnostics;
using DashDetective.Services.SystemMetrics;
using System;
using System.Collections.Generic;
using System.Runtime.Versioning;

namespace DashDetective.Tabs.Performance;

/// <summary>
/// Reads NVIDIA GPU temperature and power. Temperature comes from NVAPI's published
/// <c>NvAPI_GPU_GetThermalSettings</c>; power from NVML's <c>nvmlDeviceGetPowerUsage</c> — see
/// <see cref="NvmlInterop"/> for why power is not read through NVAPI. Both DLLs ship with the display driver,
/// so this adds no package and needs no admin.
///
/// Both libraries are initialized once, lazily (NVML's init costs roughly 10 ms), and each adapter is resolved
/// to a vendor-reported GPU once and then cached. The two sources degrade <b>independently</b>: a board whose
/// driver won't report power still shows a temperature.
///
/// All the decode logic — which thermal sensor to believe, and whether a reading is plausible — is factored
/// into pure static methods below, so it is unit-tested while the interop is left to the smoke run.
/// </summary>
internal sealed class NvidiaGpuSensorReader : IGpuSensorReader {
    /// <summary>PCI vendor id for NVIDIA.</summary>
    private const uint NvidiaVendorId = 0x10DE;

    /// <summary>NVAPI_THERMAL_TARGET_GPU — the sensor measuring the GPU core itself, as opposed to memory,
    /// board or power supply.</summary>
    private const int ThermalTargetGpu = 1;

    // Plausible windows; anything outside means "not really reported" (the DiskTemperatureProvider idiom).
    private const int MinCelsius = 1;
    private const int MaxCelsius = 150;
    private const double MinWatts = 0.1;
    private const double MaxWatts = 2000;

    private readonly List<IntPtr> _nvapiGpus = [];
    private readonly List<VendorPciId> _nvapiPci = [];
    private readonly List<IntPtr> _nvmlDevices = [];
    private readonly List<VendorPciId> _nvmlPci = [];

    // Adapter → vendor index, resolved once. The claimed sets make two identical cards pair up in enumeration
    // order rather than both resolving to the vendor's first GPU.
    private readonly Dictionary<string, int> _nvapiByAdapter = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _nvmlByAdapter = new(StringComparer.Ordinal);
    private readonly HashSet<int> _nvapiClaimed = [];
    private readonly HashSet<int> _nvmlClaimed = [];

    // Reused across ticks so the per-second read allocates nothing.
    private readonly int[] _sensorTargets = new int[NvApiInterop.MaxThermalSensors];
    private readonly int[] _sensorTemperatures = new int[NvApiInterop.MaxThermalSensors];

    private bool _initialized;
    private bool _thermalReady;
    private bool _powerReady;

    /// <summary>Empty, and annotated on purpose: this is the enforcement point for <see cref="NvApiInterop"/>
    /// and <see cref="NvmlInterop"/>. Both are hand-written <c>DllImport</c> classes that CA1416 cannot see,
    /// so the attribute goes where the analyzer can act on it — the only place this class comes into
    /// existence — which forces the guard all the way up in
    /// <see cref="IGpuSensorProvider.ForCurrentPlatform"/>. It stays off the type so the pure decode statics
    /// below keep running on every CI leg.</summary>
    [SupportedOSPlatform("windows")]
    public NvidiaGpuSensorReader() { }

    public uint VendorId => NvidiaVendorId;

    public GpuSensorSample Read(string adapterKey, GpuPciId pci) {
        EnsureInitialized();
        return new GpuSensorSample(ReadTemperature(adapterKey, pci), ReadPower(adapterKey, pci));
    }

    /// <summary>Initializes both libraries and snapshots each one's GPUs with their PCI ids. Runs once; a
    /// failure of either side is logged and leaves that metric permanently unavailable, rather than being
    /// retried at the sampling cadence.</summary>
    private void EnsureInitialized() {
        if (_initialized)
            return;
        _initialized = true;

        try {
            if (NvApiInterop.Initialize()) {
                foreach (var gpu in NvApiInterop.EnumPhysicalGpus()) {
                    if (NvApiInterop.ReadPciId(gpu) is not { } pci)
                        continue;
                    _nvapiGpus.Add(gpu);
                    _nvapiPci.Add(pci);
                }
                _thermalReady = _nvapiGpus.Count > 0;
            }
        } catch (Exception e) {
            Log.Warn("NvidiaGpuSensorReader NVAPI initialization failed", e);
        }

        try {
            if (NvmlInterop.Initialize()) {
                foreach (var device in NvmlInterop.EnumDevices()) {
                    if (NvmlInterop.ReadPciId(device) is not { } pci)
                        continue;
                    _nvmlDevices.Add(device);
                    _nvmlPci.Add(pci);
                }
                _powerReady = _nvmlDevices.Count > 0;
            }
        } catch (Exception e) {
            Log.Warn("NvidiaGpuSensorReader NVML initialization failed", e);
        }
    }

    private double? ReadTemperature(string adapterKey, GpuPciId pci) {
        if (!_thermalReady)
            return null;
        if (Resolve(adapterKey, pci, _nvapiPci, _nvapiByAdapter, _nvapiClaimed) is not { } index)
            return null;

        var count = NvApiInterop.ReadThermalSensors(_nvapiGpus[index], _sensorTargets, _sensorTemperatures);
        var sensor = SelectGpuSensorIndex(count, _sensorTargets);
        return sensor >= 0 ? PlausibleCelsius(_sensorTemperatures[sensor]) : null;
    }

    private double? ReadPower(string adapterKey, GpuPciId pci) {
        if (!_powerReady)
            return null;
        if (Resolve(adapterKey, pci, _nvmlPci, _nvmlByAdapter, _nvmlClaimed) is not { } index)
            return null;

        return NvmlInterop.ReadPowerMilliwatts(_nvmlDevices[index]) is { } milliwatts
            ? PlausibleWatts(milliwatts)
            : null;
    }

    /// <summary>Resolves (and remembers) which vendor-reported GPU an adapter is. A negative cache entry
    /// records "this adapter isn't one of ours", so the match isn't retried every tick.</summary>
    private static int? Resolve(string adapterKey, GpuPciId pci, IReadOnlyList<VendorPciId> candidates,
                                Dictionary<string, int> cache, HashSet<int> claimed) {
        if (cache.TryGetValue(adapterKey, out var cached))
            return cached >= 0 ? cached : null;

        var index = GpuPciMatcher.Match(pci, candidates, claimed);
        cache[adapterKey] = index ?? -1;
        if (index is { } resolved)
            claimed.Add(resolved);
        return index;
    }

    /// <summary>Picks which of a GPU's thermal sensors to show: the one measuring the GPU core, else the first
    /// reported sensor. Returns -1 when the driver reported none. Pure; unit-tested.</summary>
    internal static int SelectGpuSensorIndex(int count, IReadOnlyList<int> targets) {
        if (count <= 0 || targets.Count == 0)
            return -1;

        var limit = Math.Min(count, targets.Count);
        for (var i = 0; i < limit; i++)
            if (targets[i] == ThermalTargetGpu)
                return i;
        return 0;
    }

    /// <summary>Accepts a sensor reading only inside a plausible GPU range, so a zero ("not reported") or a
    /// garbage value blanks the tile instead of being displayed. Pure; unit-tested.</summary>
    internal static double? PlausibleCelsius(int celsius) =>
        celsius is >= MinCelsius and <= MaxCelsius ? celsius : null;

    /// <summary>Converts NVML's milliwatts to watts, rejecting readings outside a plausible board range.
    /// Pure; unit-tested.</summary>
    internal static double? PlausibleWatts(uint milliwatts) {
        var watts = milliwatts / 1000.0;
        return watts is >= MinWatts and <= MaxWatts ? watts : null;
    }

    public void Dispose() {
        if (_thermalReady)
            NvApiInterop.Unload();
        if (_powerReady)
            NvmlInterop.Shutdown();
        _thermalReady = false;
        _powerReady = false;
    }
}
