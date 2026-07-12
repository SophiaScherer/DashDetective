using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.InteropServices;

namespace DashDetective.Tabs.Network;

/// <summary>One raw connection row from the OS tables: endpoints, TCP state (0 for UDP) and the
/// owning process id. Addresses/ports are already host-usable (port byte order swapped).</summary>
internal readonly record struct RawConnection(
    string Protocol, IPAddress LocalAddress, int LocalPort,
    IPAddress RemoteAddress, int RemotePort, uint State, int Pid);

/// <summary>
/// Feature-local <c>iphlpapi</c> interop for the Active Connections panel — the netstat data the
/// managed <c>IPGlobalProperties</c> API can't provide because it omits the owning PID. Follows the
/// File Explorer <c>ShellInterop</c> conventions: classic <see cref="DllImportAttribute"/>, private
/// nested sequential structs, an <see cref="OperatingSystem.IsWindows"/> guard, and soft-fail (a
/// native failure yields an empty list, never an exception).
///
/// IPv4 only for now (the OWNER_PID tables use different 16-byte-address structs for IPv6 — deferred).
/// </summary>
public static class ConnectionsInterop {
    /// <summary>All IPv4 TCP connections with owning PID and state. Empty on any failure.</summary>
    internal static IReadOnlyList<RawConnection> GetTcp() {
        var rows = new List<RawConnection>();
        if (!OperatingSystem.IsWindows())
            return rows;

        var table = IntPtr.Zero;
        try {
            var size = 0;
            // Size-probe: expect ERROR_INSUFFICIENT_BUFFER and a filled-in size. Retry the real call
            // a couple of times in case the table grows between sizing and reading.
            GetExtendedTcpTable(IntPtr.Zero, ref size, false, AF_INET, TCP_TABLE_OWNER_PID_ALL, 0);
            uint ret = ERROR_INSUFFICIENT_BUFFER;
            for (var attempt = 0; attempt < 3 && ret == ERROR_INSUFFICIENT_BUFFER; attempt++) {
                table = Marshal.AllocHGlobal(size);
                ret = GetExtendedTcpTable(table, ref size, false, AF_INET, TCP_TABLE_OWNER_PID_ALL, 0);
                if (ret == NO_ERROR)
                    break;
                Marshal.FreeHGlobal(table);
                table = IntPtr.Zero;
            }
            if (table == IntPtr.Zero || ret != NO_ERROR)
                return rows;

            var count = Marshal.ReadInt32(table);
            var rowSize = Marshal.SizeOf<MIB_TCPROW_OWNER_PID>();
            var ptr = IntPtr.Add(table, 4); // skip the leading dwNumEntries DWORD
            for (var i = 0; i < count; i++) {
                var row = Marshal.PtrToStructure<MIB_TCPROW_OWNER_PID>(ptr);
                rows.Add(new RawConnection(
                    "TCP",
                    new IPAddress(row.localAddr), NetworkPort(row.localPort),
                    new IPAddress(row.remoteAddr), NetworkPort(row.remotePort),
                    row.state, (int)row.owningPid));
                ptr = IntPtr.Add(ptr, rowSize);
            }
        } catch {
            rows.Clear();
        } finally {
            if (table != IntPtr.Zero)
                Marshal.FreeHGlobal(table);
        }
        return rows;
    }

    /// <summary>All IPv4 UDP listeners with owning PID (UDP is connectionless — no remote/state).
    /// Empty on any failure.</summary>
    internal static IReadOnlyList<RawConnection> GetUdp() {
        var rows = new List<RawConnection>();
        if (!OperatingSystem.IsWindows())
            return rows;

        var table = IntPtr.Zero;
        try {
            var size = 0;
            GetExtendedUdpTable(IntPtr.Zero, ref size, false, AF_INET, UDP_TABLE_OWNER_PID, 0);
            uint ret = ERROR_INSUFFICIENT_BUFFER;
            for (var attempt = 0; attempt < 3 && ret == ERROR_INSUFFICIENT_BUFFER; attempt++) {
                table = Marshal.AllocHGlobal(size);
                ret = GetExtendedUdpTable(table, ref size, false, AF_INET, UDP_TABLE_OWNER_PID, 0);
                if (ret == NO_ERROR)
                    break;
                Marshal.FreeHGlobal(table);
                table = IntPtr.Zero;
            }
            if (table == IntPtr.Zero || ret != NO_ERROR)
                return rows;

            var count = Marshal.ReadInt32(table);
            var rowSize = Marshal.SizeOf<MIB_UDPROW_OWNER_PID>();
            var ptr = IntPtr.Add(table, 4);
            for (var i = 0; i < count; i++) {
                var row = Marshal.PtrToStructure<MIB_UDPROW_OWNER_PID>(ptr);
                rows.Add(new RawConnection(
                    "UDP",
                    new IPAddress(row.localAddr), NetworkPort(row.localPort),
                    IPAddress.Any, 0, 0, (int)row.owningPid));
                ptr = IntPtr.Add(ptr, rowSize);
            }
        } catch {
            rows.Clear();
        } finally {
            if (table != IntPtr.Zero)
                Marshal.FreeHGlobal(table);
        }
        return rows;
    }

    /// <summary>The port DWORDs store the port in the low 16 bits in NETWORK (big-endian) order, so
    /// they must be byte-swapped or e.g. 443 reads as 47873.</summary>
    private static int NetworkPort(uint value) =>
        ((int)(value & 0xFF) << 8) | (int)((value >> 8) & 0xFF);

    private const int AF_INET = 2;
    private const int TCP_TABLE_OWNER_PID_ALL = 5;
    private const int UDP_TABLE_OWNER_PID = 1;
    private const uint NO_ERROR = 0;
    private const uint ERROR_INSUFFICIENT_BUFFER = 122;

    [StructLayout(LayoutKind.Sequential)]
    private struct MIB_TCPROW_OWNER_PID {
        public uint state;
        public uint localAddr;
        public uint localPort;
        public uint remoteAddr;
        public uint remotePort;
        public uint owningPid;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MIB_UDPROW_OWNER_PID {
        public uint localAddr;
        public uint localPort;
        public uint owningPid;
    }

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedTcpTable(IntPtr pTcpTable, ref int dwOutBufLen,
        bool sort, int ipVersion, int tblClass, uint reserved);

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedUdpTable(IntPtr pUdpTable, ref int dwOutBufLen,
        bool sort, int ipVersion, int tblClass, uint reserved);
}
