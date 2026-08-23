using DashDetective.Services.Diagnostics;
using System;
using System.Collections.Generic;
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
/// It deliberately samples a SINGLE adapter — the internet-facing one — rather than summing all
/// adapters. On .NET, <see cref="NetworkInterface.GetAllNetworkInterfaces"/> returns many virtual /
/// filter / phantom adapters (Hyper-V, VirtualBox, WFP, …) that mirror the physical NIC's counters, so
/// summing them multi-counts the same traffic (observed ~8× inflation). A single primary adapter matches
/// what Task Manager reports per connection. No native dependencies; fails soft to zero.
///
/// Which adapter that is gets re-checked against RECENT traffic on a slow cadence, not fixed at startup:
/// the cold-start pick can only go on lifetime byte counts, so on a machine with Ethernet and Wi-Fi both
/// connected it can land on whichever moved more bytes historically — often the idle one — and the
/// readouts would then sit near zero while Task Manager showed the other adapter busy.
///
/// Lives under <c>src/Services/Network</c> (not a tab folder) because it is shared: the Dashboard's
/// throughput surfaces and the Network tab both sample through it, and the Network tab's
/// adapter/IP provider reuses <see cref="SelectPrimary"/> to identify the primary adapter — so the
/// adapter-filtering / primary-selection logic lives in exactly one place.
/// </summary>
public sealed class NetworkUsageSampler {
    /// <summary>Shortest interval that yields a trustworthy rate. The shared metrics service primes its
    /// cache by sampling immediately after this sampler baselines, so a call can land a fraction of a
    /// millisecond after the previous one; dividing whatever bytes arrived in that sliver by it produces a
    /// wildly inflated figure, which then pins the throughput charts' auto-scaled axis for a whole
    /// window.</summary>
    private const double MinIntervalSeconds = 0.05;

    /// <summary>How often the primary adapter is re-checked against recent traffic.</summary>
    private static readonly TimeSpan ReselectInterval = TimeSpan.FromSeconds(5);

    /// <summary>How much more traffic a challenger must have carried over the comparison window before the
    /// sampler switches to it. Stops two near-idle adapters trading the slot back and forth, since every
    /// switch costs a rebaseline and a zero tick.</summary>
    private const long ReselectMarginBytes = 256 * 1024;

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private long _prevBytesReceived;
    private long _prevBytesSent;
    private double _prevElapsedSeconds;
    private string? _primaryId;

    // Cumulative totals per candidate adapter at the last re-check, so the next one can compare recent
    // traffic rather than lifetime counts.
    private readonly Dictionary<string, long> _candidateBytes = new(StringComparer.Ordinal);
    private double _nextReselectSeconds;

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

            // Hand over to another routed adapter when it is the one actually carrying traffic.
            primary = ReselectIfBusier(primary);

            // The primary changed (adapter added/removed, connection switched, traffic moved): its counters
            // aren't comparable to the old baseline, so rebaseline and report no rate for this tick.
            if (primary.Id != _primaryId) {
                Rebaseline(primary);
                return new NetworkSample(0, 0);
            }

            var now = _clock.Elapsed.TotalSeconds;
            var seconds = now - _prevElapsedSeconds;

            // Too short an interval to divide by. Report nothing and leave the baseline untouched, so the
            // next tick measures across the real interval instead of discarding these bytes.
            if (seconds < MinIntervalSeconds)
                return new NetworkSample(0, 0);

            var stats = primary.GetIPStatistics();
            var received = stats.BytesReceived;
            var sent = stats.BytesSent;

            // Clamp negatives to guard against a counter reset between samples.
            var downBytes = received - _prevBytesReceived;
            var upBytes = sent - _prevBytesSent;

            _prevBytesReceived = received;
            _prevBytesSent = sent;
            _prevElapsedSeconds = now;

            var down = downBytes > 0 ? downBytes * 8.0 / 1_000_000.0 / seconds : 0;
            var up = upBytes > 0 ? upBytes * 8.0 / 1_000_000.0 / seconds : 0;

            return new NetworkSample(down, up);
        } catch (System.Exception e) {
            // Logged here for the adapter context, then rethrown — deliberately NOT turned into a zero
            // sample. MetricChannel converts a throw into _onFailed(), which renders "—"; swallowing it
            // into new NetworkSample(0, 0) made that callback unreachable through this path and drew a
            // confident, live-looking flat line for a counter that was not being read at all. The zero
            // returns above are different: each is a real measurement of nothing, not a failed read.
            Log.Warn("NetworkUsageSampler sample failed", e);
            throw;
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
    /// The adapters worth sampling: the operational, non-loopback/tunnel ones, narrowed to those
    /// advertising a usable default gateway (which excludes most virtual/host-only adapters) when any
    /// qualify. Empty when the machine has no usable adapter.
    /// </summary>
    private static List<NetworkInterface> Candidates() {
        var active = NetworkInterface.GetAllNetworkInterfaces()
            .Where(static a =>
                a.OperationalStatus == OperationalStatus.Up &&
                a.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                a.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
            .ToList();
        if (active.Count == 0)
            return active;

        var routed = active.Where(HasUsableGateway).ToList();
        return routed.Count > 0 ? routed : active;
    }

    /// <summary>
    /// Picks the internet-facing adapter from the <see cref="Candidates"/>, taking the busiest by
    /// cumulative bytes. Lifetime counts are all there is to go on for a cold start; once sampling is
    /// running, <see cref="ReselectIfBusier"/> corrects the choice from recent traffic.
    ///
    /// <c>internal</c> so the Network tab's adapter/IP provider can identify the same primary adapter
    /// without duplicating this selection logic.
    /// </summary>
    internal static NetworkInterface? SelectPrimary() {
        var pool = Candidates();
        return pool.Count == 0 ? null : pool.OrderByDescending(TotalBytes).First();
    }

    /// <summary>
    /// Returns the routed adapter that has actually carried the most traffic since the last check —
    /// <paramref name="current"/> unless a challenger beats it by <see cref="ReselectMarginBytes"/>.
    /// Runs only every <see cref="ReselectInterval"/>, and the first pass merely seeds the comparison
    /// table (there is no earlier reading to difference against), so a switch needs two passes.
    /// </summary>
    private NetworkInterface ReselectIfBusier(NetworkInterface current) {
        var now = _clock.Elapsed.TotalSeconds;
        if (now < _nextReselectSeconds)
            return current;
        _nextReselectSeconds = now + ReselectInterval.TotalSeconds;

        var seeded = _candidateBytes.Count > 0;
        NetworkInterface? busiest = null;
        long busiestDelta = 0, currentDelta = 0;

        foreach (var candidate in Candidates()) {
            var total = TotalBytes(candidate);
            var delta = _candidateBytes.TryGetValue(candidate.Id, out var previous) && total > previous
                ? total - previous
                : 0;
            _candidateBytes[candidate.Id] = total;

            if (candidate.Id == current.Id)
                currentDelta = delta;
            else if (delta > busiestDelta) {
                busiestDelta = delta;
                busiest = candidate;
            }
        }

        return seeded && busiest is not null && busiestDelta > currentDelta + ReselectMarginBytes
            ? busiest
            : current;
    }

    private static NetworkInterface? FindById(string? id) {
        if (id is null)
            return null;
        try {
            return NetworkInterface.GetAllNetworkInterfaces()
                .FirstOrDefault(a => a.Id == id && a.OperationalStatus == OperationalStatus.Up);
        } catch {
            // Enumeration can fail while adapters are being reconfigured. Null means "not found", which
            // the caller already handles by re-picking the primary.
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
            // Only ever used to rank adapters by traffic, so an unreadable one simply sorts last. Zero
            // is safe here precisely because nothing renders this number.
            return 0;
        }
    }
}
