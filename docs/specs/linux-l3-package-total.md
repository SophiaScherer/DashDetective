# Report the L3 cache as the package total on Linux

Work item 102: *Linux Processor card reports one L3 slice instead of the package total on multi-CCD CPUs.*

## What was wrong

On Linux the Hardware tab's Processor card read the L3 size from `cpu0`'s cache tree alone. On a chiplet
part each CCD has its own L3, and `cpu0` reaches only one of them. A Ryzen 9 5900X has two 32 MB L3s and
showed 32 MB, while the same machine on Windows shows 64 MB, because `Win32_Processor.L3CacheSize` is the
whole package's.

## What was built

- `CpuFacts.L3CacheKilobytes` walks every core in `cpu0`'s package and adds up the distinct L3 instances
  it finds. The card now reads 64 MB on a 5900X and is unchanged on a monolithic part.

## Decisions

- **Instances are told apart by their cache `id`, else by `shared_cpu_list`.** Every core sharing an L3
  reports the same instance, so it is counted once. When a kernel publishes neither file, the cache counts
  once, which is the old reading, rather than once per core.
- **One package, not the machine.** The WMI arm takes the first processor row's L3, so a two-socket board
  reports one socket's cache on both platforms.
- **A cache index whose level cannot be read is skipped.** Only a confirmed L3 is added to the total.
- **The walk stays off the Dashboard.** The total is still a separate static that only the Hardware tab
  calls, so the CPU tile does not pay for reading every core's cache tree.

## How to verify

1. On a Linux machine with a multi-CCD CPU (for example a Ryzen 9 5900X or 7950X), open Hardware →
   Processor. L3 Cache reads the datasheet total (64 MB on a 5900X), not one CCD's 32 MB.
2. Cross-check with `lscpu -C`. On a single-socket machine its L3 row's ALL-SIZE column equals the
   card's figure, and ONE-SIZE is the old, per-CCD reading.
3. On a monolithic part (for example an Intel Core i7), the figure is unchanged from before.
4. In a VM with no cache tree, the row still reads "—".
