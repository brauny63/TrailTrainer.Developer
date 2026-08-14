# REVIEW-0023 – Selected Persisted Lifecycle Resume

## Status
READY FOR REVIEW

## Summary

Implemented a thin provider-neutral orchestration layer connecting DEV-0022 persisted lifecycle selection with DEV-0020 resume execution. It selects once, returns normal NotFound without resume, or resumes exactly once using the selected state's exact TaskId and caller-provided options.

## Requirements Implemented

- Added immutable `SelectedPersistedLifecycleResumeRequest` requiring an exact selection request object.
- Preserves merge method, optional title/message, and remote-delete option exactly.
- Matches DEV-0020 behavior by not introducing independent merge-enum validation where DEV-0020 has none.
- Added `SelectedPersistedLifecycleResumeState` with exactly NotFound, Pending, Failed, and Completed.
- Added immutable result model with complete selection/resume-state invariants and unsupported-state rejection.
- Added mockable asynchronous `ISelectedPersistedLifecycleResumer` abstraction.
- Added Tasks orchestrator injecting exactly `IPersistedLifecycleSelector` and `IPersistedDeveloperLifecycle`.
- Delegates the exact selection request object and cancellation token to the selector exactly once.
- Selection NotFound returns normal NotFound with exact selection identity and no DEV-0020 invocation.
- Found selection constructs a DEV-0020 request from the selected persisted state's exact TaskId.
- Delegates exact merge method, optional text, remote-delete option, and cancellation token.
- Invokes DEV-0020 Resume exactly once and only after Found selection.
- Maps Pending, Failed, and Completed while preserving exact selection and resume result identities.
- Treats DEV-0020 NotFound after Found selection as a clear race/inconsistency exception.
- Selector and resume exceptions/cancellation propagate without retry, rollback, second selection, or second resume.
- Adds no direct discovery, state-store, filesystem, JSON, Git, GitHub, HTTP, process, shell, polling, waiting, delay, scheduling, background, Start, CLI, or batch behavior.

## Files Created

- `src/TrailTrainer.Developer.Core/ISelectedPersistedLifecycleResumer.cs`
- `src/TrailTrainer.Developer.Core/SelectedPersistedLifecycleResumeRequest.cs`
- `src/TrailTrainer.Developer.Core/SelectedPersistedLifecycleResumeResult.cs`
- `src/TrailTrainer.Developer.Core/SelectedPersistedLifecycleResumeState.cs`
- `src/TrailTrainer.Developer.Tasks/SelectedPersistedLifecycleResumer.cs`
- `tests/TrailTrainer.Developer.Tests/SelectedPersistedLifecycleResumerTests.cs`
- `docs/developer-reviews/REVIEW-0023.md`

## Files Modified

None.

## Files Deleted

None.

## Architecture / Refactoring Notes

All public contracts and models remain provider-neutral in Core. Tasks contains only two-step orchestration over DEV-0022 and DEV-0020 abstractions. The implementation has no direct discovery/store access and cannot invoke lifecycle Start because only the Resume method is used.

## Tests Added

- Null selection validation and exact optional/resume option preservation.
- DEV-0020-consistent unsupported merge-enum handling.
- Unsupported result state and all NotFound/Pending/Failed/Completed result invariants.
- Exact selection and resume object identities for every valid result.
- Null request rejection before selection.
- Exactly one selector call with exact selection object and cancellation token.
- Selector exception/cancellation propagation preventing Resume without retry.
- Selection NotFound result, identity, and Resume short-circuit.
- Selected state's exact TaskId plus exact merge/delete options in the constructed DEV-0020 request.
- Exactly one Resume call after selection with exact cancellation token.
- Pending, Failed, and Completed mapping with exact nested identities.
- Resume NotFound race failure without re-selection or re-resume.
- DEV-0020 exception and cancellation propagation without retry.
- Fake DEV-0020 Start implementation throws, proving Start is never used by covered paths.
- The full existing regression suite remains passing.

## Verification

### dotnet build

Successful. 0 warnings, 0 errors.

### dotnet test

Successful. 458 passed, 0 failed, 0 skipped.

### git diff --check

Successful. No whitespace errors.

## Deviations from DEV-0023

None.

## Open Issues / Known Limitations

None.

## Commit and Push
No commit created.
No push performed.
