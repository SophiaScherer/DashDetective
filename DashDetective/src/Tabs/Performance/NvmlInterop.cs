using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace DashDetective.Tabs.Performance;

/// <summary>
/// Raw interop over <c>nvml.dll</c> — NVIDIA's management library, used here only to read a GPU's power draw.
/// Like NVAPI it ships with the display driver (it is what <c>nvidia-smi</c> is built on), so there is no
/// package, no redistributable and no admin requirement.
///
/// Power is read here rather than through NVAPI deliberately: NVAPI's power call is not part of NVIDIA's
/// published interface table, whereas <c>nvmlDeviceGetPowerUsage</c> is documented and supported. NVML also
/// has none of NVAPI's function-id indirection — these are ordinary flat exports, so plain
/// <c>[DllImport]</c> is enough.
///
/// Every method soft-fails rather than throwing, including when the DLL is absent.
/// </summary>
internal static class NvmlInterop {
    private const int NvmlSuccess = 0;

    /// <summary>Initializes NVML. Costs roughly 10 ms, so it is done once, lazily.</summary>
    internal static bool Initialize() {
        if (!OperatingSystem.IsWindows())
            return false;

        try {
            return nvmlInit_v2() == NvmlSuccess;
        } catch (DllNotFoundException) {
            return false;   // no NVIDIA driver on this machine
        } catch (EntryPointNotFoundException) {
            return false;
        }
    }

    /// <summary>Every NVML device handle, in NVML's own enumeration order.</summary>
    internal static IReadOnlyList<IntPtr> EnumDevices() {
        try {
            if (nvmlDeviceGetCount_v2(out var count) != NvmlSuccess)
                return [];

            var devices = new List<IntPtr>((int)count);
            for (uint i = 0; i < count; i++)
                if (nvmlDeviceGetHandleByIndex_v2(i, out var device) == NvmlSuccess && device != IntPtr.Zero)
                    devices.Add(device);
            return devices;
        } catch (EntryPointNotFoundException) {
            return [];
        }
    }

    /// <summary>A device's PCI identity, or <c>null</c> when NVML won't report it. As with NVAPI the device id
    /// arrives already packed as <c>(device &lt;&lt; 16) | vendor</c>. NVML reports no revision, so that field
    /// is left zero — <see cref="GpuPciMatcher"/> treats a zero as "not reported" and matches without it.</summary>
    internal static VendorPciId? ReadPciId(IntPtr device) {
        try {
            var info = new PciInfo();
            return nvmlDeviceGetPciInfo_v3(device, ref info) == NvmlSuccess
                ? new VendorPciId(info.PciDeviceId, info.PciSubSystemId, 0)
                : null;
        } catch (EntryPointNotFoundException) {
            return null;
        }
    }

    /// <summary>A device's current power draw in milliwatts, or <c>null</c> when unsupported — which some
    /// consumer boards genuinely are, so temperature must stay usable without it.</summary>
    internal static uint? ReadPowerMilliwatts(IntPtr device) {
        try {
            return nvmlDeviceGetPowerUsage(device, out var milliwatts) == NvmlSuccess ? milliwatts : null;
        } catch (EntryPointNotFoundException) {
            return null;
        }
    }

    internal static void Shutdown() {
        try {
            nvmlShutdown();
        } catch (DllNotFoundException) {
            // Never initialized.
        } catch (EntryPointNotFoundException) {
            // Never initialized.
        }
    }

    // ---- Native surface (NVML's exports keep their C names, hence the casing) ----

    [DllImport("nvml.dll")] private static extern int nvmlInit_v2();
    [DllImport("nvml.dll")] private static extern int nvmlShutdown();
    [DllImport("nvml.dll")] private static extern int nvmlDeviceGetCount_v2(out uint count);
    [DllImport("nvml.dll")] private static extern int nvmlDeviceGetHandleByIndex_v2(uint index, out IntPtr device);
    [DllImport("nvml.dll")] private static extern int nvmlDeviceGetPciInfo_v3(IntPtr device, ref PciInfo info);
    [DllImport("nvml.dll")] private static extern int nvmlDeviceGetPowerUsage(IntPtr device, out uint milliwatts);

    /// <summary>Layout of <c>nvmlPciInfo_t</c> as <c>nvmlDeviceGetPciInfo_v3</c> fills it: a legacy bus-id
    /// string, five 32-bit ids, then the current bus-id string — 68 bytes. The two fixed char buffers are
    /// ANSI, so <c>ByValTStr</c> with <c>CharSet.Ansi</c> sizes them correctly.</summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private struct PciInfo {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)]
        public string BusIdLegacy;
        public uint Domain;
        public uint Bus;
        public uint Device;
        public uint PciDeviceId;
        public uint PciSubSystemId;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string BusId;
    }
}
