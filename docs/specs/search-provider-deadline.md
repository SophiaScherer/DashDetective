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

- Every provider gets `SearchAggregator.ProviderDeadline` (4 s) to answer, via `Task.WaitAsync`. A provider
  that misses it contributes nothing to that result set, and the miss is logged. The other categories
  show as normal.
- Cancellation still returns at once. A superseded term doesn't wait out the deadline.
- `WaitAsync` stops *waiting* but can't stop the provider. A stalled index call keeps a thread-pool
  thread until the OS gives up. Its answer is never read.

## Decisions

- **4 seconds.** The index answers in milliseconds when it's working, and the fallback scan is capped at
  2000 folders. The deadline is there to catch a stall, not a slow answer. A deliberately slow machine
  could lose the Files category for one term; that is the trade-off.
- **Fix it in the aggregator, not in the index.** A deadline in `WindowsSearchIndex` would protect one
  provider. In the aggregator it protects all seven, including any added later.
- **Not progressive results.** Showing fast categories first and merging slow ones in later would remove
  even the 4-second wait. That is a bigger change to the dropdown, so I left it as a suggestion.

## How to verify

Not reproducible on demand. To exercise the path:

1. Stop the Windows Search service (`services.msc` → Windows Search → Stop). The index then *fails*
   rather than stalls, so the fallback scan answers. Search should still work, as before this change.
2. To simulate a stall, pause the service or break the index while a query is in flight. Pages,
   settings and the other categories should show within about 4 seconds, and a new term should keep
   working.
3. The log (`%LocalAppData%\DashDetective\logs`) records "Search provider File did not answer within 4s"
   each time the deadline fires. **If the tester hits the original bug again, that line is the thing to
   look for:** it confirms or rules out this mechanism.
