# REVIEW-0028 – Automatic Resume Scheduling Decision

## Status
READY FOR REVIEW

## Summary

DEV-0028 adds a provider-neutral, synchronous, side-effect-free decision layer that maps one DEV-0027 bounded batch-run result to Finished, ContinueImmediately, ResumeLater, or StopFailed with the required run-again and immediacy flags.

## Requirements Implemented

- Added the exact Finished, ContinueImmediately, ResumeLater, and StopFailed decision states.
- Added an immutable decision result that rejects null batch runs and unsupported decision states.
- Enforced the complete DEV-0027-state, decision-state, ShouldRunAgain, and Immediate mapping as result invariants.
- Preserved the exact DEV-0027 batch-run result object.
- Added a mockable synchronous Core abstraction without cancellation or asynchronous API.
- Added a parameterless decision service in Tasks with no dependencies.
- Mapped Empty and Completed to Finished with both flags false.
- Mapped Pending to ResumeLater with ShouldRunAgain true and Immediate false.
- Mapped Failed to StopFailed with both flags false.
- Mapped LimitReached to ContinueImmediately with both flags true.
- Rejected null input and unsupported DEV-0027 enum values.
- Relied only on the DEV-0027 terminal state and did not inspect individual steps or MoreWork independently.
- Introduced no DEV-0027 execution, scheduling, timing, delay, retry, polling, persistence, discovery, filesystem, JSON, Git, GitHub, network, process, CLI, or background behavior.

## Files Created

- `src/TrailTrainer.Developer.Core/AutomaticResumeSchedulingDecisionState.cs`
- `src/TrailTrainer.Developer.Core/AutomaticResumeSchedulingDecision.cs`
- `src/TrailTrainer.Developer.Core/IAutomaticResumeSchedulingDecision.cs`
- `src/TrailTrainer.Developer.Tasks/AutomaticResumeSchedulingDecisionService.cs`
- `tests/TrailTrainer.Developer.Tests/AutomaticResumeSchedulingDecisionTests.cs`
- `docs/developer-reviews/REVIEW-0028.md`

## Files Modified

None.

## Files Deleted

None.

## Architecture / Refactoring Notes

Provider-neutral decision state, result, and abstraction types are located in `TrailTrainer.Developer.Core`. The pure deterministic decision mapping is located in `TrailTrainer.Developer.Tasks`. The service has an implicit parameterless constructor and no dependencies. No unrelated refactoring was performed.

## Tests Added

In-memory unit tests cover null and unsupported-state rejection, every decision and flag invariant, batch-state consistency, exact batch-result identity preservation, all five required mappings, unsupported DEV-0027 state handling, absence of constructor dependencies, and the synchronous API contract. Tests use no fakes requiring external behavior because the service has no dependencies.

All existing DEV-0002 through DEV-0027 tests continue to pass.

## Verification

### dotnet build

Succeeded for the complete solution: 0 warnings, 0 errors.

### dotnet test

Succeeded for the complete solution: 550 passed, 0 failed, 0 skipped.

### git diff --check

Succeeded with no whitespace errors.

## Deviations from DEV-0028

None.

## Open Issues / Known Limitations

None.

## Commit and Push
No commit created.
No push performed.
