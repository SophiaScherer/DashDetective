using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace DashDetective.Tabs.Performance;

/// <summary>
/// Raw interop over <c>nvapi64.dll</c> — NVIDIA's display-driver API, used here only to read a GPU's
/// temperature. It ships with every NVIDIA driver, so there is no package, no redistributable and no admin
/// requirement.
///
/// NVAPI has an unusual shape: the DLL exports exactly one useful symbol, <c>nvapi_QueryInterface</c>, which
/// maps a function id to a function pointer. Every call is therefore resolved by id and bound with
/// <see cref="Marshal.GetDelegateForFunctionPointer"/> — the same technique
/// <see cref="Services.SystemMetrics.GpuAdapterProvider"/> uses for DXGI's vtable, needing no <c>unsafe</c>
/// and no csproj change. The ids below are from NVIDIA's published <c>nvapi_interface.h</c>; the delegates are
/// resolved once and cached, since a per-tick lookup would be pure overhead.
///
/// Every method soft-fails (<c>false</c>/<c>null</c>/empty) rather than throwing — including when the DLL is
/// absent entirely, which is the normal case on a machine with no NVIDIA GPU.
/// </summary>
internal static class NvApiInterop {
    // Function ids from nvapi_interface.h.
    private const uint IdInitialize = 0x0150E828;
    private const uint IdUnload = 0xD22BDD7E;
    private const uint IdEnumPhysicalGpus = 0xE5AC921F;
    private const uint IdGetPciIdentifiers = 0x2DDFB66E;
    private const uint IdGetThermalSettings = 0xE3640A56;

    private const int NvApiOk = 0;
    private const int MaxPhysicalGpus = 64;

    /// <summary>NVAPI_THERMAL_TARGET_ALL — ask for every sensor rather than one by index.</summary>
    private const uint ThermalTargetAll = 15;

    /// <summary>NVAPI_MAX_THERMAL_SENSORS_PER_GPU.</summary>
    internal const int MaxThermalSensors = 3;

    private static EnumPhysicalGpusFn? _enumPhysicalGpus;
    private static GetPciIdentifiersFn? _getPciIdentifiers;
    private static GetThermalSettingsFn? _getThermalSettings;

    /// <summary>Initializes NVAPI and resolves the calls used here. Returns <c>false</c> when the DLL is
    /// missing, initialization fails, or the driver is too old to expose one of the ids.</summary>
    internal static bool Initialize() {
        if (!OperatingSystem.IsWindows())
            return false;

        try {
            var initialize = Resolve<InitializeFn>(IdInitialize);
            if (initialize is null || initialize() != NvApiOk)
                return false;

            _enumPhysicalGpus = Resolve<EnumPhysicalGpusFn>(IdEnumPhysicalGpus);
            _getPciIdentifiers = Resolve<GetPciIdentifiersFn>(IdGetPciIdentifiers);
            _getThermalSettings = Resolve<GetThermalSettingsFn>(IdGetThermalSettings);
            return _enumPhysicalGpus is not null && _getPciIdentifiers is not null && _getThermalSettings is not null;
        } catch (DllNotFoundException) {
            return false;   // no NVIDIA driver on this machine — the expected case, not an error
        } catch (EntryPointNotFoundException) {
            return false;
        }
    }

    /// <summary>Every physical NVIDIA GPU's opaque handle, valid until <see cref="Unload"/>.</summary>
    internal static IReadOnlyList<IntPtr> EnumPhysicalGpus() {
        if (_enumPhysicalGpus is null)
            return [];

        var handles = new IntPtr[MaxPhysicalGpus];
        if (_enumPhysicalGpus(handles, out var count) != NvApiOk)
            return [];

        var result = new List<IntPtr>((int)count);
        for (var i = 0; i < count && i < handles.Length; i++)
            if (handles[i] != IntPtr.Zero)
                result.Add(handles[i]);
        return result;
    }

    /// <summary>A GPU's PCI identity, or <c>null</c> when the driver won't report it. The device id comes back
    /// already packed as <c>(device &lt;&lt; 16) | vendor</c>, which is what <see cref="VendorPciId"/> holds.</summary>
    internal static VendorPciId? ReadPciId(IntPtr gpu) {
        if (_getPciIdentifiers is null)
            return null;

        return _getPciIdentifiers(gpu, out var deviceId, out var subSystemId, out var revisionId, out _) == NvApiOk
            ? new VendorPciId(deviceId, subSystemId, revisionId)
            : null;
    }

    /// <summary>Reads every thermal sensor on a GPU into parallel target/temperature arrays (both
    /// <see cref="MaxThermalSensors"/> long), returning how many the driver actually filled, or <c>0</c> on
    /// failure. Plain arrays rather than the interop struct, so the sensor-selection logic that consumes them
    /// stays free of marshalling and is unit-testable.</summary>
    internal static int ReadThermalSensors(IntPtr gpu, int[] targets, int[] temperatures) {
        if (_getThermalSettings is null)
            return 0;

        var settings = new ThermalSettingsV2 {
            Version = ThermalSettingsVersion,
            Sensors = new ThermalSensor[MaxThermalSensors],
        };
        if (_getThermalSettings(gpu, ThermalTargetAll, ref settings) != NvApiOk || settings.Sensors is null)
            return 0;

        var count = (int)Math.Min(settings.Count, MaxThermalSensors);
        for (var i = 0; i < count; i++) {
            targets[i] = settings.Sensors[i].Target;
            temperatures[i] = settings.Sensors[i].CurrentTemp;
        }
        return count;
    }

    /// <summary>Releases NVAPI. The GPU handles handed out above are invalid afterwards.</summary>
    internal static void Unload() {
        try {
            Resolve<UnloadFn>(IdUnload)?.Invoke();
        } catch (DllNotFoundException) {
            // Nothing to unload.
        } finally {
            _enumPhysicalGpus = null;
            _getPciIdentifiers = null;
            _getThermalSettings = null;
        }
    }

    private static T? Resolve<T>(uint id) where T : Delegate {
        var fn = QueryInterface(id);
        return fn == IntPtr.Zero ? null : Marshal.GetDelegateForFunctionPointer<T>(fn);
    }

    // ---- Native surface ----

    [DllImport("nvapi64.dll", EntryPoint = "nvapi_QueryInterface", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr QueryInterface(uint id);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int InitializeFn();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int UnloadFn();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int EnumPhysicalGpusFn([Out] IntPtr[] handles, out uint count);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GetPciIdentifiersFn(
        IntPtr gpu, out uint deviceId, out uint subSystemId, out uint revisionId, out uint extDeviceId);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GetThermalSettingsFn(IntPtr gpu, uint sensorIndex, ref ThermalSettingsV2 settings);

    /// <summary>NVAPI encodes a struct's version as its size OR'd with the version number in the high word.
    /// <c>NV_GPU_THERMAL_SETTINGS_V2</c> is 68 bytes, giving 0x00020044; a wrong value is rejected with
    /// NVAPI_INCOMPATIBLE_STRUCT_VERSION.</summary>
    private static readonly uint ThermalSettingsVersion =
        (uint)(Marshal.SizeOf<ThermalSettingsV2>() | (2 << 16));

    /// <summary>Layout of <c>NV_GPU_THERMAL_SETTINGS_V2</c>: two counters then a fixed three-element sensor
    /// array, 68 bytes in total. All fields are 4-byte, so no padding is inserted.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct ThermalSettingsV2 {
        public uint Version;
        public uint Count;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = MaxThermalSensors)]
        public ThermalSensor[] Sensors;
    }

    /// <summary>One entry of <c>NV_GPU_THERMAL_SETTINGS_V2.sensor</c>. The temperatures are signed in V2 (they
    /// were unsigned in V1), and <c>Target</c> names what the sensor measures — GPU, memory, board, …</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct ThermalSensor {
        public int Controller;
        public int DefaultMinTemp;
        public int DefaultMaxTemp;
        public int CurrentTemp;
        public int Target;
    }
}
