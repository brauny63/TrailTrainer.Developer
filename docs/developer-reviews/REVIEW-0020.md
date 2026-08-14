# REVIEW-0020 – Persisted Lifecycle Resume Integration

## Status
READY FOR REVIEW

## Summary

Implemented provider-neutral orchestration connecting DEV-0017 lifecycle execution, DEV-0018 lifecycle resume, and DEV-0019 state persistence. Pending initial lifecycles are persisted; resumed Pending and Failed states are retained; state is deleted only after a successfully completed resume.

## Requirements Implemented

- Added validated immutable Start and Resume request models.
- Added invariant-enforced Start and Resume result models.
- Added `PersistedDeveloperLifecycleResumeState` with exactly NotFound, Pending, Failed, and Completed.
- Added mockable asynchronous `IPersistedDeveloperLifecycle` abstraction.
- Added minimal provider-neutral `IUtcClock` and production `SystemUtcClock`.
- Added Tasks orchestration using only injected DEV-0017, DEV-0018, DEV-0019, and clock abstractions.
- Start delegates all DEV-0017 inputs exactly and invokes the lifecycle once.
- Pending Start derives PR number and feature branch solely from the exact DEV-0017 workflow result.
- Pending Start derives repository directory/identity, base branch, and remote from original request inputs.
- Pending Start constructs state with exact TaskId, optional task path, and injected UTC time, then saves exactly once.
- Failed and Completed Start return without Save or Delete.
- Resume loads by exact TaskId before invoking DEV-0018.
- Missing state returns NotFound without resume or delete.
- Ordinal TaskId mismatch is rejected before resume/delete.
- Resume passes the exact loaded resume-context object plus exact merge/delete inputs to DEV-0018.
- Pending and Failed Resume retain the exact persisted state without Save or Delete.
- Completed Resume deletes by exact TaskId only after DEV-0018 completes, then retains loaded state in the returned result for observability.
- Save/load/resume/delete failures and cancellation short-circuit subsequent work without retry or rollback.
- Adds no filesystem implementation, JSON, HTTP, Git, process, shell, polling, delay, retry, scheduling, or provider logic to Tasks.

## Files Created

- `src/TrailTrainer.Developer.Core/IPersistedDeveloperLifecycle.cs`
- `src/TrailTrainer.Developer.Core/IUtcClock.cs`
- `src/TrailTrainer.Developer.Core/PersistedDeveloperLifecycleResumeRequest.cs`
- `src/TrailTrainer.Developer.Core/PersistedDeveloperLifecycleResumeResult.cs`
- `src/TrailTrainer.Developer.Core/PersistedDeveloperLifecycleResumeState.cs`
- `src/TrailTrainer.Developer.Core/PersistedDeveloperLifecycleStartRequest.cs`
- `src/TrailTrainer.Developer.Core/PersistedDeveloperLifecycleStartResult.cs`
- `src/TrailTrainer.Developer.Tasks/PersistedDeveloperLifecycle.cs`
- `src/TrailTrainer.Developer.Tasks/SystemUtcClock.cs`
- `tests/TrailTrainer.Developer.Tests/PersistedDeveloperLifecycleTests.cs`
- `docs/developer-reviews/REVIEW-0020.md`

## Files Modified

None.

## Files Deleted

None.

## Architecture / Refactoring Notes

All public integration contracts and models are provider-neutral and reside in Core. Tasks contains only orchestration and a minimal system clock adapter. Persistence mechanics remain exclusively in DEV-0019's Persistence project; lifecycle, resume, status, merge, and cleanup semantics remain owned by their existing abstractions.

## Tests Added

- Start/Resume request validation and null request short-circuiting.
- Start/Resume result invariants for every supported state.
- Exact DEV-0017 delegation, single invocation, ordering, and cancellation token.
- Pending state derivation for authoritative PR number and workflow completion feature branch.
- Exact original request inputs, TaskId, optional task path, injected timestamp, and saved-state identity.
- Failed/Completed non-persistence Start paths.
- Lifecycle and Save failure propagation without Save retry.
- Non-UTC clock rejection before Save.
- Exact Load TaskId/token and NotFound behavior.
- Loaded TaskId mismatch rejection before resume/delete.
- Exact loaded context identity plus merge inputs and token delegation to DEV-0018.
- Pending/Failed state retention without Save/Delete.
- Completed delete ordering, exact TaskId/token, exact result identities, and retained loaded state.
- Load, resume, and delete failure short-circuiting without retries or repeated resume.
- Cancellation/failure at every dependency boundary preventing subsequent work.
- The full existing regression suite remains passing.

## Verification

### dotnet build

Successful. 0 warnings, 0 errors.

### dotnet test

Successful. 395 passed, 0 failed, 0 skipped.

### git diff --check

Successful. No whitespace errors.

## Deviations from DEV-0020

None.

## Open Issues / Known Limitations

None.

## Commit and Push
No commit created.
No push performed.
