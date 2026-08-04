using DashDetective.Services.Diagnostics;
using DashDetective.Shared;
using DashDetective.Tabs.Hardware.Catalog;
using System;
using System.Collections.Generic;
using System.Runtime.Versioning;
using System.Threading.Tasks;

namespace DashDetective.Tabs.Hardware;

/// <summary>
/// Memory facts from <c>Win32_PhysicalMemory</c> (per-module capacity/speed/type/voltage) plus
/// <c>Win32_PhysicalMemoryArray.MemoryDevices</c> for the total slot count. Timings have no WMI source
/// (SPD/SMBus only), so they come from <see cref="HardwareCatalog"/> by part number. The platform check
/// lives in <see cref="IHardwareInfoProvider.ForCurrentPlatform"/>.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsMemoryModulesProvider : IMemoryModulesProvider {
    public Task<MemoryInfo> GetAsync() => Task.Run(Read);

    private static MemoryInfo Read() {
        try {
            var moduleGbs = new List<double>();
            ulong totalBytes = 0;
            int speed = 0, memoryType = 0, voltageMv = 0;
            var partNumber = "";

            WmiRead.ForEach(
                "SELECT Capacity, Speed, ConfiguredClockSpeed, SMBIOSMemoryType, ConfiguredVoltage, " +
                "PartNumber FROM Win32_PhysicalMemory",
                obj => {
                    var bytes = WmiRead.ToUInt64(obj["Capacity"]);
                    totalBytes += bytes;
                    moduleGbs.Add(bytes / (double)(1L << 30));
                    // The shared MemorySpeed rule prefers the running speed over the rated one, so this
                    // reads the same as the Dashboard's RAM line. Take the highest across modules.
                    speed = Math.Max(speed, MemorySpeed.Running(
                        WmiRead.ToInt(obj["ConfiguredClockSpeed"]), WmiRead.ToInt(obj["Speed"])));
                    if (memoryType == 0)
                        memoryType = WmiRead.ToInt(obj["SMBIOSMemoryType"]);
                    if (voltageMv == 0)
                        voltageMv = WmiRead.ToInt(obj["ConfiguredVoltage"]);
                    if (string.IsNullOrEmpty(partNumber))
                        partNumber = WmiRead.Text(obj, "PartNumber");
                });

            if (moduleGbs.Count == 0)
                return MemoryInfo.Unknown;

            var totalGb = totalBytes / (double)(1L << 30);
            var type = MemorySpecFormatter.TypeLabel(memoryType);
            var timings = HardwareCatalog.LookupMemory(partNumber)?.Timings ?? "—";

            return new MemoryInfo(
                Summary: MemorySpecFormatter.Summary(totalGb, type, speed),
                Installed: MemorySpecFormatter.Modules(moduleGbs),
                Speed: MemorySpecFormatter.Speed(speed),
                Timings: timings,
                SlotsUsed: MemorySpecFormatter.SlotsUsed(moduleGbs.Count, ReadSlotCount()),
                Voltage: MemorySpecFormatter.Voltage(voltageMv));
        } catch (Exception e) {
            Log.Warn("MemoryModulesProvider read failed", e);
            return MemoryInfo.Unknown;
        }
    }

    /// <summary>Total DIMM slots on the board from <c>Win32_PhysicalMemoryArray</c> (0 if unavailable —
    /// the slot count is best-effort, and the row falls back to the populated count alone).</summary>
    private static int ReadSlotCount() {
        try {
            var slots = 0;
            WmiRead.ForEach("SELECT MemoryDevices FROM Win32_PhysicalMemoryArray", obj => {
                if (slots == 0)
                    slots = WmiRead.ToInt(obj["MemoryDevices"]);
            });

            return slots;
        } catch (Exception e) {
            Log.Warn("MemoryModulesProvider slot-count read failed", e);
            return 0;
        }
    }
}
