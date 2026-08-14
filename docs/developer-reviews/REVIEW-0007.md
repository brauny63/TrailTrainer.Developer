# REVIEW-0007 – Start Developer Task Workflow

## Status

READY FOR REVIEW

## Summary

Implemented the first explicit Developer Task workflow by composing task parsing, repository status inspection, and feature-branch creation behind existing Core abstractions.

## Requirements Implemented

- Added an immutable Developer Task start result model.
- Added a mockable asynchronous task-starter abstraction.
- Parses the selected task before all other workflow operations.
- Validates expected and actual repository names using ordinal comparison.
- Rejects non-repositories, detached HEAD, branches other than `main`, and dirty working trees.
- Creates a branch only after every read-only validation succeeds.
- Passes the task's exact `ExpectedBranch` to the existing branch creator.
- Returns task identity, title, repository root, previous and created branches, task path, and review-report path.
- Propagates the supplied cancellation token to every dependency.

## Files Created

- `src/TrailTrainer.Developer.Core/DeveloperTaskStartResult.cs`
- `src/TrailTrainer.Developer.Core/IDeveloperTaskStarter.cs`
- `src/TrailTrainer.Developer.Tasks/DeveloperTaskStarter.cs`
- `tests/TrailTrainer.Developer.Tests/DeveloperTaskStarterTests.cs`
- `docs/developer-reviews/REVIEW-0007.md`

## Files Modified

None.

## Files Deleted

None.

## Architecture / Refactoring Notes

The workflow implementation depends only on `IDeveloperTaskParser`, `IGitRepositoryStatusProvider`, and `IGitBranchCreator` through constructor injection. No Git project reference, shell invocation, process execution, or concrete Git dependency was introduced in Tasks.

## Tests Added

- Successful start with complete result validation.
- Strict parse, status, and branch-creation call ordering.
- Exactly one branch-creator invocation on success.
- Exact ExpectedBranch and repository-directory forwarding.
- Cancellation-token propagation to all dependencies.
- Null, empty, and whitespace expected-repository-name rejection.
- Ordinal repository-metadata mismatch with expected and actual diagnostics.
- Parser failure without status lookup or branch creation.
- Non-repository, detached HEAD, non-`main`, and dirty-tree rejection without branch creation.

## Verification

### dotnet build

Successful. 0 warnings, 0 errors.

### dotnet test

Successful. 58 passed, 0 failed, 0 skipped.

### git diff --check

Successful. No whitespace errors.

## Deviations from DEV-0007

None.

## Open Issues / Known Limitations

None.

## Commit and Push

No commit created.
No push performed.
