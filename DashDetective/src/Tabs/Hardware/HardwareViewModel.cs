using System.Collections.Generic;
using System.Threading.Tasks;
using DashDetective.Shared;

namespace DashDetective.Tabs.Hardware;

/// <summary>
/// The Hardware tab: a spec-sheet view of the machine's components — the design comp's 2-column grid
/// of cards (Processor, Graphics, Motherboard, Memory, Storage Devices, Sensors), each an icon-tile
/// header over a key/value spec list. The page scrolls as a whole like the Dashboard, so it is
/// deliberately NOT an <see cref="ISelfScrollingPage"/>.
///
/// The card structure (titles, icons, row keys) is fixed; the values are filled once at startup from
/// <see cref="HardwareInfoProvider"/> (async WMI, off the UI thread — the continuation resumes here to
/// bind), and re-read on demand via <see cref="IRefreshablePage"/>. Any field WMI cannot supply stays
/// the neutral placeholder "—". The <b>Sensors</b> card is intentionally left as placeholders — live
/// thermals/voltages are deferred (see the plan's appendix), so there is no live-sampling
/// (<see cref="ILiveSamplingPage"/>) wiring here.
/// </summary>
public partial class HardwareViewModel : ViewModelBase, IRefreshablePage {
    private readonly HardwareCard _processor;
    private readonly HardwareCard _graphics;
    private readonly HardwareCard _motherboard;
    private readonly HardwareCard _memory;
    private readonly HardwareCard _storage;
    private readonly HardwareCard _sensors;

    /// <summary>The six component cards, in comp order, bound by the view's 2-column grid.</summary>
    public IReadOnlyList<HardwareCard> Cards { get; }

    public HardwareViewModel() {
        _processor = new HardwareCard("Processor", "—", HardwareIcons.Chip,
            HardwareIcons.Blue, HardwareIcons.BlueBg, new[] {
                new HardwareSpec("Cores / Threads"),
                new HardwareSpec("Base / Boost"),
                new HardwareSpec("Cache (L3)"),
                new HardwareSpec("TDP"),
                new HardwareSpec("Socket"),
            });
        _graphics = new HardwareCard("Graphics", "—", HardwareIcons.Graph,
            HardwareIcons.Green, HardwareIcons.GreenBg, new[] {
                new HardwareSpec("Memory"),
                new HardwareSpec("CUDA Cores"),
                new HardwareSpec("Boost Clock"),
                new HardwareSpec("Driver"),
                new HardwareSpec("Bus"),
            });
        _motherboard = new HardwareCard("Motherboard", "—", HardwareIcons.Grid,
            HardwareIcons.Purple, HardwareIcons.PurpleBg, new[] {
                new HardwareSpec("Chipset"),
                new HardwareSpec("BIOS"),
                new HardwareSpec("Form Factor"),
                new HardwareSpec("PCIe Slots"),
                new HardwareSpec("M.2 Slots"),
            });
        _memory = new HardwareCard("Memory", "—", HardwareIcons.Bars,
            HardwareIcons.Yellow, HardwareIcons.YellowBg, new[] {
                new HardwareSpec("Installed"),
                new HardwareSpec("Speed"),
                new HardwareSpec("Timings"),
                new HardwareSpec("Slots used"),
                new HardwareSpec("Voltage"),
            });
        _storage = new HardwareCard("Storage Devices", "—", HardwareIcons.Bars,
            HardwareIcons.Orange, HardwareIcons.OrangeBg, new[] {
                new HardwareSpec("Drive 1"),
                new HardwareSpec("Drive 2"),
                new HardwareSpec("Drive 3"),
                new HardwareSpec("Total Health"),
            });
        _sensors = new HardwareCard("Sensors", "—", HardwareIcons.Graph,
            HardwareIcons.Red, HardwareIcons.RedBg, new[] {
                new HardwareSpec("CPU Package"),
                new HardwareSpec("GPU Core"),
                new HardwareSpec("System"),
                new HardwareSpec("CPU Fan"),
                new HardwareSpec("VCore"),
            });

        Cards = new[] { _processor, _graphics, _motherboard, _memory, _storage, _sensors };

        _ = LoadAsync();
    }

    /// <summary>Toolbar Refresh: re-read the machine (specs can change, e.g. a BIOS update or a drive
    /// swap). Fire-and-forget like the startup load; failures leave the current values in place.</summary>
    public void Refresh() => _ = LoadAsync();

    private async Task LoadAsync() {
        // GetAsync never throws (each section falls back to its .Unknown record), but guard the whole
        // path so a surprise can't take down the app via an unobserved task exception.
        try {
            var info = await HardwareInfoProvider.GetAsync();
            // Constructed on the UI thread, so the continuation resumes there — safe to bind.
            ApplyProcessor(info.Processor);
            ApplyMemory(info.Memory);
            ApplyStorage(info.Storage);
            ApplyMotherboard(info.Motherboard);
            ApplyGraphics(info.Graphics);
            // Sensors card intentionally untouched — deferred, stays "—".
        } catch {
            // Leave whatever values are already shown (placeholders on first load).
        }
    }

    private void ApplyProcessor(ProcessorInfo p) {
        _processor.Subtitle = p.Name;
        _processor.SetRow("Cores / Threads", p.CoresThreads);
        _processor.SetRow("Base / Boost", p.BaseBoost);
        _processor.SetRow("Cache (L3)", p.CacheL3);
        _processor.SetRow("TDP", p.Tdp);
        _processor.SetRow("Socket", p.Socket);
    }

    private void ApplyMemory(MemoryInfo m) {
        _memory.Subtitle = m.Summary;
        _memory.SetRow("Installed", m.Installed);
        _memory.SetRow("Speed", m.Speed);
        _memory.SetRow("Timings", m.Timings);
        _memory.SetRow("Slots used", m.SlotsUsed);
        _memory.SetRow("Voltage", m.Voltage);
    }

    private void ApplyStorage(StorageInfo s) {
        _storage.Subtitle = s.Summary;
        // Variable rows: rebuild one row per detected drive (model → detail) plus a health row. When no
        // drives were found (e.g. the initial stub / a failed read) keep the placeholder rows.
        if (s.Drives.Count == 0)
            return;

        _storage.Rows.Clear();
        foreach (var drive in s.Drives)
            _storage.Rows.Add(new HardwareSpec(drive.Model, drive.Detail));
        _storage.Rows.Add(new HardwareSpec("Total Health", s.TotalHealth));
    }

    private void ApplyMotherboard(MotherboardInfo b) {
        _motherboard.Subtitle = b.Board;
        _motherboard.SetRow("Chipset", b.Chipset);
        _motherboard.SetRow("BIOS", b.Bios);
        _motherboard.SetRow("Form Factor", b.FormFactor);
        _motherboard.SetRow("PCIe Slots", b.PcieSlots);
        _motherboard.SetRow("M.2 Slots", b.M2Slots);
    }

    private void ApplyGraphics(GraphicsInfo g) {
        _graphics.Subtitle = g.Name;
        _graphics.SetRow("Memory", g.Memory);
        _graphics.SetRow("CUDA Cores", g.CudaCores);
        _graphics.SetRow("Boost Clock", g.BoostClock);
        _graphics.SetRow("Driver", g.Driver);
        _graphics.SetRow("Bus", g.Bus);
    }
}
