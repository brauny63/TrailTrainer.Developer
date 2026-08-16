# REVIEW-0048 – Codex Task Execution Integration

## Status
READY FOR REVIEW

## Summary

DEV-0048 adds a configurable, non-interactive Codex CLI execution boundary to the production Developer Task workflow. The workflow now uses the existing starter to establish the feature branch, records durable execution phases, invokes Codex exactly once, and enters the existing review/completion/Git/GitHub lifecycle only after successful execution.

## Codex Execution Semantics Implemented

- `ICodexTaskExecutor` provides an asynchronous, mockable Core boundary.
- `CodexCliTaskExecutor` uses `ProcessStartInfo.ArgumentList`, no shell, the target repository as working directory, redirected diagnostics, a deterministic prompt, cancellation, process-tree termination, configurable timeout, and bounded output retention.
- Non-zero exit and timeout results prevent review, staging, commit, push, and Pull Request creation.
- Startup and cancellation failures are surfaced deterministically.
- No review report is fabricated; the existing review gate remains authoritative.

## Lifecycle Integration

The production workflow order is task parsing, existing starter/branch creation, durable `BranchCreated` marker, Codex execution, durable `CodexSucceeded` marker, existing gated completion, Pull Request creation, and marker removal. Production DI supplies all integration dependencies while legacy isolated workflow tests remain independently composable.

## Failure / Resume Semantics

- Failure before branch creation produces no Codex marker or invocation.
- Interruption or failure before successful Codex completion retains `BranchCreated`, allowing a later deterministic Codex retry without recreating the branch.
- Successful Codex completion persists `CodexSucceeded`, so interruption before review/completion cannot invoke Codex twice.
- Marker identity is checked against task and repository before reuse.
- Normal intake/task failures are logged with repository context and return gracefully from the hosted adapter rather than triggering an unhandled SCM recovery loop.

## Configuration Added

`CodexExecution` contains required `ExecutablePath`, optional `AdditionalArguments`, positive `Timeout` (default 30 minutes), and positive `MaximumDiagnosticCharacters` (default 16,384). No secrets or credentials are stored.

## Requirements Implemented

The existing starter, review gate, completion, Git, GitHub, merge, cleanup, lifecycle persistence, automatic-resume bounds, health diagnostics, and service-management behavior are preserved. No task-format, installer, authentication, API, scheduling, or SCM functionality was added.

## Files Created

- `src/TrailTrainer.Developer.Core/ICodexTaskExecutor.cs`
- `src/TrailTrainer.Developer.Core/CodexTaskExecutionRequest.cs`
- `src/TrailTrainer.Developer.Core/CodexTaskExecutionResult.cs`
- `src/TrailTrainer.Developer.Core/CodexExecutionPhase.cs`
- `src/TrailTrainer.Developer.Core/CodexExecutionState.cs`
- `src/TrailTrainer.Developer.Core/ICodexExecutionStateStore.cs`
- `src/TrailTrainer.Developer.Host/CodexCliTaskExecutor.cs`
- `src/TrailTrainer.Developer.Host/CodexExecutionOptions.cs`
- `src/TrailTrainer.Developer.Persistence/LocalJsonCodexExecutionStateStore.cs`
- `tests/TrailTrainer.Developer.Tests/CodexTaskExecutionIntegrationTests.cs`
- `docs/developer-reviews/REVIEW-0048.md`

## Files Modified

- `src/TrailTrainer.Developer.Tasks/DeveloperTaskWorkflow.cs`
- `src/TrailTrainer.Developer.Tasks/HostedAutomaticResumeService.cs`
- `src/TrailTrainer.Developer.Host/DeveloperProductionRuntimeServiceCollectionExtensions.cs`
- `src/TrailTrainer.Developer.Host/ProductionRuntimeHealthValidator.cs`
- Existing DI, health, intake, hosted-service, and v1 acceptance tests were supplied a non-executed test Codex configuration where production composition is resolved.

## Files Deleted

None.

## Architecture / Refactoring Notes

Core contains contracts and state only. Tasks performs orchestration only. Persistence owns the durable JSON execution marker. Host owns Codex CLI process execution, configuration, and production registration. No raw process type crosses the infrastructure boundary.

## Tests Added

Eight focused integration tests cover exact fresh-start ordering and request mapping, one Codex invocation, non-zero and timeout failure blocking, starter/precondition failure blocking, successful-phase duplicate prevention, retryable pre-success state, missing/invalid review propagation through the existing gate, deterministic process startup failure, and pre-start cancellation. The full existing suite covers automatic-resume bounds, Git/GitHub isolation, health, hosted execution, and all Windows Service management commands. No real Codex, GitHub, network, or SCM operation is performed.

## Verification

### dotnet build

Succeeded for the complete solution with 0 warnings and 0 errors.

### dotnet test

Succeeded for the complete solution: 802 passed, 0 failed, 0 skipped.

### git diff --check

Succeeded with no whitespace errors.

## Deviations from DEV-0048

None.

## Open Issues / Known Limitations

None within DEV-0048 scope.

## TerrainEngine Pilot Readiness
READY

## Commit and Push
No commit created.
No push performed.
