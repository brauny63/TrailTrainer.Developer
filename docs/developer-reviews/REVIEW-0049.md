# REVIEW-0049 – Codex State Persistence and Interrupted-Start Recovery

## Status
READY FOR REVIEW

## Summary

DEV-0049 hardens Codex execution-state persistence and adds narrowly scoped recovery when branch creation completed but the initial `BranchCreated` marker did not become durable. Existing Git safety, Codex execution, review, completion, persistence, and hosted-service behavior remain authoritative.

## Pilot Failure Reproduced

The automated DEV-0007 regression performs two conceptual service runs. The first creates the expected branch and fails the initial state save before Codex starts. The second sees no Codex state and a clean repository on the exact expected task branch, reconstructs `BranchCreated`, does not recreate the branch, invokes Codex exactly once, and continues through completion.

## Root Cause

The temporary JSON stream in `LocalJsonCodexExecutionStateStore.SaveAsync` remained open while `File.Move(..., overwrite: true)` attempted to promote it. On Windows this deterministically caused the observed sharing violation. Saves were also not serialized per final state path.

## State Persistence Changes

- The temporary stream is serialized, flushed asynchronously, flushed to disk, and disposed before atomic promotion.
- Cancellation is checked before final replacement.
- I/O and access failures contain task ID and final state path diagnostics.
- Temporary artifacts are removed after success and, where possible, after failure.
- Readers continue to inspect only `codex-<TaskId>.json`; temporary files are never loaded.
- Existing final state remains in place until the replacement file is complete and durable.

## Concurrency Semantics

Each store instance owns per-normalized-state-path semaphores. Saves for the same task/path are serialized without sleeps or global static locking, while independent task IDs remain independent. Forty overlapping rapid saves are verified to leave one complete parseable state and no temporary artifacts.

## Interrupted-Start Recovery

When no Codex marker exists, the workflow validates task/configured/GitHub repository identity and obtains the existing Git repository status. Recovery skips branch creation only when the repository is valid, clean, attached, and currently on exactly the task's expected branch. Dirty, detached, non-repository, mismatched, or unrelated branches remain rejected through existing safety behavior. No reset, clean, stash, checkout, deletion, fabricated success, or fabricated review is performed.

## Lifecycle / Ordering Changes

Fresh start remains parse → clean-main starter → branch creation → durable `BranchCreated` → Codex. Recovery is parse → exact clean expected-branch recognition → durable `BranchCreated` → Codex. Persisted `CodexSucceeded` still prevents duplicate execution, and failed Codex execution cannot reach completion, push, or Pull Request creation.

## Hosted-Service Failure Handling

Persistence failures are now diagnostic `InvalidOperationException` instances, which the DEV-0048 hosted intake boundary logs with repository context and handles as normal task failures without causing an uncontrolled SCM restart loop. Cancellation remains cancellation and is not converted into success.

## Requirements Implemented

Atomic JSON state replacement, per-path concurrency, cancellation, diagnostic failures, temporary cleanup, safe expected-branch recovery, identity validation, duplicate-Codex prevention, existing resume priority, and unchanged production storage-directory registration are implemented. No external Codex, GitHub, network, or SCM effect occurs in tests.

## Files Created

- `tests/TrailTrainer.Developer.Tests/LocalJsonCodexExecutionStateStoreTests.cs`
- `docs/developer-reviews/REVIEW-0049.md`

## Files Modified

- `src/TrailTrainer.Developer.Persistence/LocalJsonCodexExecutionStateStore.cs`
- `src/TrailTrainer.Developer.Tasks/DeveloperTaskWorkflow.cs`
- `tests/TrailTrainer.Developer.Tests/CodexTaskExecutionIntegrationTests.cs`

## Files Deleted

None.

## Tests Added

Persistence tests cover 40 overlapping/rapid same-task saves, complete parseable final JSON, absence of temporary artifacts, independent task IDs, cancellation preserving the previous state, and replacement failure preserving prior state with useful diagnostics. Workflow tests cover valid interrupted-start recovery, unrelated/dirty branch rejection, and the complete two-run DEV-0007 regression.

## Regression Test for DEV-0007 Pilot

The regression proves that the initial save failure occurs after exactly one branch creation and before Codex; the restart then performs no second branch creation, invokes Codex exactly once, persists success, and enters normal review/completion.

## Verification

### dotnet build

Succeeded for the complete solution with 0 warnings and 0 errors.

### dotnet test

Succeeded for the complete solution: 810 passed, 0 failed, 0 skipped.

### git diff --check

Succeeded with no whitespace errors.

## Deviations from DEV-0049

None.

## Open Issues / Known Limitations

The synchronization scope is the production singleton store instance, matching the supported single-host process model. Cross-process concurrent writers remain outside the supported execution model.

## TerrainEngine DEV-0007 Retry Readiness
READY

## Commit and Push
No commit created.
No push performed.
