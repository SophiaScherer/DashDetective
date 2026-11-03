# Linux GPU names and datasheet fallback

Work item 96, Sprint 5, *Name Linux GPUs by product and fill empty Hardware rows from part datasheets.*

## Problem

On Linux the kernel identifies a graphics adapter by its PCI ids and driver only, so the Hardware tab's Graphics card read `NVIDIA nvidia (10de:2504)`. That string matched nothing in the bundled spec catalog, which is keyed by model tokens, so the CUDA, boost and bus rows were blank on Linux while the same lookup filled them on Windows. The Processor card had the same gap in a VM: a guest has no `cpufreq` policy and no `cache/index*` tree, so base clock and L3 read "—" beside a processor the page had already named correctly. Its socket always read "—", because SMBIOS type 4 needs root.

## What was built

- **`PciIdDatabase`** (`Services/Platform/Linux`) reads the host's `pci.ids` from the first of the standard hwdata/pciutils paths. It keeps only the (vendor, device) pairs asked for, skips subsystem lines (two tabs in) so a card is never named after its board partner, and stops at the `C xx` device-class section. If no table is present, every lookup misses.
- **`DrmCardFacts.ReadNamed`** is the same walk as `Read` with the product name resolved. `AdapterName` uses that name when one was found ("NVIDIA GeForce RTX 3060 Lite Hash Rate") and the old PCI-id composition otherwise. The Hardware Graphics card and the adapter enumeration behind the Dashboard and Performance cards call it. The per-tick sampler and sensor readers keep plain `Read`.
- **`CpuSpec`** gains `Base`, `CacheL3` and `Socket` for every catalog part, taken from the vendors' published datasheets. `ProcessorSpecFormatter` takes the machine's reading first and uses the catalog only for an empty row. It treats a catalog "—" as absent.

## Decisions

- **The machine's reading is never replaced.** The datasheet fills only an empty row. On Windows that covers WMI fields the firmware left blank. On Linux it covers the socket, which has no rootless source, and the VM case.
- **The table comes from the host, not the app.** No copy of `pci.ids` is shipped, so its GPL-2.0+/BSD-3 dual licensing never comes into play. The cost is that a minimal host without `hwdata` keeps the PCI-id name.
- **The name is the bracketed product, not the chip.** `pci.ids` writes a device as "GA106 [GeForce RTX 3060 Lite Hash Rate]", and `DrmCardFacts.MarketingName` keeps only what is inside the brackets, so the card reads "NVIDIA GeForce RTX 3060 Lite Hash Rate", the way Windows names the same part. An entry with no bracket ("SVGA II Adapter") or an empty one is used whole. The bundled vendor word is still prefixed. A bracket listing several models keeps all of them ("AMD Radeon RX 6600/6600 XT/6600M"), which is what the next rule reads.
- **A name covering several models gets no catalog lookup.** `pci.ids` often lists every model a chip ships as ("Navi 32 [Radeon RX 7700 XT / 7800 XT]"), and the catalog's token match would fill a 7800 XT's rows with the 7700 XT's datasheet. Any `/` in the product name skips the lookup, so those rows read "—".
- **`DrmCardFacts.Key` is untouched.** The GPU inventory keeps only adapters that both the enumeration and the sampler report, and a test pins that naming a card does not move its token.

## How to verify

1. Run the suite. `PciIdDatabaseTests`, `DrmCardFactsTests`, `LinuxGraphicsInfoProviderTests`, `LinuxGpuAdapterProviderTests`, `ProcessorSpecFormatterTests`, `LinuxProcessorInfoProviderTests` and `HardwareCatalogTests` all run on every CI leg against canned fixtures.
2. On a Linux machine with `hwdata` installed, open Hardware. The Graphics card's title matches `lspci`'s name for the adapter, and for a catalog part the CUDA, boost and bus rows are filled.
3. In a Linux VM whose host CPU is in the catalog (for example a Ryzen 5 7600X), the Processor card shows the base clock, the L3 size and "AM5" rather than "—".
4. On Windows, the Processor card's socket still shows WMI's own designation (for example "AM5" from the firmware), not the catalog's.
