# Show a dash until a disk or GPU reports

Work item 105: *Disk and GPU cards show a confident 0 when their sampler returns no data.*

## What was wrong

The Dashboard's disk and GPU cards started on the card's default `"0"`. The Performance tab's disk rows
started on `"0"`, `"0 %"`, `"0 B/s"` and `"0 ms"`. The page-local samplers overwrite a readout only when
they return a sample for that device, so a sampler that returns nothing left the zeros in place. That
happens when the PDH query goes inert, on a platform without a per-disk sampler, and on a first sample
that has no rate yet. The page then claimed an idle drive or GPU.

## What was built

- Every Dashboard GPU and disk card is built on `Placeholders.NoReading` with no unit.
- Every Performance disk row and its Active, Read, Write and Response tiles are built on the placeholder.
- The first real sample writes the value and restores the `%` unit.

## Decisions

- **A real 0 stays a 0.** Only the starting value changed. A disk that reports 0 % still shows `0 %`.
- **The unit goes with the dash**, so a card reads "—" and not "— %". The GPU readouts already worked
  this way.
- **`DashboardCard`'s own default is unchanged.** The CPU, Memory and Network cards are filled by the
  shared feed, which has its own failure path. Changing the default would touch cards this item is not
  about.

## How to verify

1. Open the Dashboard on a normal Windows machine. Each disk and GPU card shows "—" for at most one tick,
   then a percentage with its `%`.
2. Open Performance and select a disk. The row and its four tiles fill in on the first tick.
3. A disk with no activity reads `0 %`, not "—".
4. On a platform with no per-disk sampler (macOS), every disk card, row and tile stays "—".
