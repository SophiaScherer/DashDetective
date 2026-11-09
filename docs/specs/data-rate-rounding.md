# Data rates round before they pick a unit

Work item 125.

## What was wrong

`DataRateFormatter` chose kbps, Mbps or Gbps from the raw value and rounded afterward. 999.6 Mbps
therefore read "1000 Mbps" and 0.9996 Mbps read "1000 kbps". NaN printed "NaN kbps".
`ByteRateFormatter` had fixed the same thing for disk rates.

## What was built

- `Format`, `Split` and `SplitPair` choose the unit from the value as it will be displayed, promoting
  while the rounded number reaches 1000, so 999.6 Mbps reads "1.0 Gbps".
- `SplitPair` promotes from the larger side, so a pair still shares one unit.
- NaN and infinity read as zero.

## Decisions

- **`UnitFor` is unchanged.** It stays the raw magnitude rule that callers use to pick a shared axis unit.
- **The rounding mirrors `FormatValue`:** `Math.Round` at 10 and above, which is what the reader sees.

## How to verify

1. With a link whose throughput lands between 999.5 and 1000 Mbps, the readout shows "1.0 Gbps".
2. A value just under 1 Mbps in the same band shows "1.0 Mbps".
