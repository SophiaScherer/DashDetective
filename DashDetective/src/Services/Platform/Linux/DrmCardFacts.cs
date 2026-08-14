using System;
using System.Collections.Generic;
using System.Globalization;

namespace DashDetective.Services.Platform.Linux;

/// <summary>
/// One graphics adapter as <c>/sys/class/drm</c> describes it, derived once and shared by everything that
/// talks about a GPU on Linux: the adapter enumeration behind the Dashboard and Performance cards, the
/// utilisation sampler, the temperature/power reader and the Hardware tab's Graphics card. The
/// <see cref="CpuFacts"/> precedent — where several consumers want the same derived facts, the derivation
/// is shared, not just the parser, so they cannot disagree about the same hardware.
///
/// <b><see cref="Key"/> is the join key for the whole GPU surface.</b> The records these feed
/// (<c>GpuAdapter</c>, <c>GpuAdapterSample</c>, <c>DeviceInstance</c>) are keyed by a DXGI <b>LUID</b>
/// token, and the inventory only builds a card for an adapter that the enumeration and the sampler
/// <i>both</i> report. Linux has no LUID, so this uses the card's PCI address — the kernel's own name for
/// the device, stable across boots and reported identically by every one of these readers. A positional
/// index would drift the moment an eGPU is attached, and worse, would drift <i>independently</i> in each
/// reader, which shows up as no GPU card at all rather than as a wrong one.
///
/// Fields report "not known" honestly — "" or 0 — rather than substituting a stand-in; each consumer
/// applies its own placeholder. Stateless and never throws: an unreadable <c>/sys</c> yields no cards.
/// </summary>
internal sealed record DrmCardFacts(
    string Name,
    string PciAddress,
    string Driver,
    uint VendorId,
    uint DeviceId,
    uint SubsystemVendorId,
    uint SubsystemDeviceId,
    uint Revision,
    ulong VramBytes,
    string HwmonPath,
    string ProductName = "") {

    // Concatenated forward-slash literals, never Path.Combine — see IProcFileSystem.
    private const string DrmRoot = "/sys/class/drm";
    private const string CardPrefix = "card";

    /// <summary>PCI vendor ids the bundled table names. Deliberately short: it exists to turn the two or
    /// three ids a desktop actually reports into a word, not to replace <c>pci.ids</c>.</summary>
    private static readonly Dictionary<uint, string> VendorNames = new() {
        [0x10DE] = "NVIDIA",
        [0x1002] = "AMD",
        [0x1022] = "AMD",
        [0x8086] = "Intel",
        [0x1AF4] = "Red Hat",
        [0x15AD] = "VMware",
        [0x80EE] = "Oracle",
        [0x1414] = "Microsoft",
        [0x1234] = "QEMU",
    };

    /// <summary>DRM drivers that are not a display adapter in their own right — the kernel's generic
    /// framebuffer takeover and the virtual-KMS test device. <b>A paravirtualised GPU is not on this
    /// list:</b> unlike DXGI's software flag (which exists to drop the Microsoft Basic Render Driver
    /// alongside a real card), <c>vboxvideo</c> or <c>virtio-gpu</c> <i>is</i> the machine's only display
    /// adapter, and hiding it would leave a VM with no GPU card at all.</summary>
    private static readonly string[] SoftwareDrivers = ["simpledrm", "vkms"];

    /// <summary>The adapter's stable identity — its PCI address, falling back to the kernel card name for
    /// a device with no PCI parent. Never empty, and derived here so every reader keys on the same
    /// string.</summary>
    internal string Key => PciAddress.Length > 0 ? PciAddress : Name;

    /// <summary>The card's sysfs device directory, where its per-tick attributes live. Exposed so a
    /// sampler can re-read one file without rebuilding the whole derivation, and so the path literal stays
    /// in the one file that owns it.</summary>
    internal string DevicePath => DrmRoot + "/" + Name + "/device";

    /// <summary>Whether this is a placeholder DRM device rather than a real adapter — see
    /// <see cref="SoftwareDrivers"/>.</summary>
    internal bool IsSoftware => Array.IndexOf(SoftwareDrivers, Driver) >= 0;

    /// <summary>This card's display name — its product name when <see cref="ReadNamed"/> resolved one
    /// ("VMware SVGA II Adapter"), otherwise the identity sysfs alone can compose
    /// ("AMD amdgpu (1002:73df)").</summary>
    internal string AdapterName =>
        ProductName.Length > 0 ? ProductName : FormatAdapterName(VendorId, DeviceId, Driver);

    /// <summary>Reads and derives every card. Never throws: an unreadable source yields an empty
    /// list.</summary>
    internal static IReadOnlyList<DrmCardFacts> Read(IProcFileSystem proc) {
        var cards = new List<(int Index, DrmCardFacts Card)>();

        foreach (var entry in proc.ListDirectory(DrmRoot)) {
            if (CardIndex(entry) is not { } index)
                continue;

            if (ReadCard(proc, entry) is { } card)
                cards.Add((index, card));
        }

        // Sorted by the kernel's own card number so the "GPU 0"/"GPU 1" labels are stable; by index rather
        // than by name, or card10 would sort between card1 and card2.
        cards.Sort(static (a, b) => a.Index.CompareTo(b.Index));

        var result = new List<DrmCardFacts>(cards.Count);
        foreach (var (_, card) in cards)
            result.Add(card);

        return result;
    }

    /// <summary>
    /// Every card, with each one's marketing name resolved from the system's <c>pci.ids</c> table where it
    /// carries one. The enrichment is a separate entry point rather than part of <see cref="Read"/> because
    /// it costs a scan of a ~1.5 MB file: only the two readers that <i>display</i> a name — the Hardware
    /// tab's Graphics card and the adapter enumeration behind the Dashboard — call it, while the
    /// per-tick utilisation and sensor readers keep the free walk.
    ///
    /// A host with no <c>pci.ids</c>, or a card the table has never heard of, falls straight back to
    /// <see cref="Read"/>'s behaviour. Never throws.
    /// </summary>
    internal static IReadOnlyList<DrmCardFacts> ReadNamed(IProcFileSystem proc) {
        var cards = Read(proc);
        if (cards.Count == 0)
            return cards;

        var wanted = new List<(uint Vendor, uint Device)>(cards.Count);
        foreach (var card in cards)
            wanted.Add((card.VendorId, card.DeviceId));

        var names = PciIdDatabase.Read(proc, wanted);

        var named = new List<DrmCardFacts>(cards.Count);
        foreach (var card in cards)
            named.Add(card with {
                ProductName = FormatProductName(
                    card.VendorId, names.Vendor(card.VendorId), names.Device(card.VendorId, card.DeviceId)),
            });

        return named;
    }

    /// <summary>
    /// Composes the display name for a card the table named: "VMware SVGA II Adapter", "NVIDIA GA106
    /// [GeForce RTX 3060]". <b>Keyed on the device name, not the vendor</b> — a table that names the vendor
    /// but not the chip has told us nothing the ids did not already say, so that case returns "" and the
    /// caller keeps its own composition.
    ///
    /// The <i>bundled</i> vendor word wins over the table's where there is one, because <c>pci.ids</c>
    /// spells vendors out in full ("Advanced Micro Devices, Inc. [AMD/ATI]") and a card subtitle has no
    /// room for it. Pure; unit-tested.
    /// </summary>
    internal static string FormatProductName(uint vendorId, string tableVendor, string deviceName) {
        if (deviceName.Length == 0)
            return "";

        var vendor = VendorName(vendorId);
        if (vendor.Length == 0)
            vendor = tableVendor;

        return vendor.Length > 0 ? $"{vendor} {deviceName}" : deviceName;
    }

    /// <summary>
    /// Formats an adapter's display name from its PCI identity: "AMD amdgpu (1002:73df)" when the vendor is
    /// named, "amdgpu (1a2b:73df)" when it is not. The raw ids are always present because they are the only
    /// part guaranteed to be right — a card the bundled table has never heard of still reads as a specific
    /// piece of hardware rather than as an empty card. Pure; unit-tested.
    /// </summary>
    internal static string FormatAdapterName(uint vendorId, uint deviceId, string driver) {
        var ids = string.Create(CultureInfo.InvariantCulture, $"({vendorId:x4}:{deviceId:x4})");
        var vendor = VendorName(vendorId);

        if (vendor.Length > 0 && driver.Length > 0)
            return $"{vendor} {driver} {ids}";
        if (vendor.Length > 0)
            return $"{vendor} {ids}";

        return driver.Length > 0 ? $"{driver} {ids}" : ids;
    }

    /// <summary>The bundled name for a PCI vendor id, or "" when the table does not carry it.</summary>
    internal static string VendorName(uint vendorId) =>
        VendorNames.TryGetValue(vendorId, out var name) ? name : "";

    /// <summary>Parses a sysfs PCI id file ("0x1002\n") into its numeric value; 0 when absent or
    /// malformed. The kernel always writes these with the <c>0x</c> prefix, which
    /// <see cref="NumberStyles.HexNumber"/> does not accept — reading one as a plain hex number fails on
    /// every real machine.</summary>
    internal static uint ParseHexId(string? text) {
        if (text is null)
            return 0;

        var body = text.AsSpan().Trim();
        if (body.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            body = body[2..];

        return uint.TryParse(body, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;
    }

    /// <summary>Reads one card, or <c>null</c> when it reports no PCI vendor — a DRM node with no PCI
    /// identity (the kernel's simple framebuffer, an SoC display controller) has no name, no ids and no
    /// sensors to show, and a card built from it would render blank.</summary>
    private static DrmCardFacts? ReadCard(IProcFileSystem proc, string name) {
        var device = DrmRoot + "/" + name + "/device";

        var vendorId = ParseHexId(proc.ReadAllText(device + "/vendor"));
        if (vendorId == 0)
            return null;

        return new DrmCardFacts(
            name,
            LastSegment(proc.ResolveLink(device)),
            LastSegment(proc.ResolveLink(device + "/driver")),
            vendorId,
            ParseHexId(proc.ReadAllText(device + "/device")),
            ParseHexId(proc.ReadAllText(device + "/subsystem_vendor")),
            ParseHexId(proc.ReadAllText(device + "/subsystem_device")),
            ParseHexId(proc.ReadAllText(device + "/revision")),
            ParseUInt64(proc.ReadAllText(device + "/mem_info_vram_total")),
            HwmonPathOf(proc, device));
    }

    /// <summary>The card's hwmon directory, or "" when it has none. amdgpu and the open NVIDIA drivers
    /// register one per card; the proprietary NVIDIA blob registers none, which is why its temperature has
    /// no sysfs source. Only the first is taken — a card registers at most one.</summary>
    private static string HwmonPathOf(IProcFileSystem proc, string device) {
        const string hwmonPrefix = "hwmon";
        var root = device + "/hwmon";

        foreach (var entry in proc.ListDirectory(root))
            if (entry.StartsWith(hwmonPrefix, StringComparison.Ordinal))
                return root + "/" + entry;

        return "";
    }

    /// <summary>The card number in a <c>/sys/class/drm</c> entry name, or <c>null</c> when the entry is not
    /// a card at all. The directory is a mix of cards (<c>card0</c>), their render nodes
    /// (<c>renderD128</c>) and one entry per connector (<c>card0-DP-1</c>) — counting a render node or a
    /// connector as an adapter is how a single GPU turns into several.</summary>
    private static int? CardIndex(string entry) {
        if (!entry.StartsWith(CardPrefix, StringComparison.Ordinal) || entry.Length == CardPrefix.Length)
            return null;

        return int.TryParse(
            entry.AsSpan(CardPrefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var index)
            ? index
            : null;
    }

    /// <summary>The last segment of a resolved symlink target — the PCI address out of a device path, the
    /// module name out of a driver path. "" when the link did not resolve.</summary>
    private static string LastSegment(string? path) {
        if (string.IsNullOrEmpty(path))
            return "";

        var slash = path.LastIndexOf('/');
        return slash < 0 ? path : path[(slash + 1)..];
    }

    private static ulong ParseUInt64(string? text) =>
        text is not null
        && ulong.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;
}
