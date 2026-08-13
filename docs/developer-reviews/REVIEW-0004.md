# REVIEW-0004 – Git Stage and Commit

## Status

READY FOR REVIEW

## Summary

Implemented separate local Git capabilities for staging all repository changes and creating a commit from changes that are already staged.

## Requirements Implemented

- Added immutable stage and commit result models.
- Added mockable asynchronous staging and committing abstractions.
- Stages modified, untracked, and deleted files with `git add --all`.
- Determines staged-change state through Git exit codes.
- Creates commits only from changes already present in the index.
- Rejects missing staged changes and empty or whitespace-only commit messages.
- Returns the repository root, created commit SHA, and supplied commit message.
- Supports repository subdirectories and rejects non-repository directories.
- Reuses the existing shell-free, cancellable Git process runner.

## Files Created

- `src/TrailTrainer.Developer.Core/GitStageResult.cs`
- `src/TrailTrainer.Developer.Core/GitCommitResult.cs`
- `src/TrailTrainer.Developer.Core/IGitStager.cs`
- `src/TrailTrainer.Developer.Core/IGitCommitter.cs`
- `src/TrailTrainer.Developer.Git/GitIndex.cs`
- `src/TrailTrainer.Developer.Git/LocalGitStager.cs`
- `src/TrailTrainer.Developer.Git/LocalGitCommitter.cs`
- `tests/TrailTrainer.Developer.Tests/LocalGitStagerTests.cs`
- `tests/TrailTrainer.Developer.Tests/LocalGitCommitterTests.cs`
- `docs/developer-reviews/REVIEW-0004.md`

## Files Modified

None.

## Files Deleted

None.

## Architecture / Refactoring Notes

The new concrete services use `IGitRepositoryStatusProvider` to resolve repository roots and reuse the existing internal `GitProcessRunner`. Index comparison is centralized in a small internal `GitIndex` helper shared by staging and committing. No process or Git command details were added to Core.

## Tests Added

- Staging modified tracked, untracked, and deleted tracked files.
- Clean-repository staging result.
- Staging from a nested repository directory.
- Staging rejection for a non-repository directory.
- Commit creation from staged changes.
- Created commit SHA and supplied/created commit message verification.
- Exclusion of an untracked unstaged file from the commit.
- Commit creation from a nested repository directory.
- Empty and whitespace-only commit-message rejection.
- No-staged-change rejection without creating an empty commit.
- Commit rejection for a non-repository directory.

## Verification

### dotnet build

Successful. 0 warnings, 0 errors.

### dotnet test

Successful. 20 passed, 0 failed, 0 skipped.

### git diff --check

Successful. No whitespace errors.

## Deviations from DEV-0004

None.

## Open Issues / Known Limitations

None.

## Commit and Push

No commit created.
No push performed.
