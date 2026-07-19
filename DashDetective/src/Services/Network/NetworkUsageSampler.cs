using DashDetective.Services.Diagnostics;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;

namespace DashDetective.Services.Network;

/// <summary>
/// A single network-throughput snapshot: download and upload rates in megabits per second (Mbps).
/// </summary>
public readonly record struct NetworkSample(double DownMbps, double UpMbps);

/// <summary>
/// Samples network throughput via the managed <see cref="NetworkInterface"/> API. The OS exposes
/// cumulative byte counters per adapter; each <see cref="Sample"/> call differences the primary
/// adapter's totals over the elapsed wall-clock interval to derive a rate.
///
/// It deliberately samples a SINGLE adapter — the internet-facing one (a real default gateway,
/// busiest among those) — rather than summing all adapters. On .NET,
/// <see cref="NetworkInterface.GetAllNetworkInterfaces"/> returns many virtual / filter / phantom
/// adapters (Hyper-V, VirtualBox, WFP, …) that mirror the physical NIC's counters, so summing them
/// multi-counts the same traffic (observed ~8× inflation). A single primary adapter matches what
/// Task Manager reports per connection. No native dependencies; fails soft to zero.
///
/// Lives under <c>src/Services/Network</c> (not a tab folder) because it is shared: the Dashboard's
/// throughput surfaces and the Network tab both sample through it, and the Network tab's
/// adapter/IP provider reuses <see cref="SelectPrimary"/> to identify the primary adapter — so the
/// adapter-filtering / primary-selection logic lives in exactly one place.
/// </summary>
public sealed class NetworkUsageSampler {
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private long _prevBytesReceived;
    private long _prevBytesSent;
    private double _prevElapsedSeconds;
    private string? _primaryId;

    /// <summary>Friendly name of the adapter currently being sampled, for the throughput caption.</summary>
    public string AdapterName { get; private set; } = string.Empty;

    public NetworkUsageSampler() {
        // Seed the baseline from the primary adapter so the first Sample() reflects a real interval
        // rather than the whole time since boot.
        try {
            var primary = SelectPrimary();
            if (primary is not null)
                Rebaseline(primary);
            else
                _prevElapsedSeconds = _clock.Elapsed.TotalSeconds;
        } catch (System.Exception e) {
            // Leave baselines at zero; subsequent samples self-correct.
            Log.Warn("NetworkUsageSampler baseline failed", e);
        }
    }

    /// <summary>
    /// Returns download/upload rates (Mbps) since the previous call for the primary adapter. Rate is
    /// delta-bytes over the elapsed interval measured by a <see cref="Stopwatch"/> (the caller's timer
    /// is not exact), converted bytes/s → Mbps as <c>bytes * 8 / 1_000_000</c>.
    /// </summary>
    public NetworkSample Sample() {
        try {
            // Follow the same adapter across ticks; if it vanished, re-pick the current primary.
            var primary = FindById(_primaryId) ?? SelectPrimary();
            if (primary is null)
                return new NetworkSample(0, 0);

            // The primary changed (adapter added/removed, connection switched): its counters aren't
            // comparable to the old baseline, so rebaseline and report no rate for this tick.
            if (primary.Id != _primaryId) {
                Rebaseline(primary);
                return new NetworkSample(0, 0);
            }

            var stats = primary.GetIPStatistics();
            var received = stats.BytesReceived;
            var sent = stats.BytesSent;

            var now = _clock.Elapsed.TotalSeconds;
            var seconds = now - _prevElapsedSeconds;

            // Clamp negatives to guard against a counter reset between samples.
            var downBytes = received - _prevBytesReceived;
            var upBytes = sent - _prevBytesSent;

            _prevBytesReceived = received;
            _prevBytesSent = sent;
            _prevElapsedSeconds = now;

            if (seconds <= 0)
                return new NetworkSample(0, 0);

            var down = downBytes > 0 ? downBytes * 8.0 / 1_000_000.0 / seconds : 0;
            var up = upBytes > 0 ? upBytes * 8.0 / 1_000_000.0 / seconds : 0;

            return new NetworkSample(down, up);
        } catch (System.Exception e) {
            Log.Warn("NetworkUsageSampler sample failed", e);
            return new NetworkSample(0, 0);
        }
    }

    /// <summary>Locks onto <paramref name="primary"/> as the sampled adapter and resets the baseline.</summary>
    private void Rebaseline(NetworkInterface primary) {
        _primaryId = primary.Id;
        AdapterName = primary.Name;
        var stats = primary.GetIPStatistics();
        _prevBytesReceived = stats.BytesReceived;
        _prevBytesSent = stats.BytesSent;
        _prevElapsedSeconds = _clock.Elapsed.TotalSeconds;
    }

    /// <summary>
    /// Picks the internet-facing adapter: from the operational, non-loopback/tunnel adapters, prefer
    /// those advertising a usable default gateway (which excludes most virtual/host-only adapters),
    /// then take the busiest by cumulative bytes. Falls back to the busiest overall if none are routed.
    ///
    /// <c>internal</c> so the Network tab's adapter/IP provider can identify the same primary adapter
    /// without duplicating this selection logic.
    /// </summary>
    internal static NetworkInterface? SelectPrimary() {
        var active = NetworkInterface.GetAllNetworkInterfaces()
            .Where(static a =>
                a.OperationalStatus == OperationalStatus.Up &&
                a.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                a.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
            .ToList();
        if (active.Count == 0)
            return null;

        var routed = active.Where(HasUsableGateway).ToList();
        var pool = routed.Count > 0 ? routed : active;

        return pool.OrderByDescending(TotalBytes).First();
    }

    private static NetworkInterface? FindById(string? id) {
        if (id is null)
            return null;
        try {
            return NetworkInterface.GetAllNetworkInterfaces()
                .FirstOrDefault(a => a.Id == id && a.OperationalStatus == OperationalStatus.Up);
        } catch {
            return null;
        }
    }

    /// <summary>True if the adapter advertises a real default gateway (not the unspecified 0.0.0.0/::).
    /// <c>internal</c> for reuse by the Network tab's adapter classification.</summary>
    internal static bool HasUsableGateway(NetworkInterface a) {
        try {
            foreach (var g in a.GetIPProperties().GatewayAddresses) {
                var addr = g.Address;
                if (addr is not null && !addr.Equals(IPAddress.Any) && !addr.Equals(IPAddress.IPv6Any))
                    return true;
            }
        } catch {
            // Some adapters refuse GetIPProperties(); treat as unrouted.
        }
        return false;
    }

    private static long TotalBytes(NetworkInterface a) {
        try {
            var s = a.GetIPStatistics();
            return s.BytesReceived + s.BytesSent;
        } catch {
            return 0;
        }
    }
}
