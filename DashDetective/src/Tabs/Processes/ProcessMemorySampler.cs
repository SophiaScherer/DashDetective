using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace DashDetective.Tabs.Processes;

/// <summary>
/// Per-process <b>private</b> working set via the Windows PDH <c>\Process(*)\Working Set - Private</c>
/// counter — the figure Task Manager's Memory column shows. <c>Process.WorkingSet64</c> is the <i>total</i>
/// working set, which counts shared pages (DLLs and other mapped images) against every process holding
/// them, so it overstates badly: an idle Notepad reads 114 MB against Task Manager's 22.7 MB.
/// <c>Process.PrivateMemorySize64</c> is not the answer either — that is Private Bytes (committed address
/// space, 102 MB for the same Notepad), not resident private pages.
///
/// PDH reports process instances by image name, disambiguating duplicates as <c>name#1</c>, <c>name#2</c>,
/// so the instance name alone can't identify a process. The PID comes from reading
/// <c>\Process(*)\ID Process</c> in the <b>same query and the same collect</b>, where both arrays describe
/// the same instance set, and joining the two by instance name.
///
/// Static like <see cref="ProcessGpuSampler"/> (its sibling, and its sole caller's other PDH source): the
/// query is opened lazily and lives for the app's lifetime — the OS reclaims it at exit — so there is no
/// disposal to thread through the app-lifetime-singleton tab. Any failure yields an empty map and the
/// caller falls back. The platform check lives in
/// <see cref="IProcessSnapshotProvider.ForCurrentPlatform"/>.
/// </summary>
[SupportedOSPlatform("windows")]
public static class ProcessMemorySampler {
    private const uint ErrorSuccess = 0x00000000;
    private const uint PdhMoreData = 0x800007D2;
    // Both counters are formatted as doubles: a PID and a byte count are both far inside a double's exact
    // integer range, and PDH_FMT_LARGE would put a LONGLONG in the same union slot this struct reads as a
    // double, which would reinterpret the bits rather than convert them.
    private const uint PdhFmtDouble = 0x00000200;
    private const uint PdhCstatusValidData = 0x00000000;
    private const uint PdhCstatusNewData = 0x00000001;

    private const string PrivateWorkingSetPath = @"\Process(*)\Working Set - Private";
    private const string PidPath = @"\Process(*)\ID Process";

    /// <summary>A <c>PDH_FMT_COUNTERVALUE_ITEM</c>: the instance name pointer then the value union. The
    /// runtime inserts 4 bytes of padding after <see cref="CStatus"/> so the value is 8-byte aligned.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct CounterValueItem {
        public IntPtr Name;
        public uint CStatus;
        public double Value;
    }

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern uint PdhOpenQuery(string? dataSource, IntPtr userData, out IntPtr query);
    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern uint PdhAddEnglishCounter(IntPtr query, string counterPath, IntPtr userData, out IntPtr counter);
    [DllImport("pdh.dll")]
    private static extern uint PdhCollectQueryData(IntPtr query);
    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern uint PdhGetFormattedCounterArray(IntPtr counter, uint format, ref uint bufferSize, out uint itemCount, IntPtr buffer);

    private static readonly Dictionary<int, long> EmptyMap = new();

    private static IntPtr _query;
    private static IntPtr _memoryCounter;
    private static IntPtr _pidCounter;
    private static bool _initialized;
    private static bool _ready;

    /// <summary>Returns a PID → private-working-set-bytes map for the current instant. Any failure returns
    /// an empty map, and the caller keeps its own reading.</summary>
    public static IReadOnlyDictionary<int, long> Sample() {
        if (!EnsureReady() || PdhCollectQueryData(_query) != ErrorSuccess)
            return EmptyMap;

        // Both arrays come from the one collect above, so they describe the same instance set and their
        // instance names line up.
        var pidByInstance = ReadArray(_pidCounter);
        if (pidByInstance.Count == 0)
            return EmptyMap;
        var bytesByInstance = ReadArray(_memoryCounter);

        var result = new Dictionary<int, long>(pidByInstance.Count);
        foreach (var (instance, pid) in pidByInstance) {
            // PID 0 is the Idle process; "_Total" carries no PID of its own.
            if (pid <= 0 || !bytesByInstance.TryGetValue(instance, out var bytes))
                continue;
            result[(int)pid] = (long)bytes;
        }
        return result;
    }

    private static bool EnsureReady() {
        if (_initialized)
            return _ready;
        _initialized = true;

        if (!OperatingSystem.IsWindows())
            return false;
        if (PdhOpenQuery(null, IntPtr.Zero, out _query) != ErrorSuccess)
            return false;
        if (PdhAddEnglishCounter(_query, PrivateWorkingSetPath, IntPtr.Zero, out _memoryCounter) != ErrorSuccess ||
            PdhAddEnglishCounter(_query, PidPath, IntPtr.Zero, out _pidCounter) != ErrorSuccess)
            return false;

        // Both are instantaneous counters, but PDH still needs one collect before an array can be formatted.
        PdhCollectQueryData(_query);
        _ready = true;
        return true;
    }

    /// <summary>Reads one wildcard counter into an instance-name → value map. Instance names are unique
    /// within a collect (PDH suffixes duplicates <c>#1</c>, <c>#2</c>, …), so they key the join.</summary>
    private static Dictionary<string, double> ReadArray(IntPtr counter) {
        var map = new Dictionary<string, double>(StringComparer.Ordinal);

        // First call (null buffer) reports the required size via PDH_MORE_DATA; the second fills it.
        uint bufferSize = 0;
        if (PdhGetFormattedCounterArray(counter, PdhFmtDouble, ref bufferSize, out _, IntPtr.Zero) != PdhMoreData
            || bufferSize == 0)
            return map;

        var buffer = Marshal.AllocHGlobal((int)bufferSize);
        try {
            if (PdhGetFormattedCounterArray(counter, PdhFmtDouble, ref bufferSize, out var itemCount, buffer) != ErrorSuccess)
                return map;

            var itemSize = Marshal.SizeOf<CounterValueItem>();
            for (var i = 0; i < itemCount; i++) {
                var item = Marshal.PtrToStructure<CounterValueItem>(buffer + i * itemSize);
                if (item.CStatus != PdhCstatusValidData && item.CStatus != PdhCstatusNewData)
                    continue;

                var name = item.Name == IntPtr.Zero ? null : Marshal.PtrToStringUni(item.Name);
                if (string.IsNullOrEmpty(name) || name == "_Total")
                    continue;

                map[name] = item.Value;
            }
        } finally {
            Marshal.FreeHGlobal(buffer);
        }

        return map;
    }
}
