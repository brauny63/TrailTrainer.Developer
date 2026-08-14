# REVIEW-0016 – Post-Merge Cleanup

## Status
READY FOR REVIEW

## Summary

Implemented conservative post-merge Git cleanup for an explicitly confirmed merged Pull Request. The cleaner validates all supplied context and repository safety, switches to the configured base branch when needed, performs a fast-forward-only update, deletes an existing merged local feature branch without force, and optionally deletes the exact matching branch from the supplied remote.

## Requirements Implemented

- Added immutable provider-neutral cleanup result and mockable asynchronous abstraction.
- Requires a successful merge result for the same Pull Request before repository inspection or mutation.
- Validates directory, repository identity, PR number, branches, remote, and protected base/feature inequality.
- Reuses the existing repository-status abstraction to resolve repository root and reject non-repositories, dirty trees, untracked files, and detached HEAD.
- Supports invocation from repository subdirectories and returns the resolved root.
- Validates Git branch names, explicit remote existence, and local base-branch existence before mutation.
- Avoids switching when already on the base branch; otherwise uses non-forced `git switch`.
- Updates only from the supplied remote and base branch with `git pull --ff-only`.
- Uses machine-readable ref checks for local and remote feature-branch existence.
- Deletes local branches only with normal merged-branch semantics (`git branch -d`), never force deletion.
- Remote deletion is opt-in and targets only the supplied remote and feature branch.
- Missing local and remote feature branches are tolerated and reported as not deleted.
- All failures short-circuit subsequent operations; no rollback or retry is introduced.
- Propagates cancellation through repository status and every Git invocation.
- Uses the existing shell-free Git process runner and adds no GitHub HTTP behavior.

## Files Created

- `src/TrailTrainer.Developer.Core/IPostMergeCleaner.cs`
- `src/TrailTrainer.Developer.Core/PostMergeCleanupResult.cs`
- `src/TrailTrainer.Developer.Git/LocalPostMergeCleaner.cs`
- `tests/TrailTrainer.Developer.Tests/LocalPostMergeCleanerTests.cs`
- `docs/developer-reviews/REVIEW-0016.md`

## Files Modified

None.

## Files Deleted

None.

## Architecture / Refactoring Notes

Provider-neutral contracts remain in Core. Git-specific validation and mutation are isolated in the Git project and reuse `IGitRepositoryStatusProvider` plus the existing internal `GitProcessRunner`. No parallel process infrastructure or Tasks/GitHub logic was introduced.

## Tests Added

- Null, empty, whitespace, invalid PR, equal branch, null merge, unsuccessful merge, and PR-mismatch validation before repository access.
- Non-repository, tracked dirty file, untracked file, detached HEAD, subdirectory, and resolved-root scenarios.
- Missing remote and missing local base branch before mutation.
- Switch from feature to base and already-on-base behavior.
- Supplied remote/base and fast-forward-only update behavior through isolated local bare remotes.
- Pull failure preventing deletion.
- Existing and missing local feature branches with exact result flags.
- Non-forced rejection of an unmerged local feature branch and prevention of remote deletion.
- Disabled remote deletion, successful exact remote deletion, missing remote branch, preservation of other refs, supplied remote use, and remote-delete failure propagation.
- Required mutation ordering through failure-state assertions.
- Cancellation-token propagation and prevention of subsequent mutation.
- The full existing regression suite remains passing.

## Verification

### dotnet build

Successful. 0 warnings, 0 errors.

### dotnet test

Successful. 295 passed, 0 failed, 0 skipped.

### git diff --check

Successful. No whitespace errors.

## Deviations from DEV-0016

None.

## Open Issues / Known Limitations

None.

## Commit and Push
No commit created.
No push performed.
