# Await the view models' startup loads in tests

Work item 92, Sprint 5, *Performance and Dashboard tests race the view model's startup load.*

## Root cause

`PerformanceViewModel` and `DashboardViewModel` start their static hardware loads from the constructor without awaiting them. In the app each continuation resumes on the dispatcher. A test has no synchronization context, so the continuation runs on the thread pool and rebuilds `Resources` or `Cards` while the test enumerates it. The tests waited `Task.Delay(100)` and hoped the load had finished. On a slow runner it had not, which produced the `Collection was modified` failure on main's Debug windows-latest leg.

## What was built

- **`DashboardViewModel.InitialLoad`**: the constructor's five loads (CPU, memory, GPUs, system info, disks) as one `Task.WhenAll`, matching the `PerformanceViewModel.InitialLoad` that #159 added. Every load already catches its own failure, so the task never faults.
- **The three remaining sleeps are gone.** `PerformanceAlertTests`, `DashboardGpuCardTests` and `DashboardRefreshHistoryTests` await `InitialLoad`. The other four Performance sites had already been converted by #159. No test now waits a fixed delay for a view model's startup load.
- **Two pin tests per page** (`PerformanceInitialLoadTests`, `DashboardInitialLoadTests`). The first gates the GPU adapter read on a `TaskCompletionSource`, asserts the task is still pending and no GPU row or card exists yet, then releases the read and asserts the row exists once the task completes. The second asserts that a provider which throws still lets the task complete successfully.

## Decisions

- **The task is internal and only tests read it.** Production still fires the loads and forgets them, exactly as before; the constructor stores the `WhenAll` rather than discarding four separate tasks.
- **`RefreshNow` / `Refresh` are unchanged.** They still re-run the loads fire-and-forget, because the work item scopes the seam to the constructor's loads.

## How to verify

1. `dotnet test` passes, and `grep -rn "Task.Delay(100)" tests/` finds nothing.
2. Mutation check: drop `LoadGpusAsync()` from Dashboard's `InitialLoad` and `DashboardInitialLoadTests.InitialLoad_CompletesOnlyAfterTheGpuCardsAreBuilt` fails.
3. In the app, nothing changes: the Dashboard and Performance pages fill in their cards and rows as they did before.
