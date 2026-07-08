using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;

namespace DashDetective.Tabs.Dashboard;

/// <summary>
/// A single network-throughput snapshot: download and upload rates in megabits per second (Mbps).
/// </summary>
public readonly record struct NetworkSample(double DownMbps, double UpMbps);

/// <summary>
/// Samples aggregate network throughput via the managed <see cref="NetworkInterface"/> API. The OS
/// exposes cumulative byte counters per adapter; each <see cref="Sample"/> call differences those
/// totals over the elapsed wall-clock interval to derive a rate. Bytes from every operational,
/// non-loopback/tunnel adapter are summed, so the reading reflects the machine's total traffic.
/// No native dependencies, negligible per-sample cost. Fails soft to zero.
/// </summary>
public sealed class NetworkUsageSampler {
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private long _prevBytesReceived;
    private long _prevBytesSent;
    private double _prevElapsedSeconds;

    /// <summary>
    /// The friendly name of the primary active adapter (most cumulative traffic at construction
    /// time), for display as the throughput panel's caption. Empty if none could be read.
    /// </summary>
    public string AdapterName { get; } = string.Empty;

    public NetworkUsageSampler() {
        // Seed baseline totals so the first Sample() reflects a real interval rather than the whole
        // time since boot. Also pick the busiest adapter for the display label.
        try {
            var adapters = ActiveAdapters();
            _prevBytesReceived = SumBytesReceived(adapters);
            _prevBytesSent = SumBytesSent(adapters);
            _prevElapsedSeconds = _clock.Elapsed.TotalSeconds;

            var primary = adapters
                .OrderByDescending(static a => {
                    try { var s = a.GetIPStatistics(); return s.BytesReceived + s.BytesSent; }
                    catch { return 0L; }
                })
                .FirstOrDefault();
            if (primary is not null)
                AdapterName = primary.Name;
        }
        catch {
            // Leave baselines at zero; the first Sample() will simply read a large-but-clamped rate
            // or zero, and subsequent samples self-correct.
        }
    }

    /// <summary>
    /// Returns download/upload rates (Mbps) since the previous call. Rate is delta-bytes over the
    /// elapsed interval measured by a <see cref="Stopwatch"/> (the caller's timer is not exact),
    /// converted bytes/s → Mbps as <c>bytes * 8 / 1_000_000</c>.
    /// </summary>
    public NetworkSample Sample() {
        try {
            var adapters = ActiveAdapters();
            var received = SumBytesReceived(adapters);
            var sent = SumBytesSent(adapters);

            var now = _clock.Elapsed.TotalSeconds;
            var seconds = now - _prevElapsedSeconds;

            // Counter deltas. Clamp negatives to guard against an adapter dropping out / counter
            // reset between samples (would otherwise produce a spurious negative rate).
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
        }
        catch {
            return new NetworkSample(0, 0);
        }
    }

    /// <summary>Operational, real (non-loopback/tunnel) adapters.</summary>
    private static List<NetworkInterface> ActiveAdapters() =>
        NetworkInterface.GetAllNetworkInterfaces()
            .Where(static a =>
                a.OperationalStatus == OperationalStatus.Up &&
                a.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                a.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
            .ToList();

    private static long SumBytesReceived(IEnumerable<NetworkInterface> adapters) {
        long total = 0;
        foreach (var a in adapters) {
            try { total += a.GetIPStatistics().BytesReceived; }
            catch { /* skip adapters that refuse statistics */ }
        }
        return total;
    }

    private static long SumBytesSent(IEnumerable<NetworkInterface> adapters) {
        long total = 0;
        foreach (var a in adapters) {
            try { total += a.GetIPStatistics().BytesSent; }
            catch { /* skip adapters that refuse statistics */ }
        }
        return total;
    }
}
