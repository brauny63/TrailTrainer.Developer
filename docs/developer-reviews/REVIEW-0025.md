# REVIEW-0025 – Automatic Persisted Lifecycle Resume

## Status
READY FOR REVIEW

## Summary

DEV-0025 connects automatic resume-candidate selection from DEV-0024 with persisted lifecycle resume execution from DEV-0020. One invocation selects exactly once and either returns NotFound without resuming or resumes the exact selected TaskId once and maps the DEV-0020 outcome.

## Requirements Implemented

- Added an immutable automatic-resume request that preserves merge and remote-branch deletion options exactly and does not expose TaskId.
- Added the exact NotFound, Pending, Failed, and Completed result states.
- Added an immutable result with candidate/resume state invariants and exact nested object identity preservation.
- Added a mockable asynchronous Core abstraction.
- Added Tasks orchestration depending only on `IAutomaticResumeCandidateSelector` and `IPersistedDeveloperLifecycle`.
- Validates a null request before candidate selection.
- Calls candidate selection exactly once with the exact caller cancellation token.
- Returns NotFound with the exact candidate and does not invoke DEV-0020 when no candidate exists.
- Uses exactly `Candidate.ResumeTarget.TaskId` and delegates all caller merge/delete options unchanged.
- Invokes DEV-0020 exactly once after a Found candidate with the exact cancellation token.
- Maps Pending, Failed, and Completed while preserving exact candidate and resume result identities.
- Treats DEV-0020 NotFound after a Found candidate as a clear race/inconsistency failure without reselection, retry, or second resume.
- Propagates selector, DEV-0020, and cancellation failures without converting them into lifecycle states.
- Introduces no direct persistence mutation, discovery, DEV-0022, filesystem, JSON, Git, GitHub, process, polling, delay, retry, scheduling, or background behavior.

## Files Created

- `src/TrailTrainer.Developer.Core/AutomaticPersistedLifecycleResumeRequest.cs`
- `src/TrailTrainer.Developer.Core/AutomaticPersistedLifecycleResumeState.cs`
- `src/TrailTrainer.Developer.Core/AutomaticPersistedLifecycleResumeResult.cs`
- `src/TrailTrainer.Developer.Core/IAutomaticPersistedLifecycleResumer.cs`
- `src/TrailTrainer.Developer.Tasks/AutomaticPersistedLifecycleResumer.cs`
- `tests/TrailTrainer.Developer.Tests/AutomaticPersistedLifecycleResumerTests.cs`
- `docs/developer-reviews/REVIEW-0025.md`

## Files Modified

None.

## Files Deleted

None.

## Architecture / Refactoring Notes

Provider-neutral request, state, result, and abstraction types are located in `TrailTrainer.Developer.Core`. The orchestration is located in `TrailTrainer.Developer.Tasks` and composes only the existing DEV-0024 and DEV-0020 abstractions. No unrelated refactoring was performed.

## Tests Added

Fake-only unit tests cover exact request value preservation, DEV-0020-compatible merge-enum behavior, result invariants and identity preservation, null-request validation, single selection and resume invocation order, exact cancellation-token delegation, NotFound short-circuiting, exact selected TaskId and option delegation, all outcome mappings, race handling, exception propagation, cancellation propagation, and absence of retry/reselection behavior.

All existing DEV-0002 through DEV-0024 tests continue to pass.

## Verification

### dotnet build

Succeeded for the complete solution: 0 warnings, 0 errors.

### dotnet test

Succeeded for the complete solution: 488 passed, 0 failed, 0 skipped.

### git diff --check

Succeeded with no whitespace errors.

## Deviations from DEV-0025

None.

## Open Issues / Known Limitations

None.

## Commit and Push
No commit created.
No push performed.
