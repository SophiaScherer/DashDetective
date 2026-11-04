# Linux per-process CPU divisor

Work item 132: *Linux per-process CPU % is inflated under taskset or a container CPU quota.*

## What was wrong

`LinuxProcessSnapshotProvider` divided each process's CPU time by `Environment.ProcessorCount`. On Linux
that follows the affinity mask and cgroup quota, so DashDetective pinned to two cores of eight divided by
two and showed processes at up to four times their share of the machine.

## What was built

- `OnlineCpuCount` counts the `cpuN` lines of `/proc/stat` through `IProcFileSystem`. The kernel lists
  online CPUs there whatever the caller's affinity or quota.
- Each snapshot re-reads it, so a hot-plugged CPU is picked up. `ProcessorCount` remains the fallback
  when `/proc/stat` is unreadable or has no `cpuN` line.

## Decisions

- **Windows is unchanged.** `WindowsProcessSnapshotProvider` keeps its own divisor.
- **Read per snapshot, not once.** `/proc/stat` is a cheap read and online CPUs can change.

## How to verify

1. Run the `OnlineCpuCount_*` tests.
2. Make `OnlineCpuCount` always return the fallback and the first one fails.
