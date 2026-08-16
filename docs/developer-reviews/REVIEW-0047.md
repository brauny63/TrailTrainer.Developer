# REVIEW-0047 – Initial Developer Task Intake
## Status
READY FOR REVIEW

## Summary

DEV-0047 adds a small production intake boundary for one configured local target repository. The hosted service now attempts intake before invoking the unchanged bounded automatic-resume worker. Intake is disabled by default, gives existing persisted work priority, validates repository safety, selects at most one task in existing discovery order, and starts it through `IPersistedDeveloperLifecycle`.

## Intake Semantics Implemented

- Disabled intake returns without repository inspection, task discovery, or lifecycle start.
- Enabled intake first uses the existing automatic-resume candidate selector.
- Any persisted resume candidate prevents new task discovery and lifecycle start.
- The configured directory must exist and the existing Git status abstraction must report a repository with an attached branch and no uncommitted changes.
- Existing `DeveloperTaskDiscovery` ordering determines the selected task; only the first descriptor is started.
- The selected task is passed to the existing persisted lifecycle boundary, which retains existing parsing, starter, Git, GitHub, merge, cleanup, and persistence behavior.
- The existing automatic-resume worker runs afterward and retains all established execution bounds.
- Existing structured logging reports disabled, skipped, empty, selected, and started intake outcomes with task/repository context.

## Configuration Added

The `InitialTaskIntake` section contains:

- `Enabled`, default `false`
- `RepositoryPath`
- `RepositoryName`
- `GitHubOwner`
- `BaseBranch`, default `main`
- `RemoteName`, default `origin`

Merge behavior is reused from `AutomaticResumeHostOptions`. No credentials or tokens were added.

## Requirements Implemented

The implementation reuses task discovery, automatic-resume candidate semantics, persisted lifecycle start, the lifecycle state store, Git repository status, and the hosted automatic-resume pipeline. It does not clone, pull, clean, reset, stash, delete, or overwrite repository work. It adds no polling loop, watcher, queue, scheduler, database, installer behavior, or external authentication.

## Files Created

- `src/TrailTrainer.Developer.Core/IInitialDeveloperTaskIntake.cs`
- `src/TrailTrainer.Developer.Core/IInitialDeveloperTaskIntakeRequestProvider.cs`
- `src/TrailTrainer.Developer.Core/InitialDeveloperTaskIntakeRequest.cs`
- `src/TrailTrainer.Developer.Core/InitialDeveloperTaskIntakeResult.cs`
- `src/TrailTrainer.Developer.Core/InitialDeveloperTaskIntakeState.cs`
- `src/TrailTrainer.Developer.Tasks/InitialDeveloperTaskIntake.cs`
- `src/TrailTrainer.Developer.Host/InitialTaskIntakeOptions.cs`
- `src/TrailTrainer.Developer.Host/ConfiguredInitialDeveloperTaskIntakeRequestProvider.cs`
- `tests/TrailTrainer.Developer.Tests/InitialDeveloperTaskIntakeTests.cs`
- `docs/developer-reviews/REVIEW-0047.md`

## Files Modified

- `src/TrailTrainer.Developer.Tasks/HostedAutomaticResumeService.cs`
- `src/TrailTrainer.Developer.Host/DeveloperProductionRuntimeServiceCollectionExtensions.cs`
- `src/TrailTrainer.Developer.Host/ProductionRuntimeHealthValidator.cs`
- `src/TrailTrainer.Developer.Host/Program.cs`
- `tests/TrailTrainer.Developer.Tests/HostedAutomaticResumeServiceTests.cs`

## Files Deleted

None.

## Architecture / Refactoring Notes

Core contains only the asynchronous, mockable intake contract and data model. Tasks contains orchestration and logging but no concrete Git process, GitHub transport, persistence format, or host configuration. Host owns option binding and conversion into a Core request. The existing hosted adapter supports the intake pair when production composition supplies it while remaining usable by the independently tested automatic-resume pipeline.

## Tests Added

Nine focused tests cover disabled behavior, missing repository, non-Git repository, dirty repository, deterministic selection among multiple tasks, one start per attempt, resume priority/non-overwrite, visible malformed-task failure, no-task behavior, existing lifecycle request mapping, persistence-boundary use, and production DI with both default-disabled and valid-enabled configuration.

The complete existing suite additionally verifies automatic-resume continuation and bounds, no real GitHub or Codex calls, no Windows SCM mutation, unchanged operational health behavior, and unchanged Windows Service management commands.

## Verification
### dotnet build

Succeeded for the complete solution with 0 warnings and 0 errors.

### dotnet test

Succeeded for the complete solution: 794 passed, 0 failed, 0 skipped.

### git diff --check

Succeeded with no whitespace errors.

## Deviations from DEV-0047

None.

## Open Issues / Known Limitations

None within DEV-0047 scope.

## Pilot Readiness Assessment
READY

## Commit and Push
No commit created.
No push performed.
