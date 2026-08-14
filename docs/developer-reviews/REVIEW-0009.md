# REVIEW-0009 – Developer Task CLI

## Status

READY FOR REVIEW

## Summary

Implemented a small, testable command-line adapter for listing, showing, starting, and completing Developer Tasks through the existing Core abstractions and workflow implementations.

## Requirements Implemented

- Added `tasks list`, `tasks show`, `tasks start`, and `tasks complete` commands.
- Resolves the repository root from the current working directory and derives its name from the root directory.
- Resolves tasks by canonical `DeveloperTaskId` or exact filename, with clear missing and ambiguous failures.
- Preserves discovery ordering and avoids document parsing during list operations.
- Delegates show, start, and complete behavior to existing abstractions without duplicating workflow rules.
- Requires `--message`, defaults remote to `origin`, and defaults upstream tracking to `true`.
- Supports ordinal case-insensitive command and option names.
- Rejects unknown commands/options, missing arguments/values, and duplicate options.
- Separates standard output and errors and returns non-zero exit codes on failure.
- Propagates cancellation through all invoked dependencies.

## Files Created

- `src/TrailTrainer.Developer.CLI/DeveloperCliApplication.cs`
- `tests/TrailTrainer.Developer.Tests/DeveloperCliApplicationTests.cs`
- `docs/developer-reviews/REVIEW-0009.md`

## Files Modified

- `src/TrailTrainer.Developer.CLI/Program.cs`
- `tests/TrailTrainer.Developer.Tests/TrailTrainer.Developer.Tests.csproj`

## Files Deleted

None.

## Architecture / Refactoring Notes

`DeveloperCliApplication` is an injectable adapter accepting arguments, working directory, output/error writers, and cancellation. `Program.cs` is a thin composition root. The CLI invokes existing abstractions and introduces no shell or process execution and no workflow business-rule duplication.

## Tests Added

- Canonical task-ID and exact-filename resolution.
- Missing, ambiguous, and non-fuzzy task resolution.
- Ordered list output, empty-list output, and no parsing during list.
- Required show fields without start/complete calls.
- Start delegation arguments, call count, and output.
- Complete required message, default remote/upstream, explicit remote, exact message, call count, and output.
- Case-insensitive command and option names.
- Unknown commands/options, missing values, and duplicate options.
- Workflow failure exit code, stderr output, and absence of success output.
- Cancellation propagation.

## Verification

### dotnet build

Successful. 0 warnings, 0 errors.

### dotnet test

Successful. 97 passed, 0 failed, 0 skipped.

### git diff --check

Successful. No whitespace errors.

## Deviations from DEV-0009

None.

## Open Issues / Known Limitations

None.

## Commit and Push

No commit created.
No push performed.
