using DashDetective.Services.Diagnostics;
using DashDetective.Services.SystemMetrics;
using System;
using System.Collections.Generic;
using System.Runtime.Versioning;

namespace DashDetective.Tabs.Performance;

/// <summary>
/// Routes a GPU's temperature/power read to the reader for that adapter's vendor. Windows has no in-box GPU
/// sensor API, so each vendor is served by the SDK its own display driver installs — no package, no
/// redistributable, no admin. An adapter whose vendor has no reader simply reports nothing, which is how the
/// AMD and Intel tiles stay "—" until those readers exist.
///
/// Page-local to the Performance tab, following the feature-local P/Invoke precedent of File Explorer's
/// <c>ShellInterop</c> and the Network tab's
/// <c>ConnectionsInterop</c>. Never throws: a reader that faults is logged once and dropped for the rest of
/// the session, so a persistent fault can't flood the log at the sampling cadence. The platform check lives
/// in <see cref="IGpuSensorProvider.ForCurrentPlatform"/>.
/// </summary>
internal sealed class WindowsGpuSensorProvider : IGpuSensorProvider {
    private readonly List<IGpuSensorReader> _readers;

    /// <summary>Builds the provider over the vendor readers available on this machine.</summary>
    [SupportedOSPlatform("windows")]
    public WindowsGpuSensorProvider() : this(CreateReaders()) { }

    /// <summary>Test seam: the same routing over an explicit reader set. Deliberately unannotated — the
    /// routing itself is portable, so every one of its tests runs on the Linux leg too.</summary>
    internal WindowsGpuSensorProvider(IEnumerable<IGpuSensorReader> readers) =>
        _readers = new List<IGpuSensorReader>(readers);

    /// <summary>The vendor readers to run on this machine. Intel is deferred — its adapters fall through to no
    /// reader and keep showing "—". Constructing a reader is cheap: each one initializes its libraries lazily,
    /// on the first adapter it is actually asked about. This is the Windows-only step, which is why the
    /// public constructor above carries the attribute and this does the actual work.</summary>
    [SupportedOSPlatform("windows")]
    private static IEnumerable<IGpuSensorReader> CreateReaders() =>
        [new NvidiaGpuSensorReader(), new AmdGpuSensorReader()];

    /// <summary>Reads one adapter's sensors, or <see cref="GpuSensorSample.None"/> when its vendor has no
    /// reader (or the read reports nothing). <paramref name="adapterKey"/> is the adapter's LUID token.</summary>
    public GpuSensorSample Read(string adapterKey, GpuPciId? pci) {
        if (pci is null)
            return GpuSensorSample.None;

        var reader = SelectReader(pci.VendorId, _readers);
        if (reader is null)
            return GpuSensorSample.None;

        try {
            return reader.Read(adapterKey, pci);
        } catch (Exception e) {
            // A reader is expected to soft-fail internally; a throw means it is broken, so drop it entirely
            // rather than re-entering it every tick.
            Log.Warn($"GpuSensorProvider reader for vendor 0x{pci.VendorId:X4} failed", e);
            _readers.Remove(reader);
            reader.Dispose();
            return GpuSensorSample.None;
        }
    }

    /// <summary>The reader serving a PCI vendor id, or <c>null</c> when none does. Pure; unit-tested.</summary>
    internal static IGpuSensorReader? SelectReader(uint vendorId, IReadOnlyList<IGpuSensorReader> readers) {
        foreach (var reader in readers)
            if (reader.VendorId == vendorId)
                return reader;
        return null;
    }

    public void Dispose() {
        foreach (var reader in _readers)
            reader.Dispose();
        _readers.Clear();
    }
}
