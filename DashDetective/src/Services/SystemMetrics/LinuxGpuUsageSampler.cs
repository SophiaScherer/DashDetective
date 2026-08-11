using DashDetective.Services.Diagnostics;
using DashDetective.Services.Platform.Linux;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace DashDetective.Services.SystemMetrics;

/// <summary>
/// Samples per-GPU utilisation from sysfs — amdgpu's <c>gpu_busy_percent</c>, an instantaneous 0–100
/// integer per card. The Linux counterpart to <see cref="WindowsGpuUsageSampler"/>'s PDH query.
///
/// <b>Every sample carries an empty engine map.</b> Windows splits a GPU's load across engine types (3D,
/// Copy, VideoDecode …) and the Performance tab builds its Detailed grid from that; sysfs publishes one
/// scalar per card and nothing finer, and the per-engine breakdown lives in debugfs, which needs root. An
/// empty map leaves the Detailed toggle hidden rather than showing an empty grid — that is the intended
/// outcome, not an unfinished one.
///
/// <b>Every adapter is reported; a card whose driver publishes no <c>gpu_busy_percent</c> gets a null
/// reading</b> rather than 0%. Both halves matter: the inventory builds a GPU card only for an adapter this
/// sampler names, so omitting one hides real hardware, while a zero would show it as permanently idle.
/// Today the null case is the proprietary NVIDIA driver and Intel's i915, neither of which exposes a
/// rootless utilisation figure.
///
/// The card list is resolved once, at construction: the adapters are static hardware, and re-deriving the
/// whole <see cref="DrmCardFacts"/> picture every tick would re-read a dozen files per card to learn
/// nothing new. Never throws: any failure yields an empty map.
/// </summary>
internal sealed class LinuxGpuUsageSampler : IGpuUsageSampler {
    private const string BusyFile = "/gpu_busy_percent";

    private static readonly IReadOnlyDictionary<string, GpuAdapterSample> Empty =
        new Dictionary<string, GpuAdapterSample>();

    private static readonly IReadOnlyDictionary<string, double> NoEngines = new Dictionary<string, double>();

    /// <summary>PCI vendor id for NVIDIA — the only cards <c>nvidia-smi</c> can answer for, so a machine
    /// without one never spawns it however the setting is left.</summary>
    private const uint NvidiaVendorId = 0x10DE;

    private readonly IProcFileSystem _proc;
    private readonly NvidiaSmiReader? _nvidiaSmi;

    // Key → the one file this sampler re-reads per tick, or null for a card that publishes none. Cards
    // with no source are kept so the adapter still appears, reporting a null utilisation.
    private readonly List<(string Key, string? BusyPath)> _cards = [];

    private bool _hasNvidiaCard;

    public LinuxGpuUsageSampler() : this(new ProcFileSystem(), new NvidiaSmiReader()) { }

    /// <summary>Test seam: injects the filesystem so the sysfs read can be exercised against canned
    /// fixtures from any dev machine, and the nvidia-smi reader so no process ever exists.</summary>
    internal LinuxGpuUsageSampler(IProcFileSystem proc, NvidiaSmiReader? nvidiaSmi = null) {
        _proc = proc;
        _nvidiaSmi = nvidiaSmi;

        try {
            foreach (var card in DrmCardFacts.Read(proc)) {
                var path = card.DevicePath + BusyFile;
                _cards.Add((card.Key, proc.Exists(path) ? path : null));
                _hasNvidiaCard |= card.VendorId == NvidiaVendorId;
            }
        } catch (Exception e) {
            // A failed enumeration leaves _cards empty, so SampleAdapters returns nothing forever — the
            // same inert soft-fail contract the PDH sampler holds to.
            Log.Warn("LinuxGpuUsageSampler enumeration failed", e);
        }
    }

    /// <inheritdoc/>
    public bool NvidiaMetricsEnabled { get; set; }

    public IReadOnlyDictionary<string, GpuAdapterSample> SampleAdapters() {
        if (_cards.Count == 0)
            return Empty;

        var nvidia = NvidiaReadings();

        var samples = new Dictionary<string, GpuAdapterSample>(_cards.Count, StringComparer.Ordinal);
        foreach (var (key, path) in _cards) {
            // sysfs first: it is free, current, and the only source for AMD. nvidia-smi fills in only the
            // cards sysfs cannot answer for.
            var percent = path is null ? null : ParsePercent(_proc.ReadAllText(path));
            samples[key] = new GpuAdapterSample(percent ?? nvidia?.Utilisation(key), NoEngines);
        }

        return samples;
    }

    /// <summary>The nvidia-smi reader to consult this tick, or <c>null</c> when it is switched off, absent,
    /// or there is no NVIDIA card to ask about. Also nudges it to refresh, which it does on its own slow
    /// cadence rather than per tick.</summary>
    private NvidiaSmiReader? NvidiaReadings() {
        if (!NvidiaMetricsEnabled || !_hasNvidiaCard || _nvidiaSmi is null)
            return null;

        _nvidiaSmi.RefreshIfDue();
        return _nvidiaSmi;
    }

    /// <summary>Parses a <c>gpu_busy_percent</c> body into a clamped 0–100 reading, or <c>null</c> when the
    /// file has gone away or holds something else — which drops the card from this tick rather than
    /// reporting it idle. Pure; unit-tested.</summary>
    internal static double? ParsePercent(string? text) {
        if (text is null
            || !double.TryParse(
                text.AsSpan().Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            return null;

        return Math.Clamp(value, 0, 100);
    }

    /// <summary>Nothing to release: sysfs reads open and close per call, unlike a PDH query handle.</summary>
    public void Dispose() { }
}
