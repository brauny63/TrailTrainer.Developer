# REVIEW-0057 - Recover Stranded Codex State Without Lifecycle State

## Status

READY FOR REVIEW

## Summary

A dedicated startup recovery component now adopts only the fully proven pre-DEV-0055 stranded Codex state. It persists a minimal recovery lifecycle payload, changes the matching Codex phase to ReviewRepairRequired, and hands execution back to the existing automatic resume and review-only workflow without touching implementation files or weakening normal dirty-repository checks.

## Requirements Implemented

- Required exact Codex phase, task ID, repository path, task-file path, feature branch, dirty worktree, review path, invalid review, and absent lifecycle state before adoption.
- Rejected missing, conflicting, clean, valid, mismatched, and ambiguous states without persistence changes.
- Added a backward-compatible persisted recovery-start payload discoverable by AutomaticResumeCandidateSelector.
- Routed adopted state through PersistedDeveloperLifecycle and the existing DeveloperTaskWorkflow.
- Re-ran the automatic resume worker after successful startup adoption and skipped ordinary initial intake.
- Preserved the ordinary initial-intake dirty-repository safety checks unchanged.
- Performed no branch creation, reset, clean, stash, deletion, commit, push, network, GitHub, or Windows SCM operation during recovery tests.

## Files Created

- `src/TrailTrainer.Developer.Core/IStrandedCodexStateRecovery.cs`
- `src/TrailTrainer.Developer.Core/StrandedCodexStateRecoveryResult.cs`
- `src/TrailTrainer.Developer.Tasks/StrandedCodexStateRecovery.cs`
- `tests/TrailTrainer.Developer.Tests/StrandedCodexStateRecoveryTests.cs`
- `docs/developer-reviews/REVIEW-0057.md`

## Files Modified

- `src/TrailTrainer.Developer.Core/DeveloperLifecyclePersistedState.cs`
- `src/TrailTrainer.Developer.Core/PersistedDeveloperLifecycleResumeResult.cs`
- `src/TrailTrainer.Developer.Host/DeveloperProductionRuntimeServiceCollectionExtensions.cs`
- `src/TrailTrainer.Developer.Persistence/LifecycleStateJsonFormat.cs`
- `src/TrailTrainer.Developer.Tasks/HostedAutomaticResumeService.cs`
- `src/TrailTrainer.Developer.Tasks/PersistedDeveloperLifecycle.cs`
- `tests/TrailTrainer.Developer.Tests/HostedAutomaticResumeServiceTests.cs`

## Files Deleted

None

## Architecture / Refactoring Notes

StrandedCodexStateRecovery owns only strict legacy-state proof and adoption. AutomaticResumeCandidateSelector remains read-only and unchanged. The persisted lifecycle model has one mutually exclusive recovery-start payload for the pre-PR workflow stage; PersistedDeveloperLifecycle consumes it through the existing orchestrator and replaces it with the established PR resume context if the workflow becomes pending. HostedAutomaticResumeService invokes recovery only after ordinary automatic resume reports no candidate and before initial intake. InitialDeveloperTaskIntake and DeveloperTaskWorkflow retain their normal dirty-worktree safety rules.

## Tests Added

- Added the exact DEV-0007 legacy-state regression with BranchCreated state, dirty expected branch, invalid `## Architecture Notes` review, lifecycle adoption, ReviewRepairRequired transition, selector discovery, and byte-for-byte implementation preservation.
- Added rejection coverage for wrong branch, wrong repository, wrong task ID, wrong task file, missing review, clean repository, conflicting lifecycle state, valid review, and missing Codex state.
- Existing review-repair workflow regressions continue to prove review-only Codex instructions, corrected review parsing, implementation preservation, and workflow continuation without real Codex or external effects.

## Verification

### dotnet build

Successful with `dotnet build --no-restore`. 0 warnings, 0 errors. The restoring invocation was blocked only by sandbox denial when NuGet attempted to read the user-level NuGet.Config.

### dotnet test

Successful with `dotnet test --no-restore --no-build`. 844 passed, 0 failed, 0 skipped.

### git diff --check

Successful. No whitespace errors.

## Deviations from DEV-0057

The restoring `dotnet build` invocation could not read the user-level NuGet.Config under the workspace sandbox. Verification used the already restored dependency graph with `--no-restore`; no product or test scope was reduced.

## Open Issues / Known Limitations

None

## Commit and Push

No commit created.
No push performed.
