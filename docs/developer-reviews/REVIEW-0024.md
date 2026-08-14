# REVIEW-0024 – Automatic Resume Candidate Selection

## Status
READY FOR REVIEW

## Summary

DEV-0024 adds a provider-neutral automatic resume-candidate policy. It discovers persisted lifecycle states exactly once, selects the oldest state with an ordinal TaskId tie-break, and returns either a validated Found result with an identity-preserving resume target or a normal NotFound result.

## Requirements Implemented

- Added `AutomaticResumeCandidateState` with exactly `Found` and `NotFound`.
- Added immutable `AutomaticResumeCandidateResult` with validation of all Found, NotFound, identity, TaskId, and enum-state invariants.
- Added mockable asynchronous `IAutomaticResumeCandidateSelector` abstraction.
- Added `AutomaticResumeCandidateSelector` depending only on `IDeveloperLifecycleStateDiscovery`.
- Discovery is called exactly once with the caller's exact cancellation token.
- Empty discovery results return NotFound with null state and target.
- Selection uses earliest `SavedAtUtc`, then ordinal TaskId ordering, independently of discovery order.
- The selected state object is preserved exactly in both the result and `PersistedLifecycleResumeTarget`.
- Discovery errors and cancellation propagate without retry or conversion to NotFound.
- A null discovery collection fails clearly as a discovery contract violation.
- No persistence mutation, filesystem, JSON, Git, GitHub, process, polling, scheduling, retry, DEV-0020, or DEV-0023 behavior was introduced.

## Files Created

- `src/TrailTrainer.Developer.Core/AutomaticResumeCandidateState.cs`
- `src/TrailTrainer.Developer.Core/AutomaticResumeCandidateResult.cs`
- `src/TrailTrainer.Developer.Core/IAutomaticResumeCandidateSelector.cs`
- `src/TrailTrainer.Developer.Tasks/AutomaticResumeCandidateSelector.cs`
- `tests/TrailTrainer.Developer.Tests/AutomaticResumeCandidateSelectorTests.cs`
- `docs/developer-reviews/REVIEW-0024.md`

## Files Modified

None.

## Files Deleted

None.

## Architecture / Refactoring Notes

Provider-neutral types and the abstraction are in `TrailTrainer.Developer.Core`. The automatic selection policy is in `TrailTrainer.Developer.Tasks` and reuses the existing DEV-0021 discovery abstraction and DEV-0022 resume-target model. No unrelated refactoring was performed.

## Tests Added

Fake-only unit tests cover result invariants, unsupported enum values, exact discovery invocation and cancellation-token delegation, error and cancellation propagation without retry, null and empty discovery output, single-state identity preservation, oldest-first selection, ordinal TaskId tie-breaking including case-distinct IDs, discovery-order independence, and correct resume-target construction.

All existing DEV-0002 through DEV-0023 tests continue to pass.

## Verification

### dotnet build

Succeeded for the complete solution: 0 warnings, 0 errors.

### dotnet test

Succeeded for the complete solution: 471 passed, 0 failed, 0 skipped.

### git diff --check

Succeeded with no whitespace errors.

## Deviations from DEV-0024

None.

## Open Issues / Known Limitations

None.

## Commit and Push
No commit created.
No push performed.
