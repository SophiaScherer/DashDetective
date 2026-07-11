using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using DashDetective.Services.Network;

namespace DashDetective.Tabs.Network;

/// <summary>The Adapters + IP Configuration snapshot: every adapter (for the list) plus the primary
/// adapter's IPv4 configuration (for the IP panel).</summary>
public sealed record AdapterSnapshot(IReadOnlyList<AdapterInfo> Adapters, IpConfigInfo PrimaryConfig);

/// <summary>
/// Reads the machine's network adapters and the primary adapter's IPv4 configuration via the managed
/// <see cref="NetworkInterface"/> API. Enumeration is a cheap stateless snapshot (unlike the
/// throughput sampler's stateful byte-counter differencing), but it is still done off the UI thread
/// to match the app's provider convention. Follows <c>SystemInfoProvider</c>: a static
/// <see cref="GetAsync"/> that never throws — each adapter and each field falls back independently so
/// one dead source can't blank the panel.
///
/// The primary adapter is identified with <see cref="NetworkUsageSampler.SelectPrimary"/> so the
/// "which adapter is internet-facing" logic lives in exactly one place (shared with the sampler).
/// </summary>
public static class AdapterInfoProvider {
    // Name/description fragments that mark an adapter as virtual/host rather than a physical NIC.
    private static readonly string[] VirtualMarkers = {
        "virtual", "hyper-v", "vethernet", "vmware", "virtualbox", "tap-", "tap ", "loopback",
        "pseudo", "wan miniport", "bluetooth", "wintun", "wireguard", "npcap", "docker",
    };

    public static Task<AdapterSnapshot> GetAsync() => Task.Run(Read);

    private static AdapterSnapshot Read() {
        try {
            var primary = NetworkUsageSampler.SelectPrimary();
            var primaryId = primary?.Id;

            var adapters = new List<(AdapterInfo Info, int SortRank, string Name)>();
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces()) {
                // Loopback is never interesting to show; everything else (including virtual) is listed.
                if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    continue;

                try {
                    var info = Describe(nic);
                    var rank = nic.Id == primaryId ? 0 : info.Kind switch {
                        AdapterKind.Connected => 1,
                        AdapterKind.Virtual => 2,
                        _ => 3,
                    };
                    adapters.Add((info, rank, info.Name));
                } catch {
                    // A single unreadable adapter shouldn't blank the whole list.
                }
            }

            var ordered = adapters
                .OrderBy(a => a.SortRank)
                .ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
                .Select(a => a.Info)
                .ToList();

            var config = primary is not null ? ReadIpConfig(primary) : IpConfigInfo.Unknown;
            return new AdapterSnapshot(ordered, config);
        } catch {
            return new AdapterSnapshot(Array.Empty<AdapterInfo>(), IpConfigInfo.Unknown);
        }
    }

    private static AdapterInfo Describe(NetworkInterface nic) {
        var isUp = nic.OperationalStatus == OperationalStatus.Up;
        var isVirtual = IsVirtual(nic);
        var kind = !isUp ? AdapterKind.Disconnected
            : isVirtual ? AdapterKind.Virtual
            : AdapterKind.Connected;

        var status = isUp ? "Connected" : "Disconnected";
        var speed = FormatSpeed(nic, isUp);
        return new AdapterInfo(nic.Name, nic.Description, status, speed, kind);
    }

    private static bool IsVirtual(NetworkInterface nic) {
        var haystack = $"{nic.Name} {nic.Description}".ToLowerInvariant();
        return VirtualMarkers.Any(m => haystack.Contains(m, StringComparison.Ordinal));
    }

    /// <summary>Formats link speed as Gbps/Mbps. <see cref="NetworkInterface.Speed"/> returns −1 (or 0)
    /// when unknown — and a down adapter has no meaningful speed — so both render as "—".</summary>
    private static string FormatSpeed(NetworkInterface nic, bool isUp) {
        long bps;
        try {
            bps = nic.Speed;
        } catch {
            return "—";
        }

        if (!isUp || bps <= 0)
            return "—";

        if (bps >= 1_000_000_000L) {
            var gbps = bps / 1_000_000_000.0;
            return $"{gbps.ToString("0.#", CultureInfo.InvariantCulture)} Gbps";
        }
        var mbps = bps / 1_000_000.0;
        return $"{mbps.ToString("0.#", CultureInfo.InvariantCulture)} Mbps";
    }

    private static IpConfigInfo ReadIpConfig(NetworkInterface nic) {
        try {
            var props = nic.GetIPProperties();

            var unicast = props.UnicastAddresses
                .FirstOrDefault(u => u.Address.AddressFamily == AddressFamily.InterNetwork);
            var ipv4 = unicast?.Address.ToString() ?? "—";
            var mask = unicast?.IPv4Mask?.ToString() ?? "—";

            var gateway = props.GatewayAddresses
                .Select(g => g.Address)
                .FirstOrDefault(a => a is not null && a.AddressFamily == AddressFamily.InterNetwork)
                ?.ToString() ?? "—";

            var dnsList = props.DnsAddresses
                .Where(a => a.AddressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6)
                .Take(2)
                .Select(a => a.ToString())
                .ToList();
            var dns = dnsList.Count > 0 ? string.Join(", ", dnsList) : "—";

            var mac = FormatMac(nic.GetPhysicalAddress());

            var dhcp = "—";
            try {
                // IsDhcpEnabled is Windows-only; the guard both satisfies the platform analyzer and
                // matches SystemInfoProvider's convention (the app is a Windows desktop app).
                if (OperatingSystem.IsWindows())
                    dhcp = props.GetIPv4Properties()?.IsDhcpEnabled == true ? "Enabled" : "Disabled";
            } catch {
                // Some adapters have no IPv4 properties; leave DHCP unknown.
            }

            return new IpConfigInfo(ipv4, mask, gateway, dns, mac, dhcp);
        } catch {
            return IpConfigInfo.Unknown;
        }
    }

    private static string FormatMac(PhysicalAddress mac) {
        var bytes = mac.GetAddressBytes();
        if (bytes.Length == 0)
            return "—";
        return string.Join("-", bytes.Select(b => b.ToString("X2", CultureInfo.InvariantCulture)));
    }
}
