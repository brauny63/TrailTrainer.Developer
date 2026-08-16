# REVIEW-0056 - Prioritize Recoverable Work Before Initial Intake

## Status

READY FOR REVIEW

## Summary

Hosted startup now runs the existing automatic-resume pipeline before initial task intake. Initial intake is skipped whenever the resume result proves that persisted recoverable work was found, while unrelated dirty repositories remain subject to the unchanged strict intake and workflow safety checks.

## Requirements Implemented

- Exposed whether an automatic-resume worker result encountered persisted resumable work.
- Reordered hosted startup so automatic recovery runs before initial task intake.
- Prevented initial intake and duplicate task creation when review-repair recovery exists.
- Preserved controlled-failure host behavior by stopping safely after a controlled resume failure.
- Kept the existing clean-repository requirement for new intake unchanged.
- Kept dirty-branch permission scoped to the existing review-repair workflow phase.

## Files Created

- `docs/developer-reviews/REVIEW-0056.md`

## Files Modified

- `src/TrailTrainer.Developer.Core/AutomaticResumeWorkerResult.cs`
- `src/TrailTrainer.Developer.Tasks/HostedAutomaticResumeService.cs`
- `src/TrailTrainer.Developer.Tasks/InitialDeveloperTaskIntake.cs`
- `tests/TrailTrainer.Developer.Tests/HostedAutomaticResumeServiceTests.cs`
- `tests/TrailTrainer.Developer.Tests/InitialDeveloperTaskIntakeTests.cs`

## Files Deleted

None

## Architecture / Refactoring Notes

Startup ordering remains owned by HostedAutomaticResumeService and persisted lifecycle recovery remains owned by the existing AutomaticResumeWorker pipeline. AutomaticResumeWorkerResult derives recovery presence from the existing nested resume results. Before applying its new-task dirty check, InitialDeveloperTaskIntake recognizes only an exact task, repository, task-file, and ReviewRepairRequired Codex state; execution still crosses the existing persisted lifecycle and workflow boundaries. Review-repair branch validation remains owned by DeveloperTaskWorkflow and was not relaxed.

## Tests Added

- Added hosted startup coverage proving a DEV-0007 persisted recovery candidate runs before and suppresses initial intake.
- Added hosted startup coverage proving initial intake runs only after automatic resume reports no recoverable work.
- Added intake coverage proving a dirty exact ReviewRepairRequired task resumes without selecting a second discovered task.
- Updated controlled intake failure coverage for the new resume-first ordering.
- Existing DEV-0007 parser and workflow regressions continue to prove review-only instructions, parser-valid repair, dirty matching-branch recovery, unchanged implementation work, and continuation beyond review validation.

## Verification

### dotnet build

Successful with `dotnet build --no-restore`. 0 warnings, 0 errors. The exact restoring invocation was blocked only by sandbox denial when reading the user-level NuGet.Config.

### dotnet test

Successful with `dotnet test --no-restore`. 834 passed, 0 failed, 0 skipped.

### git diff --check

Successful. No whitespace errors.

## Deviations from DEV-0056

The restoring `dotnet build` invocation could not read the user-level NuGet.Config under the workspace sandbox. Verification used the already restored dependency graph with `--no-restore`; no product or test scope was reduced.

## Open Issues / Known Limitations

None

## Commit and Push

No commit created.
No push performed.
