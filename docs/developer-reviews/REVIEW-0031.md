# REVIEW-0031 – Repeated Delayed Automatic Resume Loop

## Status
READY FOR REVIEW

## Summary

DEV-0031 adds a provider-neutral bounded repeated delayed-resume executor. It executes DEV-0029 sequentially, delays only after ResumeLater while capacity remains, and stops on Finished, Failed, LimitReached, or the explicit maximum run count.

## Requirements Implemented

- Added an immutable request validating a non-null exact DEV-0029 request, strictly positive ResumeDelay, and `MaximumRuns > 0`.
- Added the exact Finished, Failed, ImmediateWorkRemaining, DelayedWorkRemaining, and RunLimitReached states.
- Added an immutable result requiring at least one non-null run, validating DelayCount bounds, continuation history, terminal run state, flags, and normal delay/run correspondence.
- Result preserves exact DEV-0029 result instances in order and exposes a read-only snapshot.
- Added a mockable asynchronous Core abstraction.
- Added Tasks orchestration depending exactly on `IAutomaticResumeRunOrchestrator` and `IAsyncDelay`.
- Uses only `AutomaticResumeRunResult.State` as continuation authority.
- Finished stops immediately as Finished.
- Failed stops immediately as Failed without retry.
- LimitReached stops immediately as ImmediateWorkRemaining with ShouldRunAgain and Immediate true.
- ResumeLater delays once and executes another run only while capacity remains.
- Returns RunLimitReached with ShouldRunAgain true and Immediate false when ResumeLater remains at the bound.
- Delegates the exact RunRequest instance, ResumeDelay, and caller cancellation token on every operation.
- Preserves exact returned run objects and strict run/delay ordering.
- Guarantees no more than `MaximumRuns` DEV-0029 invocations and no more than `MaximumRuns - 1` delays.
- Propagates run, delay, and cancellation failures unchanged without retry or later operation.
- Introduces no direct Task.Delay, DEV-0027/DEV-0028, persistence, discovery, filesystem, JSON, Git, GitHub, network, process, CLI, hosted-service, background-worker, retry, or concurrent behavior.

## Files Created

- `src/TrailTrainer.Developer.Core/RepeatedDelayedAutomaticResumeRequest.cs`
- `src/TrailTrainer.Developer.Core/RepeatedDelayedAutomaticResumeState.cs`
- `src/TrailTrainer.Developer.Core/RepeatedDelayedAutomaticResumeResult.cs`
- `src/TrailTrainer.Developer.Core/IRepeatedDelayedAutomaticResumeExecutor.cs`
- `src/TrailTrainer.Developer.Tasks/RepeatedDelayedAutomaticResumeExecutor.cs`
- `tests/TrailTrainer.Developer.Tests/RepeatedDelayedAutomaticResumeExecutorTests.cs`
- `docs/developer-reviews/REVIEW-0031.md`

## Files Modified

None.

## Files Deleted

None.

## Architecture / Refactoring Notes

Provider-neutral request, state, result, and abstraction types are located in `TrailTrainer.Developer.Core`. The bounded repeated orchestration is located in `TrailTrainer.Developer.Tasks` and composes only the DEV-0029 orchestrator and DEV-0030 delay abstraction. No unrelated refactoring was performed.

## Tests Added

Injected-fake unit tests cover request validation and identity, DelayCount bounds, all result terminal invariants, DelayedWorkRemaining model validity, exact identities/order and immutable collection exposure, terminal first runs, repeated ResumeLater sequences, terminal states after delays, exact run and delay delegation, strict operation ordering and sequentiality, MaximumRuns values 1/2/5, hard run/delay bounds, first and later run exceptions, first and later delay exceptions, no retry, pre-cancellation, delay cancellation, later-run cancellation, and exact constructor dependencies. No orchestration test waits in real time.

All existing DEV-0002 through DEV-0030 tests continue to pass.

## Verification

### dotnet build

Succeeded for the complete solution: 0 warnings, 0 errors.

### dotnet test

Succeeded for the complete solution: 626 passed, 0 failed, 0 skipped.

### git diff --check

Succeeded with no whitespace errors.

## Deviations from DEV-0031

None.

## Open Issues / Known Limitations

None.

## Commit and Push
No commit created.
No push performed.
