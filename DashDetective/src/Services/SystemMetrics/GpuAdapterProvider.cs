using DashDetective.Services.Diagnostics;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace DashDetective.Services.SystemMetrics;

/// <summary>One graphics adapter as DXGI reports it: the PDH-style <see cref="LuidToken"/> that joins to
/// the <c>\GPU Engine(*)</c> counter instances, the friendly <see cref="Name"/> (DXGI's description),
/// whether it is a software/basic-render adapter (<see cref="IsSoftware"/>, to be filtered out), and its
/// dedicated VRAM in bytes (unused for now — VRAM display is deferred).</summary>
public sealed record GpuAdapter(string LuidToken, string Name, bool IsSoftware, ulong DedicatedVideoMemory);

/// <summary>
/// Enumerates the machine's graphics adapters via DXGI (<c>dxgi.dll</c>: <c>CreateDXGIFactory1</c> →
/// <c>EnumAdapters1</c> → <c>GetDesc1</c>) — the authoritative LUID→name map. Unlike WMI's
/// <c>Win32_VideoController</c> (used by <see cref="DashDetective.Tabs.Dashboard.GpuInfoProvider"/>), DXGI
/// exposes each adapter's <b>LUID</b> (so per-GPU PDH counters can be attributed to a name) and a software
/// flag (so the Microsoft Basic Render Driver and similar are dropped). Each <see cref="GpuAdapter.LuidToken"/>
/// is formatted to match the PDH instance-name token (<c>luid_0x{High:x8}_0x{Low:x8}</c>).
///
/// COM is reached via <b>raw vtable function pointers</b> (<see cref="Marshal.GetDelegateForFunctionPointer"/>),
/// not <c>[ComImport]</c> RCWs: built-in COM is disabled by a runtime feature switch, so an RCW throws
/// "Built-in COM has been disabled". This needs no <c>unsafe</c> and no csproj change. Soft-fails to an empty
/// list on any failure (or a non-Windows host), like the other providers.
/// </summary>
public static class GpuAdapterProvider {
    public static Task<IReadOnlyList<GpuAdapter>> GetAsync() => Task.Run(Read);

    /// <summary>Formats a LUID's high/low parts into the PDH instance-name token
    /// (<c>luid_0x00000000_0x0000e54b</c>) — lowercase, eight hex digits each — so it joins directly
    /// against the <c>\GPU Engine(*)</c> counter instances. Pure; unit-tested.</summary>
    public static string FormatLuidToken(int high, uint low) =>
        string.Create(CultureInfo.InvariantCulture, $"luid_0x{(uint)high:x8}_0x{low:x8}");

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
        var adapters = new List<GpuAdapter>();
        if (!OperatingSystem.IsWindows())
            return adapters;

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

                    adapters.Add(new GpuAdapter(
                        FormatLuidToken(desc.LuidHigh, desc.LuidLow),
                        (desc.Description ?? "").Trim(),
                        (desc.Flags & DxgiAdapterFlagSoftware) != 0,
                        (ulong)desc.DedicatedVideoMemory));
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
