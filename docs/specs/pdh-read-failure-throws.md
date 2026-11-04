# A failed read on a working sampler throws

Work item 127: *ProcessorUtilityCpuSampler and WindowsMemoryUsageSampler return 0 when a read fails.*

## What was wrong

Once a sampler was up, a failed `PdhCollectQueryData` / `PdhGetFormattedCounterValue` (CPU) or
`GlobalMemoryStatusEx` (memory) returned a zero reading. `MetricChannel` only shows the placeholder when a
sampler throws, so the Dashboard sat on a confident 0% for the session.

## What was built

- `ProcessorUtilityCpuSampler.Interpret` turns the two PDH statuses plus the raw value into a clamped
  reading, and throws `InvalidOperationException` on any failure.
- `WindowsMemoryUsageSampler.EnsureRead` throws when `GlobalMemoryStatusEx` reports failure.

## Decisions

- **The inert-at-start zero contract is unchanged.** A sampler that never became ready, the latched-inert
  memory sampler and the `Unsupported*` arms still return zero; `CpuUsageSamplerTests` pins it.
- **Only a read failure after the sampler was ready throws.** A DLL load failure still latches inert.

## How to verify

1. Run `Interpret_FailedRead_Throws` and `EnsureRead_Failure_Throws`.
2. Make `Interpret` stop throwing and they fail.
