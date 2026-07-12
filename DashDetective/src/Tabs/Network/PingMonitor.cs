using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading.Tasks;

namespace DashDetective.Tabs.Network;

/// <summary>
/// Continuously pings a user-supplied host via the in-box <see cref="Ping"/> API. One
/// <see cref="Ping"/> instance is reused across sends — the owner must serialize calls (a
/// <see cref="Ping"/> can't run two operations at once); the Network VM does this with an in-flight
/// guard. Keeps a rolling window of results for an average-RTT / packet-loss summary and the last
/// few reply lines for the console-style readout. Never throws: a failed send is recorded as a
/// timeout. The target can be changed at runtime via <see cref="SetTarget"/>, which resets the window.
/// </summary>
public sealed class PingMonitor : IDisposable {
    /// <summary>The default ping target, used until the user edits the field.</summary>
    public const string DefaultTarget = "8.8.8.8";

    /// <summary>The current ping target. Change via <see cref="SetTarget"/> so the window resets.</summary>
    public string Target { get; private set; } = DefaultTarget;

    private const int TimeoutMs = 1500;
    private const int WindowSize = 20; // rolling window for avg RTT + loss %
    private const int LineCount = 3;   // console shows the last N replies

    private readonly Ping _ping = new();
    private readonly Queue<bool> _results = new();
    private readonly Queue<long> _rtts = new();
    private readonly Queue<string> _lines = new();

    /// <summary>Last few reply lines joined for the console readout (newest at the bottom).</summary>
    public string ConsoleText => string.Join("\n", _lines);

    /// <summary>Average-RTT / loss summary over the rolling window, or an unreachable note when all
    /// recent pings failed.</summary>
    public string SummaryText {
        get {
            if (_results.Count == 0)
                return "";
            var failures = _results.Count(static ok => !ok);
            var loss = failures * 100 / _results.Count;
            if (_rtts.Count == 0)
                return $"{Target} unreachable · 100% loss";
            var avg = (long)Math.Round(_rtts.Average());
            return $"Avg = {avg.ToString(CultureInfo.InvariantCulture)}ms · " +
                   $"{loss.ToString(CultureInfo.InvariantCulture)}% loss";
        }
    }

    /// <summary>Sends one ping and folds the result into the rolling window + console lines.</summary>
    public async Task SendAsync() {
        string line;
        var ok = false;
        long rtt = 0;

        try {
            var reply = await _ping.SendPingAsync(Target, TimeoutMs);
            if (reply.Status == IPStatus.Success) {
                ok = true;
                rtt = reply.RoundtripTime;
                // Options (and thus TTL) can be null on some stacks / failure paths — omit TTL then.
                var ttl = reply.Options?.Ttl;
                line = ttl.HasValue
                    ? $"Reply from {Target}: time={Format(rtt)}ms TTL={ttl.Value.ToString(CultureInfo.InvariantCulture)}"
                    : $"Reply from {Target}: time={Format(rtt)}ms";
            } else {
                line = "Request timed out.";
            }
        } catch {
            // Offline / resolution failure / send error — record as a timeout, keep going.
            line = "Request timed out.";
        }

        Push(_lines, line, LineCount);
        Push(_results, ok, WindowSize);
        if (ok)
            Push(_rtts, rtt, WindowSize);
    }

    /// <summary>Switches to a new target and clears the rolling window + console lines, so the avg/loss
    /// summary and reply history don't carry over from the previous host. A blank value is ignored.</summary>
    public void SetTarget(string target) {
        target = target?.Trim() ?? "";
        if (target.Length == 0)
            return;
        Target = target;
        _results.Clear();
        _rtts.Clear();
        _lines.Clear();
    }

    private static string Format(long rtt) =>
        rtt == 0 ? "<1" : rtt.ToString(CultureInfo.InvariantCulture);

    private static void Push<T>(Queue<T> queue, T item, int max) {
        queue.Enqueue(item);
        while (queue.Count > max)
            queue.Dequeue();
    }

    public void Dispose() => _ping.Dispose();
}
