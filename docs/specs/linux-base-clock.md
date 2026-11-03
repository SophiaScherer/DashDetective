# Linux base clock

Work item 97, Sprint 10, *Linux base clock shows the maximum turbo frequency on intel_pstate and amd-pstate.*

## Problem

`CpuFacts` took the processor's clock from `cpufreq`'s `cpuinfo_max_freq`, and the Hardware tab's Processor card passed it to the Base side of its "Base / Boost" row. Under intel_pstate and amd-pstate that file is the maximum turbo frequency, so a Ryzen 5 7600X read "5.3 / 5.3 GHz". Because the machine's reading wins over the datasheet, the catalog's real base (4.7 GHz) never showed. The Dashboard caption and the CPU card subtitles on the Dashboard and Performance tabs carried the same turbo figure, where on Windows they show WMI's `MaxClockSpeed`, which is the rated base.

## What was built

- **`CpuFacts.MaxClockMhz` is now `BaseClockMhz`**, and `BaseClockMhzOf` reads, in order:
  1. `/sys/devices/system/cpu/cpuN/cpufreq/base_frequency` (kHz), which intel_pstate publishes when the processor has HWP (Skylake and later).
  2. `/sys/devices/system/cpu/cpuN/acpi_cppc/nominal_freq` (MHz), ACPI CPPC's nominal frequency, which is the nominal frequency amd-pstate works from. A 0 there means the firmware does not implement the register and is skipped.
  3. The clock in the model name ("… CPU @ 3.60GHz"), which is the rated base by definition.
- Each sysfs source takes the highest value across the online cores, so a hybrid chip reports its performance cores' base.
- With none of them, the base is 0. The Processor card then shows the catalog's rated base (from Work item 96's datasheet fallback), or "—" for a part the catalog does not list. The Dashboard caption drops its "@ clock" suffix, and the CPU card subtitles drop the clock.
- `CpuStaticInfo.MaxClockMhz` keeps its name: it is shared with the Windows arm, where it mirrors WMI's field.

## Decisions

- **`cpuinfo_max_freq` is never used as the base.** Under the two pstate drivers it is the turbo ceiling. Under acpi-cpufreq it is the highest exposed P-state, which may or may not be the base depending on the platform. Neither is the base by definition, so it is not used at all.
- **There is no `amd_pstate_nominal_freq` file.** The amd-pstate documentation lists `amd_pstate_highest_perf`, `amd_pstate_max_freq` and `amd_pstate_lowest_nonlinear_freq` and no nominal one. The nominal frequency is ACPI CPPC's, published per core under `acpi_cppc/` in MHz (see the kernel's `cppc_sysfs` documentation), and is world-readable.
- **The model name's clock stays a source.** It is the rated base the vendor printed in the name, not a reading that means something else, and it is the only source a VM guest has.
- **`cpu MHz` is still never used.** It is the instantaneous clock.

## How to verify

1. Run the suite. `CpuFactsTests` (`Read_PrefersIntelPstateBaseFrequency`, `Read_TakesTheHighestBaseFrequencyAcrossCores`, `Read_WithNoBaseFrequency_UsesTheCppcNominalFrequency`, `Read_CppcNominalFrequency_WinsOverTheModelNameClock`, `Read_ZeroCppcNominalFrequency_FallsThroughToTheModelName`, `Read_NeverUsesCpuinfoMaxFreqAsTheBase`, `Read_UnparsableBaseFrequency_IsSkipped`), `LinuxProcessorInfoProviderTests` and `LinuxCpuInfoProviderTests` run against canned fixtures on every CI leg.
2. On an intel_pstate machine, `cat /sys/devices/system/cpu/cpu0/cpufreq/base_frequency`. The Processor card's Base side and the Dashboard caption match it.
3. On an amd-pstate machine, `cat /sys/devices/system/cpu/cpu0/acpi_cppc/nominal_freq`. The Base side matches it, and the Boost side is the catalog's rated boost.
