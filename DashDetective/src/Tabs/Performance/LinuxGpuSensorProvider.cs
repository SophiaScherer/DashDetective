using DashDetective.Services.Diagnostics;
using DashDetective.Services.Platform.Linux;
using DashDetective.Services.SystemMetrics;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace DashDetective.Tabs.Performance;

/// <summary>
/// Reads GPU temperature and power from the card's own hwmon in sysfs — the Linux counterpart to
/// <see cref="WindowsGpuSensorProvider"/>'s vendor-SDK fan-out.
///
/// <b>There is no vendor routing here, and no PCI matching.</b> Windows needs both because it has no in-box
/// sensor API: each vendor ships its own SDK, and those SDKs enumerate adapters in their own order with no
/// LUID, so <see cref="GpuPciMatcher"/> exists to join them back up. The kernel already publishes each
/// card's sensors underneath the card itself, so the adapter key alone finds them and the
/// <paramref name="pci"/> argument is unused.
///
/// <b>Three different unit scales, none of them the display unit:</b> <c>temp1_input</c> is millidegrees
/// Celsius, <c>power1_average</c> microwatts. Picking the wrong divisor still yields a plausible-looking
/// number, so both are converted here and range-checked before they reach a tile.
///
/// A card whose driver registers no hwmon — the proprietary NVIDIA blob does not — has no sensors to read
/// and reports <see cref="GpuSensorSample.None"/>, which leaves both tiles at "—". Never throws.
/// </summary>
internal sealed class LinuxGpuSensorProvider : IGpuSensorProvider {
    // Plausible windows; anything outside means "not really reported", the DiskTemperatureProvider idiom.
    // The temperature window matches the vendor readers'; the power floor is deliberately theirs too —
    // an idle card genuinely reporting a fraction of a watt should still show a number.
    private const double MinCelsius = 1;
    private const double MaxCelsius = 150;
    private const double MinWatts = 0.1;
    private const double MaxWatts = 2000;

    private const string TemperatureFile = "/temp1_input";

    // amdgpu reports an averaged figure; some drivers offer only the instantaneous one. Preference order.
    private static readonly string[] PowerFiles = ["/power1_average", "/power1_input"];

    private readonly IProcFileSystem _proc;

    // Adapter key → the card's hwmon directory. Resolved once: the card set is static hardware, and the
    // walk that finds it re-reads a dozen files per card.
    private readonly Dictionary<string, string> _hwmonByAdapter = new(StringComparer.Ordinal);

    public LinuxGpuSensorProvider() : this(new ProcFileSystem()) { }

    /// <summary>Test seam: injects the filesystem so the hwmon read can be exercised against canned
    /// fixtures from any dev machine.</summary>
    internal LinuxGpuSensorProvider(IProcFileSystem proc) {
        _proc = proc;

        try {
            foreach (var card in DrmCardFacts.Read(proc))
                if (card.HwmonPath.Length > 0)
                    _hwmonByAdapter[card.Key] = card.HwmonPath;
        } catch (Exception e) {
            // An empty map reports nothing forever — the same inert soft-fail the vendor readers hold to.
            Log.Warn("LinuxGpuSensorProvider enumeration failed", e);
        }
    }

    public GpuSensorSample Read(string adapterKey, GpuPciId? pci) {
        if (!_hwmonByAdapter.TryGetValue(adapterKey, out var hwmon))
            return GpuSensorSample.None;

        return new GpuSensorSample(ReadTemperature(hwmon), ReadPower(hwmon));
    }

    private double? ReadTemperature(string hwmon) =>
        PlausibleCelsius(ParseScaled(_proc.ReadAllText(hwmon + TemperatureFile), 1000.0));

    private double? ReadPower(string hwmon) {
        foreach (var file in PowerFiles)
            if (PlausibleWatts(ParseScaled(_proc.ReadAllText(hwmon + file), 1_000_000.0)) is { } watts)
                return watts;

        return null;
    }

    /// <summary>Divides a sysfs integer reading by its scale, or <c>null</c> when the file is absent or
    /// holds something else. sysfs reports these as whole numbers in a sub-unit precisely so it never has
    /// to write a decimal point. Pure; unit-tested.</summary>
    internal static double? ParseScaled(string? text, double scale) =>
        text is not null
        && double.TryParse(
            text.AsSpan().Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value / scale
            : null;

    /// <summary>Rejects a temperature outside a plausible GPU range — a driver that reports 0 for a sensor
    /// it does not have would otherwise show a card sitting at absolute zero. Pure; unit-tested.</summary>
    internal static double? PlausibleCelsius(double? celsius) =>
        celsius is >= MinCelsius and <= MaxCelsius ? celsius : null;

    /// <summary>Rejects a power draw outside a plausible board range, which is also what catches a wrong
    /// unit scale: a milliwatt reading read as microwatts lands far below the floor. Pure;
    /// unit-tested.</summary>
    internal static double? PlausibleWatts(double? watts) =>
        watts is >= MinWatts and <= MaxWatts ? watts : null;

    /// <summary>Nothing to release: sysfs reads open and close per call, unlike a vendor SDK's init
    /// handles.</summary>
    public void Dispose() { }
}
