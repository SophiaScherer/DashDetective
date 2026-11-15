# Failure placeholders clear units and companion readouts

Work item 134, Sprint 10: *Failure placeholders leave units and companion readouts looking live.*

## What was wrong

When the CPU, memory or network sampler failed, the Dashboard replaced the value with a dash but left the
card's unit, so it read "— %", "— GB" or "— Mbps", and left the companion readouts (memory ratio, network
upload) showing the last good reading. On Performance, a CPU failure blanked only Utilization, so the
Processes, Threads, Handles and Up time tiles froze or stayed at their initial zeros.

## What was built

- Dashboard `OnCpuFailed`, `OnMemoryFailed` and `OnNetworkFailed` clear the card unit, as the GPU path
  already does, and the memory header and network sub-line. The success path sets the unit again.
- Performance `OnCpuFailed` sets the four tiles that ride the CPU tick to the placeholder.

## Decisions

- **The unit lives with the value.** A card with no reading shows a bare dash.
- **Performance memory and network rails are unchanged.** Only the CPU tiles were reported as stale.

## How to verify

1. Run the unit tests `DashboardFailurePlaceholderTests` and `PerformanceFailurePlaceholderTests`.
