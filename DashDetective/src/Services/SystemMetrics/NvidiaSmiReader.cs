using DashDetective.Services.Diagnostics;
using DashDetective.Tabs.Toolkit;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;

namespace DashDetective.Services.SystemMetrics;

/// <summary>
/// Reads NVIDIA GPU utilisation by shelling out to <c>nvidia-smi</c> — the only rootless source on Linux,
/// since the proprietary driver publishes nothing in sysfs.
///
/// <b>This spawns a process, so it is deliberately kept off the sampling path.</b> A refresh runs at most
/// once every <see cref="RefreshSeconds"/> seconds of wall clock (not every N ticks — the tick interval is
/// a user setting and can be half a second), never overlaps itself, and never blocks the caller: readings
/// come from the last completed run, and the very first tick after enabling reports nothing. It is also
/// gated behind an off-by-default setting, so a machine that never opts in never spawns anything.
///
/// The first failure retires it for the session: a missing binary throws rather than returning a code, and
/// retrying it every 15 seconds forever would be a spawn storm in slow motion.
/// </summary>
internal sealed class NvidiaSmiReader {
    /// <summary>Wall-clock seconds between runs. Well below any plausible "how hot is my GPU right now"
    /// need, and far above the sampling tick.</summary>
    private const int RefreshSeconds = 15;

    private const string Executable = "nvidia-smi";

    private static readonly string[] Arguments =
        ["--query-gpu=pci.bus_id,utilization.gpu", "--format=csv,noheader,nounits"];

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private readonly IProcessLauncher _launcher;
    private readonly Stopwatch _sinceRefresh = new();
    private readonly object _gate = new();

    // Last completed run's readings, keyed by normalised PCI address. Replaced wholesale, never merged, so
    // a card that drops out of nvidia-smi's output stops reporting rather than freezing at its last value.
    private volatile IReadOnlyDictionary<string, double> _readings = new Dictionary<string, double>();

    private bool _running;
    private bool _retired;

    public NvidiaSmiReader() : this(new SystemProcessLauncher()) { }

    /// <summary>Test seam: injects the launcher so the parse and the cadence can be exercised without any
    /// process existing.</summary>
    internal NvidiaSmiReader(IProcessLauncher launcher) => _launcher = launcher;

    /// <summary>The last completed run's utilisation for a card, or <c>null</c> when it reported none —
    /// including before the first run finishes.</summary>
    internal double? Utilisation(string pciAddress) =>
        _readings.TryGetValue(pciAddress, out var value) ? value : null;

    /// <summary>Starts a refresh if one is due and none is in flight. Returns immediately; the readings
    /// land on a later call. Never throws.
    /// <para>Locks rather than using the shared <c>OverlapGuard</c>, which is deliberately not
    /// thread-safe: this is called from sampler threads, not the UI thread, so the test-and-set really
    /// can interleave. It also latches off permanently after a failure, which the guard has no notion
    /// of.</para></summary>
    internal void RefreshIfDue() {
        lock (_gate) {
            if (_retired || _running)
                return;
            if (_sinceRefresh.IsRunning && _sinceRefresh.Elapsed.TotalSeconds < RefreshSeconds)
                return;

            _running = true;
        }

        _ = RunAsync();
    }

    private async System.Threading.Tasks.Task RunAsync() {
        try {
            var capture = await _launcher.CaptureAsync(Executable, Arguments, Timeout);
            if (capture.ExitCode == 0 && !capture.TimedOut)
                _readings = Parse(capture.StandardOutput);
        } catch (Exception e) {
            // A missing binary surfaces as a throw, not an exit code. One report, then never again.
            Log.Warn("nvidia-smi is unavailable; NVIDIA GPU utilisation will stay blank", e);
            lock (_gate)
                _retired = true;
        } finally {
            lock (_gate) {
                _running = false;
                _sinceRefresh.Restart();
            }
        }
    }

    /// <summary>
    /// Parses <c>--format=csv,noheader,nounits</c> rows of "<c>bus_id, utilisation</c>" into readings keyed
    /// the way sysfs names the same card. Rows that report no number — nvidia-smi prints
    /// "[N/A]" for a GPU that cannot answer — are dropped rather than read as zero. Pure; unit-tested.
    /// </summary>
    internal static IReadOnlyDictionary<string, double> Parse(string output) {
        var readings = new Dictionary<string, double>(StringComparer.Ordinal);

        foreach (var line in output.Split('\n')) {
            var comma = line.IndexOf(',');
            if (comma <= 0)
                continue;

            var busId = NormalizeBusId(line[..comma]);
            if (busId.Length == 0)
                continue;

            if (double.TryParse(
                    line.AsSpan(comma + 1).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture,
                    out var percent))
                readings[busId] = Math.Clamp(percent, 0, 100);
        }

        return readings;
    }

    /// <summary>
    /// Converts an nvidia-smi bus id to the form sysfs uses, which is the key the rest of the GPU surface
    /// joins on. <b>nvidia-smi writes an eight-digit domain and uppercase hex</b>
    /// ("00000000:01:00.0"), where sysfs writes four and lowercase ("0000:01:00.0") — join them raw and
    /// every NVIDIA reading silently fails to match its card. Pure; unit-tested.
    /// </summary>
    internal static string NormalizeBusId(string busId) {
        var trimmed = busId.Trim();
        var colon = trimmed.IndexOf(':');
        if (colon <= 0)
            return "";

        var domain = trimmed[..colon];
        var rest = trimmed[(colon + 1)..];

        // Domains wider than four digits are zero-padded; narrower ones are padded out to match sysfs.
        domain = domain.Length > 4 ? domain[^4..] : domain.PadLeft(4, '0');

        return (domain + ":" + rest).ToLowerInvariant();
    }
}
