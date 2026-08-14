# REVIEW-0022 – Persisted Lifecycle Selection / Resume Targeting

## Status
READY FOR REVIEW

## Summary

Implemented provider-neutral selection of one persisted lifecycle state from DEV-0021 discovery results. Callers can select by exact ordinal TaskId, oldest timestamp, or newest timestamp and receive either the exact selected state or a clear NotFound result.

## Requirements Implemented

- Added `PersistedLifecycleSelectionMode` with exactly ExactTaskId, Oldest, and Newest.
- Added immutable validated selection request with mode-specific TaskId invariants and unsupported-enum rejection.
- Added `PersistedLifecycleSelectionState` with exactly Found and NotFound.
- Added immutable selection result with Found/NotFound state invariants.
- Added immutable resume target validating non-empty and ordinal-consistent TaskId against the exact persisted state.
- Added mockable asynchronous `IPersistedLifecycleSelector` abstraction.
- Added `PersistedLifecycleSelector` in Tasks with only `IDeveloperLifecycleStateDiscovery` injected.
- Calls discovery exactly once per selection and propagates the exact cancellation token.
- Exact selection uses ordinal comparison, preserves object identity, returns NotFound when absent, and fails on duplicate matches.
- Oldest selection uses earliest `SavedAtUtc`, then lowest ordinal TaskId.
- Newest selection uses latest `SavedAtUtc`, then highest ordinal TaskId.
- Oldest/Newest correctness is independent of discovery ordering.
- Empty discovery returns NotFound for every applicable mode.
- Discovery failures and cancellation propagate without retry or conversion to NotFound.
- Selector cannot mutate state or invoke DEV-0020 Start/Resume because discovery is its sole dependency.
- Adds no filesystem, JSON, Git, GitHub, HTTP, process, shell, polling, delay, retry, scheduling, background, CLI, or fuzzy-selection behavior.

## Files Created

- `src/TrailTrainer.Developer.Core/IPersistedLifecycleSelector.cs`
- `src/TrailTrainer.Developer.Core/PersistedLifecycleResumeTarget.cs`
- `src/TrailTrainer.Developer.Core/PersistedLifecycleSelectionMode.cs`
- `src/TrailTrainer.Developer.Core/PersistedLifecycleSelectionRequest.cs`
- `src/TrailTrainer.Developer.Core/PersistedLifecycleSelectionResult.cs`
- `src/TrailTrainer.Developer.Core/PersistedLifecycleSelectionState.cs`
- `src/TrailTrainer.Developer.Tasks/PersistedLifecycleSelector.cs`
- `tests/TrailTrainer.Developer.Tests/PersistedLifecycleSelectorTests.cs`
- `docs/developer-reviews/REVIEW-0022.md`

## Files Modified

None.

## Files Deleted

None.

## Architecture / Refactoring Notes

All contracts and models remain provider-neutral in Core. Tasks performs only in-memory selection over the injected DEV-0021 discovery abstraction. No persistence implementation or lifecycle orchestration dependency is exposed to the selector.

## Tests Added

- Exact mode missing, empty, and whitespace TaskId validation.
- Oldest/Newest non-null TaskId rejection and unsupported mode rejection.
- Null request rejection before discovery.
- Found/NotFound result invariants and unsupported result state.
- Resume-target empty/null/mismatch validation and exact state identity.
- Exactly one discovery call and exact cancellation token.
- Discovery exception propagation without retry.
- Exact ordinal, case-distinct, missing, duplicate, ordering-independent, and identity-preserving selection.
- Empty Oldest/Newest discovery NotFound results.
- Oldest timestamp and lowest-ordinal tie-break independent of input order.
- Newest timestamp and highest-ordinal tie-break independent of input order.
- Pre-cancelled discovery propagation without NotFound conversion.
- The selector's discovery-only dependency structurally excludes Store mutation and DEV-0020 Start/Resume calls.
- The full existing regression suite remains passing.

## Verification

### dotnet build

Successful. 0 warnings, 0 errors.

### dotnet test

Successful. 440 passed, 0 failed, 0 skipped.

### git diff --check

Successful. No whitespace errors.

## Deviations from DEV-0022

None.

## Open Issues / Known Limitations

None.

## Commit and Push
No commit created.
No push performed.
