using DashDetective.Services.Diagnostics;
using DashDetective.Services.Platform.Windows;
using System;
using System.Collections.Generic;
using System.Management;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;

namespace DashDetective.Services.SystemMetrics;

/// <summary>
/// A GPU's details on Windows. DirectX version: <c>D3D12CreateDevice</c>'s capability test on the live DXGI
/// adapter (null <c>ppDevice</c>), which returns no device but does load <c>d3d12.dll</c>,
/// <c>D3D12Core.dll</c> and the vendor's user-mode driver — why this is a separate read only the Performance
/// tab requests. Driver version and location: the shared <see cref="VideoControllers"/> rows, joined by PCI
/// identity (<see cref="GpuDriverRows"/>). Each fact soft-fails to null on its own; nothing is read when no
/// requested adapter exists.
///
/// Annotated on the TYPE, not the constructor: unlike a P/Invoke-only reader, its methods call annotated
/// BCL APIs (WMI, the registry) and the type-annotated <see cref="WindowsGpuAdapterProvider"/>, so every
/// member is Windows-only and a constructor-only attribute would not build.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsGpuDetailsProvider : IGpuDetailsProvider {
    // IID_ID3D12Device.
    private static readonly Guid IidD3D12Device = new("189819f1-1db6-4b57-be54-1821339b85f7");

    /// <summary><c>D3D_FEATURE_LEVEL</c>s Direct3D 12 can run at, highest first: 12_2, 12_1, 12_0, 11_1,
    /// 11_0. A runtime too old to know 12_2 refuses it and the probe falls through to 12_1.</summary>
    private static readonly int[] FeatureLevels = [0xc200, 0xc100, 0xc000, 0xb100, 0xb000];

    /// <summary>Set once Direct3D 12 is found missing, so the process stops asking and logs it once.</summary>
    private static int _d3d12Missing;

    [DllImport("d3d12.dll")]
    private static extern int D3D12CreateDevice(IntPtr adapter, int minimumFeatureLevel, ref Guid riid, IntPtr device);

    public Task<IReadOnlyDictionary<string, GpuDetails>> GetAsync(IReadOnlyCollection<string> adapterKeys) =>
        Task.Run(() => Read(new HashSet<string>(adapterKeys, StringComparer.Ordinal)));

    private static IReadOnlyDictionary<string, GpuDetails> Read(HashSet<string> wanted) {
        var directX = new Dictionary<string, string?>(StringComparer.Ordinal);
        var adapters = WindowsGpuAdapterProvider.Enumerate((adapter, pointer) => {
            if (!adapter.IsSoftware && wanted.Contains(adapter.LuidToken))
                directX[adapter.LuidToken] = ProbeDirectX(pointer);
        });

        var details = new Dictionary<string, GpuDetails>(StringComparer.Ordinal);
        if (directX.Count == 0)
            return details;

        var rows = ReadDriverRows();
        foreach (var adapter in adapters) {
            if (!directX.TryGetValue(adapter.LuidToken, out var dx))
                continue;
            var (driver, location) = adapter.Pci is { } pci ? GpuDriverRows.For(pci, rows) : (null, null);
            details[adapter.LuidToken] = new GpuDetails(driver, dx, location);
        }
        return details;
    }

    /// <summary>Every PCI display adapter's driver row; empty when WMI cannot be read, which blanks only
    /// the driver and location rows.</summary>
    private static IReadOnlyList<GpuDriverRow> ReadDriverRows() {
        try {
            var rows = new List<GpuDriverRow>();
            foreach (var controller in VideoControllers.Read()) {
                var row = GpuDriverRows.From(
                    controller.PnpDeviceId, controller.DriverVersion,
                    VideoControllers.LocationInformation(controller.PnpDeviceId));
                if (row is not null)
                    rows.Add(row);
            }
            return rows;
        } catch (Exception e) when (e is ManagementException or COMException or UnauthorizedAccessException) {
            // WMI's failure modes: a broken repository or query, the service unavailable, or access denied.
            Log.Warn("GPU driver details unreadable", e);
            return [];
        }
    }

    /// <summary>
    /// The adapter's highest Direct3D 12 feature level as "12 (FL x.y)", or null when Direct3D 12 accepts
    /// none. Any success code counts as supported: with a null <c>ppDevice</c> the documented answer is
    /// S_FALSE, but S_OK means the same.
    /// </summary>
    private static string? ProbeDirectX(IntPtr adapter) {
        if (Volatile.Read(ref _d3d12Missing) != 0)
            return null;

        try {
            foreach (var level in FeatureLevels) {
                var iid = IidD3D12Device;
                if (D3D12CreateDevice(adapter, level, ref iid, IntPtr.Zero) >= 0)
                    return GpuDetails.FormatDirectX(level);
            }
        } catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException
                                       or BadImageFormatException) {
            if (Interlocked.Exchange(ref _d3d12Missing, 1) == 0)
                Log.Warn("Direct3D 12 unavailable; no DirectX version will be reported", e);
        }
        return null;
    }
}

/// <summary>The no-details contract for a platform without a reader: every adapter is absent, so the
/// Performance tab lists nothing.</summary>
internal sealed class UnsupportedGpuDetailsProvider : IGpuDetailsProvider {
    public Task<IReadOnlyDictionary<string, GpuDetails>> GetAsync(IReadOnlyCollection<string> adapterKeys) =>
        Task.FromResult<IReadOnlyDictionary<string, GpuDetails>>(new Dictionary<string, GpuDetails>());
}
