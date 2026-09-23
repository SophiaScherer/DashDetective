# Search provider deadline

Relates to work item 66, Sprint 4: *Search bar breaks after a new term; recovers only on restart.*
**This is not a confirmed fix for that item.** The bug hasn't been reproduced. This change closes the
one mechanism that code reading turned up which matches every reported symptom.

## The mechanism

`SearchAggregator.QueryAsync` fans each term out to seven providers and merges their answers after
`Task.WhenAll`. That wait had **no deadline**. It returned only once every provider had answered, however
long that took.

The Files provider asks the Windows index through OLE DB (`WindowsSearchIndex`). That call has no
timeout of its own, and it checks the cancellation token only *between* rows, never while
`Open`/`ExecuteReader` is blocked. If the Windows Search service stalls, the call never returns:

- The search in progress sits on "Searching…" and shows nothing, not even pages or settings, which
  answer instantly.
- Each new term starts a fresh index query. If the service is still stuck, that query stalls too, and
  its term also shows nothing.
- Nothing in the app resets it; a restart does. That is the item's "works again on boot up".

That matches the report: search worked, a remembered term ran, a new term showed nothing, and a restart fixed it.

## What was built

- Every provider gets `SearchAggregator.ProviderDeadline` (6 s) to answer, via `Task.WaitAsync`. A provider
  that misses it contributes nothing to that result set, and the miss is logged. The other categories
  show as normal.
- **The Files provider's index gets its own 2 s** (`FileSearchProvider.IndexDeadline`). Past that, it is
  treated as unable to answer, and the capped scan runs instead. The index and the scan run *in turn*
  under the one provider deadline, so without this a stalled index would have used up the scan's time as
  well. With it, a stalled index still leaves the Files category answering, from the scan.
- Cancellation still returns at once. A superseded term doesn't wait out the deadline.
- `WaitAsync` stops *waiting* but can't stop the provider. A stalled index call keeps a thread-pool
  thread until the OS gives up. Its answer is never read.

## Why only a restart cleared it

The index's connection string leaves OLE DB connection pooling on. A connection wedged in the pool
outlives every search, and only a fresh process gets a fresh pool. This is why a restart, and nothing
short of it, brought search back.

## Known limitations

- **A stalled call is abandoned, not stopped.** `WaitAsync` stops *waiting*. The blocked OLE DB call
  keeps its thread-pool thread and its connection until the OS gives up. While the index stays wedged,
  each new term leaves one more behind. Search now *looks* healthy, so more may pile up before a restart
  than did before. **This change fixes what the user sees; the stalled call underneath is unchanged.**
- An abandoned call's eventual exception goes unobserved by the aggregator. `Program.cs`'s
  `UnobservedTaskException` handler logs it when the task is collected, but without the search term.

## Decisions

- **6 seconds overall, 2 for the index.** The index answers in milliseconds when it's working, and the scan
  is capped at 2000 folders. This leaves the scan about 4 s. The deadline is there to catch a stall, not a
  slow answer. A slow network-mounted scope could still lose the Files category for one term; that is the
  trade-off.
- **Fix it in the aggregator, not in the index.** A deadline in `WindowsSearchIndex` would protect one
  provider. In the aggregator it protects all seven, including any added later.
- **Not progressive results.** Showing fast categories first and merging slow ones in later would remove
  even the wait for a stalled provider. That is a bigger change to the dropdown, so I left it as a suggestion.

## How to verify

Not reproducible on demand. To exercise the path:

1. Stop the Windows Search service (`services.msc` → Windows Search → Stop). The index then *fails*
   rather than stalls, so the fallback scan answers. Search should still work, as before this change.
2. To simulate a stall, pause the service or break the index while a query is in flight. Pages,
   settings and the other categories should show at once, files after about 2 seconds (from the scan), and a new term should keep
   working.
3. The log (`%LocalAppData%\DashDetective\logs`) records "Windows index did not answer within 2s; searching the folders instead"
   each time the deadline fires. **If the tester hits the original bug again, that line is the thing to
   look for:** it confirms or rules out this mechanism.
