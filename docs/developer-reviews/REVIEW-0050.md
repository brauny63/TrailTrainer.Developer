# REVIEW-0050 – Codex Execution Validation and Missing Review Recovery

## Status
READY FOR REVIEW

## Summary

DEV-0050 makes `CodexSucceeded` a validated state rather than a process-exit marker. A successful Codex process must leave the repository on the expected task branch and produce a parseable, valid review before success becomes durable. Missing or invalid reviews remain controlled task failures and cannot enter completion, Git, GitHub, or merge work.

## Pilot Failure Reproduced

The DEV-0007 regression starts from a clean main branch, creates and persists the task branch, simulates Codex exit code 0 without `REVIEW-0007.md`, and proves that the state remains `BranchCreated`. Review completion and Pull Request creation are not called, and the failure contains the task ID and expected review path.

## Root Cause

`DeveloperTaskWorkflow` previously persisted `CodexSucceeded` immediately after a zero exit code. Review parsing occurred later inside gated completion, so a missing file raised `FileNotFoundException` after false success had become durable. The hosted startup boundary did not distinguish this expected task failure from fatal startup failures on every intake/resume path.

## Codex Success Validation

After exit code 0 and before persisting success, the workflow verifies the repository is valid, attached, and on the exact expected task branch. It resolves the repository-relative expected review path within the repository, parses it through the existing review parser, and validates it through the existing review validator. Only a valid result permits `CodexSucceeded`.

## Review Validation Boundary

The existing parser and validator remain authoritative. Missing, malformed, structurally invalid, identity-mismatched, blocked, or otherwise invalid reviews are translated into `DeveloperTaskExecutionException` with task, repository, branch, review path, exit code, and validation-phase context. No review is fabricated, repaired, or edited.

## Durable State Changes

No new durable phase was needed. Failed process execution or failed post-execution validation leaves the existing `BranchCreated` marker in place. `CodexSucceeded` is saved only after all minimum postconditions pass, and an already validated persisted success still skips duplicate Codex execution.

## Retry Semantics

A resumed `BranchCreated` execution runs only from the exact expected, clean, attached task branch. An unexpected branch or uncommitted work blocks retry without reset, clean, stash, checkout, deletion, or overwrite. A clean incomplete execution can retry deterministically without recreating the branch. Changes left by an incomplete Codex run therefore block automatic overwrite.

## Hosted-Service Failure Handling

The hosted intake and automatic-resume boundaries catch only `DeveloperTaskExecutionException`, log the controlled task failure, and keep it from terminating host startup. Cancellation, configuration failures, unrelated I/O failures, programmer errors, and other invariant failures continue to propagate. The executable-host failure test uses no Windows Event Log provider, keeping tests free of external SCM/Event Log effects.

## Diagnostics

Controlled execution diagnostics distinguish process failure, timeout, missing review, invalid review, unsafe repository state, retry validation, and post-execution validation. Messages include task ID, repository path, expected branch, expected review path where applicable, and Codex exit code where available; no environment dump or secret is logged.

## Requirements Implemented

- Exit code 0 alone cannot persist `CodexSucceeded`.
- Missing and invalid reviews block completion, staging, commit, push, Pull Request creation, and merge work.
- Expected review identity and status are enforced by the existing parser and validator.
- Post-execution and pre-retry repository safety are enforced generically.
- Incomplete clean execution remains retryable; unsafe or dirty retries are blocked without cleanup.
- Validated success continues through the existing completion flow and prevents duplicate Codex execution.
- Controlled task failures are bounded at both hosted intake and resume boundaries.
- DEV-0048 execution and DEV-0049 interrupted-start recovery behavior remain intact.

## Files Created

- `src/TrailTrainer.Developer.Core/DeveloperTaskExecutionException.cs`
- `docs/developer-reviews/REVIEW-0050.md`

## Files Modified

- `src/TrailTrainer.Developer.Tasks/DeveloperTaskWorkflow.cs`
- `src/TrailTrainer.Developer.Tasks/HostedAutomaticResumeService.cs`
- `tests/TrailTrainer.Developer.Tests/AutomaticResumeExecutableHostTests.cs`
- `tests/TrailTrainer.Developer.Tests/CodexTaskExecutionIntegrationTests.cs`
- `tests/TrailTrainer.Developer.Tests/HostedAutomaticResumeServiceTests.cs`

## Files Deleted

None.

## Architecture / Refactoring Notes

Core adds only the controlled task-execution exception contract. Tasks retains orchestration, review validation, retry safety, and the hosted failure boundary. Existing persistence, Git, GitHub, lifecycle, parser, validator, and production DI registrations are preserved; no second Codex subsystem was introduced.

## Tests Added

Tests cover validated exit-zero success, missing review, invalid review, non-zero exit, timeout, completion and Pull Request blocking, deterministic clean retry, dirty and unexpected-branch retry blocking, duplicate execution prevention, DEV-0049 interrupted-start recovery, controlled intake failure, controlled resume failure, and propagation of unrelated host failures. All effects use fakes; no real Codex, GitHub, network, SCM, or repository-cleanup operation occurs.

## DEV-0007 Regression Test

`Dev0007Regression_ExitZeroWithoutReview_RemainsRetryableAndNeverCompletes` uses DEV-0007 identity, the expected feature branch, and `docs/developer-reviews/REVIEW-0007.md`. A fake successful Codex result followed by missing-review parsing leaves `BranchCreated`, never calls completion or Pull Request creation, and surfaces a controlled diagnostic. The normal success test proves the valid-review counterpart persists `CodexSucceeded` before continuing.

## Verification

### dotnet build

Successful. 0 warnings and 0 errors.

### dotnet test

Successful. 817 passed, 0 failed, 0 skipped.

### git diff --check

Successful. No whitespace errors.

## Deviations from DEV-0050

None.

## Open Issues / Known Limitations

None within DEV-0050 scope.

## TerrainEngine DEV-0007 Retry Readiness
READY

## Commit and Push
No commit created.
No push performed.
