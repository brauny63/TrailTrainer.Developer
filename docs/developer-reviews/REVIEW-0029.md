# REVIEW-0029 – Automatic Resume Run Orchestrator

## Status
READY FOR REVIEW

## Summary

DEV-0029 adds a provider-neutral bounded orchestrator that executes DEV-0027 batch runs sequentially and uses DEV-0028 as the sole continuation authority. It stops on Finished, ResumeLater, or StopFailed and continues on ContinueImmediately only while its explicit batch-run capacity remains.

## Requirements Implemented

- Added an immutable request requiring a non-null exact DEV-0027 batch-run request and `MaximumBatchRuns > 0`.
- Added the exact Finished, ResumeLater, Failed, and LimitReached orchestration states.
- Added an immutable result requiring at least one batch and decision, equal collection counts, exact pair identity, valid continuation history, terminal state/decision consistency, and exact final flags.
- Result collections preserve exact DEV-0027 and DEV-0028 objects in execution order and expose read-only snapshots.
- Added a mockable asynchronous Core abstraction.
- Added Tasks orchestration depending exactly on `IAutomaticResumeBatchRunner` and `IAutomaticResumeSchedulingDecision`.
- Executes each DEV-0027 run sequentially with the exact request instance and exact caller cancellation token.
- Passes every successful DEV-0027 result exactly once and unchanged to DEV-0028.
- Uses only the DEV-0028 decision state as continuation authority.
- Finished, ResumeLater, and StopFailed terminate immediately without another batch.
- ContinueImmediately starts another batch only while capacity remains.
- Never invokes DEV-0027 more than `MaximumBatchRuns` times.
- Returns LimitReached with ShouldRunAgain true and Immediate true when immediate continuation remains at the safety bound.
- Propagates DEV-0027, DEV-0028, and cancellation exceptions unchanged without retry or conversion to Failed.
- Introduces no DEV-0026, persistence, discovery, filesystem, JSON, Git, GitHub, network, process, polling, retry, delay, clock, timer, scheduling, CLI, background-worker, or concurrent execution behavior.

## Files Created

- `src/TrailTrainer.Developer.Core/AutomaticResumeRunRequest.cs`
- `src/TrailTrainer.Developer.Core/AutomaticResumeRunState.cs`
- `src/TrailTrainer.Developer.Core/AutomaticResumeRunResult.cs`
- `src/TrailTrainer.Developer.Core/IAutomaticResumeRunOrchestrator.cs`
- `src/TrailTrainer.Developer.Tasks/AutomaticResumeRunOrchestrator.cs`
- `tests/TrailTrainer.Developer.Tests/AutomaticResumeRunOrchestratorTests.cs`
- `docs/developer-reviews/REVIEW-0029.md`

## Files Modified

None.

## Files Deleted

None.

## Architecture / Refactoring Notes

Provider-neutral request, state, result, and abstraction types are located in `TrailTrainer.Developer.Core`. The bounded orchestration is located in `TrailTrainer.Developer.Tasks` and composes only the DEV-0027 runner and DEV-0028 decision abstractions. No unrelated refactoring was performed.

## Tests Added

Injected-fake unit tests cover request validation and identity, all result invariants, exact batch/decision correspondence and identities, order and immutable collection exposure, first and later terminal decisions, multiple immediate continuations, one-run and multi-run limits, the hard maximum guarantee, exact request/token/result delegation, call order and sequential execution, first and later DEV-0027/DEV-0028 exceptions, no retry, pre-cancellation, later cancellation, and prevention of subsequent decisions and batches.

All existing DEV-0002 through DEV-0028 tests continue to pass.

## Verification

### dotnet build

Succeeded for the complete solution: 0 warnings, 0 errors.

### dotnet test

Succeeded for the complete solution: 574 passed, 0 failed, 0 skipped.

### git diff --check

Succeeded with no whitespace errors.

## Deviations from DEV-0029

None.

## Open Issues / Known Limitations

None.

## Commit and Push
No commit created.
No push performed.
