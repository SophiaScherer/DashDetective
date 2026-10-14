using DashDetective.Services.Diagnostics;
using DashDetective.Services.Platform.Windows;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading.Tasks;

namespace DashDetective.Services.SystemMetrics;

/// <summary>
/// Enumerates the machine's graphics adapters via DXGI (<c>dxgi.dll</c>: <c>CreateDXGIFactory1</c> →
/// <c>EnumAdapters1</c> → <c>GetDesc1</c>) — the authoritative LUID→name map. Unlike WMI's
/// <c>Win32_VideoController</c> (which <c>HardwareInfoProvider</c> still reads for its spec card), DXGI
/// exposes each adapter's <b>LUID</b> (so per-GPU PDH counters can be attributed to a name) and a software
/// flag (so the Microsoft Basic Render Driver and similar are dropped). Each <see cref="GpuAdapter.LuidToken"/>
/// is formatted to match the PDH instance-name token (<c>luid_0x{High:x8}_0x{Low:x8}</c>).
///
/// COM is reached via <b>raw vtable function pointers</b> (<see cref="Marshal.GetDelegateForFunctionPointer"/>),
/// not <c>[ComImport]</c> RCWs: built-in COM is disabled by a runtime feature switch, so an RCW throws
/// "Built-in COM has been disabled". This needs no <c>unsafe</c> and no csproj change. Soft-fails to an empty
/// list on any failure, like the other providers; the platform check lives in
/// <see cref="HardwareProviders.ForCurrentPlatform"/>.
///
/// Each adapter also carries its <see cref="GpuDetails"/>: the DirectX version from a Direct3D 12 capability
/// test on the live adapter, and the driver version and bus location from the shared
/// <see cref="VideoControllers"/> rows, joined by PCI identity. Each soft-fails to null on its own.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsGpuAdapterProvider : IGpuAdapterProvider {
    public Task<IReadOnlyList<GpuAdapter>> GetAsync() => Task.Run(Read);

    // ---- DXGI interop (vtable slots per the IDXGIFactory1 / IDXGIAdapter1 layouts) ----

    // IID_IDXGIFactory1.
    private static readonly Guid IidFactory1 = new("770aae78-f26f-4dba-a829-253c83d1b387");

    private const int VtRelease = 2;          // IUnknown::Release
    private const int VtEnumAdapters1 = 12;   // IDXGIFactory1::EnumAdapters1
    private const int VtGetDesc1 = 10;        // IDXGIAdapter1::GetDesc1

    private const int SOk = 0;                              // S_OK
    private const uint DxgiErrorNotFound = 0x887A0002;      // enumerated past the last adapter
    private const uint DxgiAdapterFlagSoftware = 2;         // DXGI_ADAPTER_FLAG_SOFTWARE

    [DllImport("dxgi.dll")]
    private static extern int CreateDXGIFactory1(ref Guid riid, out IntPtr ppFactory);

    // ---- Direct3D 12 capability test (DirectX version) ----

    // IID_ID3D12Device.
    private static readonly Guid IidD3D12Device = new("189819f1-1db6-4b57-be54-1821339b85f7");

    private const int SFalse = 1;   // S_FALSE: the level is supported, and no device was asked for

    /// <summary><c>D3D_FEATURE_LEVEL</c>s Direct3D 12 can run at, highest first: 12_2, 12_1, 12_0, 11_1,
    /// 11_0. A runtime too old to know 12_2 refuses it and the probe falls through to 12_1.</summary>
    private static readonly int[] FeatureLevels = [0xc200, 0xc100, 0xc000, 0xb100, 0xb000];

    [DllImport("d3d12.dll")]
    private static extern int D3D12CreateDevice(IntPtr adapter, int minimumFeatureLevel, ref Guid riid, IntPtr device);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int EnumAdapters1Fn(IntPtr self, uint adapter, out IntPtr ppAdapter);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetDesc1Fn(IntPtr self, out AdapterDesc1 desc);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate uint ReleaseFn(IntPtr self);

    /// <summary>Layout of <c>DXGI_ADAPTER_DESC1</c>. <see cref="Description"/> is a fixed 128-wchar field;
    /// the three memory fields are pointer-sized (<c>SIZE_T</c>); <see cref="AdapterLuid"/> is
    /// <c>{ uint LowPart; int HighPart }</c>.</summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct AdapterDesc1 {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string Description;
        public uint VendorId;
        public uint DeviceId;
        public uint SubSysId;
        public uint Revision;
        public UIntPtr DedicatedVideoMemory;
        public UIntPtr DedicatedSystemMemory;
        public UIntPtr SharedSystemMemory;
        public uint LuidLow;
        public int LuidHigh;
        public uint Flags;
    }

    private static IReadOnlyList<GpuAdapter> Read() {
        var adapters = ReadDxgi();
        if (adapters.Count == 0)
            return adapters;

        // Driver version and bus location come from the device records, joined on PCI identity.
        var rows = ReadDriverRows();
        var detailed = new List<GpuAdapter>(adapters.Count);
        foreach (var adapter in adapters) {
            var (driver, location) = adapter.Pci is { } pci ? GpuDriverRows.For(pci, rows) : (null, null);
            detailed.Add(adapter with {
                Details = new GpuDetails(driver, adapter.Details?.DirectXVersion, location),
            });
        }
        return detailed;
    }

    /// <summary>Every PCI display adapter's driver row; empty when WMI cannot be read, which leaves only
    /// the driver and location rows blank.</summary>
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
        } catch (Exception e) {
            // Unfiltered on purpose: these rows are secondary, and an escape here would fault the whole
            // adapter list and take every GPU card with it.
            Log.Warn("GPU driver details unreadable", e);
            return [];
        }
    }

    /// <summary>
    /// The adapter's highest Direct3D 12 feature level, formatted as Task Manager shows it, or null when
    /// Direct3D 12 accepts none. Uses <c>D3D12CreateDevice</c>'s documented capability test: with a null
    /// <c>ppDevice</c> it answers S_FALSE for a supported level and creates no device.
    /// </summary>
    private static string? ProbeDirectX(IntPtr adapter) {
        try {
            foreach (var level in FeatureLevels) {
                var iid = IidD3D12Device;
                if (D3D12CreateDevice(adapter, level, ref iid, IntPtr.Zero) == SFalse)
                    return GpuDetails.FormatDirectX(level);
            }
        } catch (Exception e) {
            // Unfiltered: it runs inside the DXGI walk, whose own catch would end the enumeration and drop
            // every adapter after this one. Typically no d3d12.dll (DllNotFoundException).
            Log.Warn("Direct3D 12 probe failed; no DirectX version reported", e);
        }
        return null;
    }

    private static List<GpuAdapter> ReadDxgi() {
        var adapters = new List<GpuAdapter>();
        var factory = IntPtr.Zero;
        try {
            var iid = IidFactory1;
            if (CreateDXGIFactory1(ref iid, out factory) != SOk || factory == IntPtr.Zero)
                return adapters;

            var enumAdapters = GetMethod<EnumAdapters1Fn>(factory, VtEnumAdapters1);
            for (uint i = 0; ; i++) {
                var hr = enumAdapters(factory, i, out var adapter);
                if ((uint)hr == DxgiErrorNotFound || adapter == IntPtr.Zero)
                    break;
                if (hr != SOk)
                    continue;

                try {
                    var getDesc1 = GetMethod<GetDesc1Fn>(adapter, VtGetDesc1);
                    if (getDesc1(adapter, out var desc) != SOk)
                        continue;

                    var software = (desc.Flags & DxgiAdapterFlagSoftware) != 0;
                    adapters.Add(new GpuAdapter(
                        GpuAdapter.FormatLuidToken(desc.LuidHigh, desc.LuidLow),
                        (desc.Description ?? "").Trim(),
                        software,
                        (ulong)desc.DedicatedVideoMemory,
                        new GpuPciId(desc.VendorId, desc.DeviceId, desc.SubSysId, desc.Revision),
                        // The figure Task Manager's "Shared GPU memory" total reads.
                        (ulong)desc.SharedSystemMemory,
                        // Probed here because it needs the live adapter; software adapters are dropped anyway.
                        new GpuDetails(null, software ? null : ProbeDirectX(adapter), null)));
                } finally {
                    GetMethod<ReleaseFn>(adapter, VtRelease)(adapter);
                }
            }
        } catch (Exception e) {
            Log.Warn("GpuAdapterProvider read failed", e);
        } finally {
            if (factory != IntPtr.Zero)
                GetMethod<ReleaseFn>(factory, VtRelease)(factory);
        }

        return adapters;
    }

    /// <summary>Binds a callable delegate to a COM object's vtable slot: read the object's vtable pointer,
    /// then the function pointer at <paramref name="slot"/>, then marshal it to <typeparamref name="T"/>.</summary>
    private static T GetMethod<T>(IntPtr comObject, int slot) where T : Delegate {
        var vtable = Marshal.ReadIntPtr(comObject);
        var fn = Marshal.ReadIntPtr(vtable, slot * IntPtr.Size);
        return Marshal.GetDelegateForFunctionPointer<T>(fn);
    }
}

/// <summary>The no-adapters set — what the old <c>OperatingSystem.IsWindows()</c> guard returned.</summary>
internal sealed class UnsupportedGpuAdapterProvider : IGpuAdapterProvider {
    public Task<IReadOnlyList<GpuAdapter>> GetAsync() => Task.FromResult<IReadOnlyList<GpuAdapter>>([]);
}
