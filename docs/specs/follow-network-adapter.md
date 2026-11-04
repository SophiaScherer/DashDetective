# Dashboard and Performance follow the sampler's network adapter

Work item 113, Sprint 10: *Dashboard and Performance keep the first network adapter's name, link and errors.*

## What was wrong

`NetworkUsageSampler` re-picks its adapter when another one carries the traffic and reports it through
`AdapterName`. Both pages read that name and the interface once at construction, so after a switch the
Dashboard caption, the Performance row name, its "View in Network" target, the Link tile and the Errors tile
all described the old adapter. With no adapter at startup the Errors tile read a confident `0`.

## What was built

- The Dashboard Network caption updates from the service on each network tick.
- The Performance Network row, on the first tick after the name changes, renames itself, retargets its
  link, re-reads the link speed and description, and swaps the interface the Errors tile reads.
- `NetworkUsageSampler.FindByName` resolves the sampler's adapter by its friendly name.
- The Errors tile shows a dash when there is no readable adapter.

## Decisions

- **The row's saved-order id does not change.** `ResourceRow.WidgetId` keeps the name the row was built
  with, so a renamed row keeps its place in a saved rail order.
- **No new event on the shared service.** The pages compare the name on the tick they already receive.

## How to verify

1. Run `DashboardNetworkAdapterTests` and `PerformanceNetworkAdapterTests`.
2. On a machine with two active adapters, move traffic from one to the other: the caption and row follow.
