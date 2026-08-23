using DashDetective.Services.Diagnostics;
using DashDetective.Services.Platform.Linux;
using System;
using System.Globalization;

namespace DashDetective.Services.SystemMetrics;

/// <summary>
/// Reads a drive's temperature from its hwmon in sysfs — the Linux counterpart to
/// <see cref="WindowsDiskTemperatureProvider"/>'s NVMe health-log IOCTL, and rootless for the same reason:
/// the kernel has already done the privileged read.
///
/// <b>Finding nothing is the common case, not a fault.</b> NVMe drives register a hwmon automatically, but
/// SATA and SAS drives only do so through <c>drivetemp</c>, a module most distributions do not load by
/// default. A machine with a spinning disk and no <c>drivetemp</c> reports no temperature, and that is
/// correct behaviour rather than something to work around — the alternative sources all need root.
///
/// <b>Matched on the hwmon's <c>name</c>, never its index.</b> hwmon numbering is not stable across boots
/// and the low numbers usually belong to the CPU package and the ACPI thermal zone; reading
/// <c>hwmon0/temp1_input</c> would cheerfully report the processor's temperature on a drive card.
///
/// <b>Two different walks reach the block device</b>, because the kernel hangs the two hwmon kinds off
/// different parents — see <see cref="BlockDeviceOf"/>. Stateless, as every
/// <see cref="HardwareProviders"/> member must be: it re-walks per call rather than caching, which costs a
/// handful of pseudo-file reads on a timer that fires every fifteenth tick.
/// </summary>
internal sealed class LinuxDiskTemperatureProvider : IDiskTemperatureProvider {
    // Concatenated forward-slash literals, never Path.Combine — see IProcFileSystem.
    private const string HwmonRoot = "/sys/class/hwmon";

    /// <summary>The two hwmon drivers that describe a drive. <c>nvme</c> is the controller's own sensor;
    /// <c>drivetemp</c> is the SATA/SAS module.</summary>
    private static readonly string[] DriveSensors = ["nvme", "drivetemp"];

    // The same plausible window the Windows arm applies: outside it, the drive is not really reporting.

    private readonly IProcFileSystem _proc;

    public LinuxDiskTemperatureProvider() : this(new ProcFileSystem()) { }

    /// <summary>Test seam: injects the filesystem so both mapping shapes can be exercised against canned
    /// fixtures from any dev machine.</summary>
    internal LinuxDiskTemperatureProvider(IProcFileSystem proc) => _proc = proc;

    public double? ReadCelsius(int deviceId) {
        try {
            foreach (var entry in _proc.ListDirectory(HwmonRoot)) {
                var hwmon = HwmonRoot + "/" + entry;

                if (!IsDriveSensor(_proc.ReadAllText(hwmon + "/name")))
                    continue;

                if (BlockDeviceOf(hwmon) is not { } device)
                    continue;

                if (SysBlockFacts.DiskNumberOf(_proc, device) != deviceId)
                    continue;

                return PlausibleCelsius(ParseMillidegrees(_proc.ReadAllText(hwmon + "/temp1_input")));
            }
        } catch (Exception e) {
            Log.Warn("LinuxDiskTemperatureProvider read failed", e);
        }

        return null;
    }

    /// <summary>Whether a hwmon's <c>name</c> is one of the drive sensors. Pure; unit-tested.</summary>
    internal static bool IsDriveSensor(string? name) {
        var trimmed = name?.Trim();
        return trimmed is not null && Array.IndexOf(DriveSensors, trimmed) >= 0;
    }

    /// <summary>
    /// The block device a drive hwmon belongs to, or <c>null</c> when the link leads nowhere.
    ///
    /// The two kinds resolve differently, which is the whole difficulty. A <c>drivetemp</c> hwmon hangs off
    /// the SCSI device, whose block device sits in a <c>block/</c> subdirectory
    /// (<c>…/0:0:0:0/block/sda</c>). An <c>nvme</c> hwmon hangs off the <b>controller</b>, and the block
    /// device is a namespace <i>child</i> of it (<c>…/nvme/nvme0</c> → <c>nvme0n1</c>) — so treating the
    /// link target as the device, which works for drivetemp, finds nothing at all for NVMe.
    /// </summary>
    private string? BlockDeviceOf(string hwmon) {
        if (_proc.ResolveLink(hwmon + "/device") is not { } device || device.Length == 0)
            return null;

        // drivetemp: the block device is listed under the SCSI device's block/ directory.
        foreach (var name in _proc.ListDirectory(device + "/block"))
            return name;

        // nvme: the namespaces are children of the controller, prefixed with its name (nvme0 → nvme0n1).
        var controller = LastSegment(device);
        if (controller.Length == 0)
            return null;

        foreach (var name in _proc.ListDirectory(device))
            if (name.Length > controller.Length && name.StartsWith(controller, StringComparison.Ordinal))
                return name;

        return null;
    }

    /// <summary>Converts a hwmon reading to degrees. <c>temp1_input</c> is <b>millidegrees</b>, so 42850 is
    /// 42.85 °C — read as whole degrees it would report a drive at forty-two thousand. Pure;
    /// unit-tested.</summary>
    internal static double? ParseMillidegrees(string? text) =>
        text is not null
        && double.TryParse(
            text.AsSpan().Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value / 1000.0
            : null;

    /// <summary>Rejects a reading outside a plausible drive range, matching the Windows arm — a sensor
    /// reporting 0 means "not reported", not a drive at freezing. Pure; unit-tested.</summary>
    internal static double? PlausibleCelsius(double? celsius) => DiskTemperatureRange.Celsius(celsius);

    private static string LastSegment(string path) {
        var slash = path.LastIndexOf('/');
        return slash < 0 ? path : path[(slash + 1)..];
    }
}
