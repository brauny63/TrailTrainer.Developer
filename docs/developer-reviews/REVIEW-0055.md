# REVIEW-0055 - Enforce Developer Review Contract in Codex Instructions

## Status

READY FOR REVIEW

## Summary

The Codex instruction now emits the exact authoritative DeveloperReviewParser contract. Invalid parser-level review output is persisted as a review-repair recovery state so a subsequent invocation preserves existing implementation work and repairs only the review.

## Requirements Implemented

- Centralized the reusable review headings and Codex formatting guidance in DeveloperReviewContract.
- Made DeveloperReviewParser consume the shared required-section contract without weakening validation.
- Added exact status, file-list, verification, deviations, filename, heading-ID, and commit/push instructions to every Codex task request.
- Added a review-only recovery phase for parser-invalid reviews that preserves existing dirty implementation output.
- Preserved the serialized numeric value of the existing CodexSucceeded lifecycle phase.

## Files Created

- `src/TrailTrainer.Developer.Core/DeveloperReviewContract.cs`
- `docs/developer-reviews/REVIEW-0055.md`

## Files Modified

- `src/TrailTrainer.Developer.Core/CodexExecutionPhase.cs`
- `src/TrailTrainer.Developer.Core/CodexTaskExecutionRequest.cs`
- `src/TrailTrainer.Developer.Tasks/DeveloperReviewParser.cs`
- `src/TrailTrainer.Developer.Tasks/DeveloperTaskWorkflow.cs`
- `tests/TrailTrainer.Developer.Tests/CodexTaskExecutionIntegrationTests.cs`
- `tests/TrailTrainer.Developer.Tests/DeveloperReviewParserTests.cs`

## Files Deleted

None

## Architecture / Refactoring Notes

DeveloperReviewContract in the Core project is the single reusable owner of required section names and generated Codex review guidance. The parser consumes its heading collection directly, while CodexTaskExecutionRequest delegates instruction generation to it. ReviewRepairRequired is appended to the lifecycle enum to retain the persisted numeric meaning of existing phases.

## Tests Added

- Added contract-generation coverage for every required fixed heading, the task-specific deviations heading, exact verification forms, and commit/push wording.
- Added the DEV-0007 regression proving that `## Architecture Notes` is rejected, the generated repair instruction contains `## Architecture / Refactoring Notes`, the corrected review parses, and an existing implementation file remains unchanged.
- Added workflow recovery coverage proving parser failure persists review-repair state and permits a dirty expected branch only for review-only repair without cleanup or implementation duplication.

## Verification

### dotnet build

Successful. 0 warnings, 0 errors.

### dotnet test

Successful. 831 passed, 0 failed, 0 skipped.

### git diff --check

Successful. No whitespace errors.

## Deviations from DEV-0055

None

## Open Issues / Known Limitations

None

## Commit and Push

No commit created.
No push performed.
