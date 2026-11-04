# Dispose guard for the PDH samplers

Work item 130: *WindowsPhysicalDiskThroughputSampler and WindowsLogicalProcessorSampler are not safe to dispose twice.*

## What was wrong

Both samplers documented `Dispose` as safe to call more than once, but kept no disposed state. A second
`Dispose` closed the same PDH query handle again, and `Sample` after `Dispose` collected on a closed
query. `WindowsGpuUsageSampler` already carried the guard.

## What was built

- A `_disposed` flag on each sampler. `Dispose` closes the query once; `Sample` returns an empty set
  when disposed, without touching PDH.
- An `internal IsDisposed` property so the contract is testable through `SamplerInit.Inert`.

## Decisions

- **The constructors keep their platform annotation and nothing else changes.** The inert path stays
  callable on every platform, which is what the tests use.
- **No handle zeroing.** The flag alone is the guard, matching `WindowsGpuUsageSampler`.

## How to verify

1. Run the `Dispose_Twice_IsSafeAndSampleAfterwardsIsEmpty` tests for both samplers.
2. Setting `_disposed = false` in `Dispose` makes them fail.
