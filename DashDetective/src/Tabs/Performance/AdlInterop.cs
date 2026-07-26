using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace DashDetective.Tabs.Performance;

/// <summary>
/// Raw interop over <c>atiadlxx.dll</c> — AMD's Display Library, used here only to read a GPU's temperature.
/// It ships with every AMD driver, so there is no package, no redistributable and no admin requirement.
///
/// ADL is used in preference to the newer ADLX because its exports are flat C functions, needing nothing more
/// than <c>[DllImport]</c>; ADLX's C API is interface/vtable-based and considerably heavier for one reading.
///
/// Two ADL quirks drive the shape of this file. It requires the caller to supply a memory-allocation
/// <b>callback</b>, which must be kept alive for as long as the context (a collected delegate would crash the
/// process). And its <c>AdapterInfo.iVendorID</c> is <b>not usable</b> — it reports 0x03EA for AMD and 0x000A
/// for NVIDIA, evidently a broken parse of the hex vendor string — so adapters are identified from the PNP
/// device string instead (see <see cref="PnpPciParser"/>). ADL also enumerates non-AMD adapters, and lists one
/// physical GPU once per display output, so callers must filter and de-duplicate.
///
/// Every method soft-fails rather than throwing, including when the DLL is absent.
/// </summary>
internal static class AdlInterop {
    private const int AdlOk = 0;

    /// <summary>ADL_MAX_PATH — the fixed width of every string field in <c>AdapterInfo</c>.</summary>
    private const int MaxPath = 256;

    /// <summary>ADL_PMLOG_MAX_SENSORS — the fixed length of the PMLOG sensor array.</summary>
    internal const int MaxSensors = 256;

    // The allocation callback must outlive the context, so it is rooted in a static field rather than being
    // passed as a temporary.
    private static readonly MallocFn AllocCallback = size => Marshal.AllocHGlobal(size);

    private static IntPtr _context;

    /// <summary>Creates the ADL context. Returns <c>false</c> when the DLL is missing or ADL refuses.</summary>
    internal static bool Initialize() {
        if (!OperatingSystem.IsWindows())
            return false;

        try {
            // 1 = enumerate only adapters that are physically present.
            if (ADL2_Main_Control_Create(AllocCallback, 1, out _context) != AdlOk)
                return false;
            return _context != IntPtr.Zero;
        } catch (DllNotFoundException) {
            return false;   // no AMD driver on this machine — the expected case, not an error
        } catch (EntryPointNotFoundException) {
            return false;
        }
    }

    /// <summary>Every adapter ADL knows about, as (its ADL index, its PNP device string). Includes non-AMD
    /// adapters and repeats one GPU per display output — the caller filters and de-duplicates.</summary>
    internal static IReadOnlyList<(int Index, string PnpString)> EnumAdapters() {
        if (_context == IntPtr.Zero)
            return [];

        try {
            if (ADL2_Adapter_NumberOfAdapters_Get(_context, out var count) != AdlOk || count <= 0)
                return [];

            var stride = Marshal.SizeOf<AdapterInfo>();
            var buffer = Marshal.AllocHGlobal(stride * count);
            try {
                if (ADL2_Adapter_AdapterInfo_Get(_context, buffer, stride * count) != AdlOk)
                    return [];

                var adapters = new List<(int, string)>(count);
                for (var i = 0; i < count; i++) {
                    var info = Marshal.PtrToStructure<AdapterInfo>(buffer + i * stride);
                    adapters.Add((info.AdapterIndex, info.PnpString ?? ""));
                }
                return adapters;
            } finally {
                Marshal.FreeHGlobal(buffer);
            }
        } catch (EntryPointNotFoundException) {
            return [];
        }
    }

    /// <summary>The adapter's <c>ADL_ASIC_*</c> family-type bits, or <c>null</c> when ADL won't say — which
    /// includes every non-AMD adapter, since ADL returns an error for those. Callers treat "won't say" as
    /// "not discrete". Interpreted by <see cref="AmdGpuSensorReader.IsDiscrete"/>.</summary>
    internal static int? ReadAsicFamilyType(int adapterIndex) {
        if (_context == IntPtr.Zero)
            return null;

        try {
            return ADL2_Adapter_ASICFamilyType_Get(_context, adapterIndex, out var asicTypes, out _) == AdlOk
                ? asicTypes
                : null;
        } catch (EntryPointNotFoundException) {
            return null;
        }
    }

    /// <summary>Fills <paramref name="supported"/> / <paramref name="values"/> (both
    /// <see cref="MaxSensors"/> long) from ADL's PMLOG snapshot for one adapter. Plain arrays rather than the
    /// interop struct, so the sensor-selection logic that consumes them stays free of marshalling and is
    /// unit-testable.</summary>
    internal static bool ReadSensors(int adapterIndex, int[] supported, int[] values) {
        if (_context == IntPtr.Zero)
            return false;

        try {
            var data = new PMLogDataOutput { Sensors = new SingleSensorData[MaxSensors] };
            if (ADL2_New_QueryPMLogData_Get(_context, adapterIndex, ref data) != AdlOk || data.Sensors is null)
                return false;

            for (var i = 0; i < MaxSensors; i++) {
                supported[i] = data.Sensors[i].Supported;
                values[i] = data.Sensors[i].Value;
            }
            return true;
        } catch (EntryPointNotFoundException) {
            return false;
        }
    }

    internal static void Shutdown() {
        if (_context == IntPtr.Zero)
            return;

        try {
            ADL2_Main_Control_Destroy(_context);
        } catch (DllNotFoundException) {
            // Never created.
        } catch (EntryPointNotFoundException) {
            // Never created.
        } finally {
            _context = IntPtr.Zero;
        }
    }

    // ---- Native surface. ADL's own exports keep their C names, hence the casing. ----

    /// <summary>ADL_MAIN_MALLOC_CALLBACK — ADL allocates its adapter arrays through this.</summary>
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate IntPtr MallocFn(int size);

    [DllImport("atiadlxx.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int ADL2_Main_Control_Create(MallocFn callback, int enumConnectedAdapters, out IntPtr context);

    [DllImport("atiadlxx.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int ADL2_Main_Control_Destroy(IntPtr context);

    [DllImport("atiadlxx.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int ADL2_Adapter_NumberOfAdapters_Get(IntPtr context, out int count);

    [DllImport("atiadlxx.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int ADL2_Adapter_AdapterInfo_Get(IntPtr context, IntPtr info, int inputSize);

    [DllImport("atiadlxx.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int ADL2_New_QueryPMLogData_Get(IntPtr context, int adapterIndex, ref PMLogDataOutput data);

    [DllImport("atiadlxx.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int ADL2_Adapter_ASICFamilyType_Get(IntPtr context, int adapterIndex, out int asicTypes, out int valids);

    /// <summary>Layout of ADL's <c>AdapterInfo</c>: 1572 bytes, six fixed 256-byte ANSI string fields
    /// interleaved with 4-byte ints, so no padding is inserted.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct AdapterInfo {
        public int Size;
        public int AdapterIndex;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MaxPath)] public string Udid;
        public int BusNumber;
        public int DeviceNumber;
        public int FunctionNumber;
        public int VendorId;    // unusable — see the class remarks
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MaxPath)] public string AdapterName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MaxPath)] public string DisplayName;
        public int Present;
        public int Exist;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MaxPath)] public string DriverPath;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MaxPath)] public string DriverPathExt;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MaxPath)] public string PnpString;
        public int OsDisplayIndex;
    }

    /// <summary>One entry of ADL's PMLOG snapshot: whether the sensor exists on this board, and its value.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct SingleSensorData {
        public int Supported;
        public int Value;
    }

    /// <summary>Layout of <c>ADLPMLogDataOutput</c>: a size field then a fixed 256-entry sensor array,
    /// 2052 bytes. Indexed by <c>ADLSensorType</c>, so a sensor's meaning is its position.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct PMLogDataOutput {
        public int Size;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = MaxSensors)] public SingleSensorData[] Sensors;
    }
}
