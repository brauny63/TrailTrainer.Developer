# REVIEW-0044 – Windows Service Restart Command
## Status
READY FOR REVIEW
## Summary

DEV-0044 adds the explicit `restart` management command for the existing `TrailTrainer Developer` Windows Service. It composes the established status, stop, and start operations with deterministic, fail-fast state handling and no polling, retry, rollback, or SCM command duplication.

## State Handling Implemented

- `Running` -> status, stop exactly once, then start exactly once.
- `Stopped` -> status, then start exactly once without a redundant stop.
- `NotInstalled` -> deterministic operation failure without stop or start.
- `StartPending` -> deterministic safe failure without changes.
- `StopPending` -> deterministic safe failure without changes.
- `Paused` -> deterministic safe failure without implicitly changing pause semantics.
- `Unknown` -> deterministic safe failure without changes.

The transitional, paused, and unknown states are rejected because safe restart would require waiting, polling, or additional semantics outside DEV-0044.

## Requirements Implemented

- Added `restart` to the existing Host management-command dispatcher and usage text.
- Reuses the exact stable service identity through existing manager operations.
- Reuses only `GetStatusAsync`, `StopAsync`, and `StartAsync`; no `sc.exe` construction was added to orchestration.
- Dispatch occurs before Generic Host composition, so restart does not start the automatic-resume Host path.
- A running service must stop successfully before start is invoked.
- Stop failure prevents start.
- Start failure is surfaced without a second attempt or rollback.
- Status failure is surfaced without stop or start.
- Performs no install, uninstall, provision, deprovision, recovery, or delayed-start operation.
- Preserved exit codes: 0 success, 1 operation failure, and 2 invalid command/arguments.
- Non-Windows execution fails before process execution.
- Added no retry, polling, timer, wait loop, rollback, PowerShell, Git/GitHub behavior, or Developer Task execution.
- Existing management commands and provisioning/deprovisioning semantics remain unchanged.

## Files Created

- `docs/developer-reviews/REVIEW-0044.md`

## Files Modified

- `src/TrailTrainer.Developer.Host/WindowsServiceManagementCommandDispatcher.cs`
- `tests/TrailTrainer.Developer.Tests/WindowsServiceManagementCommandTests.cs`

## Files Deleted

None.

## Architecture / Refactoring Notes

Restart is operational orchestration only at the Host boundary. Platform operations remain encapsulated by the existing DEV-0038 manager abstraction. Core, lifecycle persistence/discovery, automatic-resume orchestration, Generic Host worker behavior, production runtime registration, provisioning, and deprovisioning are unchanged.

## Tests Added

Fourteen test cases cover running and stopped success paths, exact status/stop/start ordering and counts, absent-service failure, StartPending/StopPending/Paused/Unknown conservative behavior, status failure, stop failure preventing start, start failure from running and stopped states, absence of second attempts, invalid arguments, safe non-Windows handling, exact stable identity, and the complete production-manager `query -> stop -> start` sequence through the fake process runner. Tests also verify that no install, uninstall, provisioning, deprovisioning, or service-configuration operation occurs. No real Windows Service was queried or modified.

All existing DEV-0038 through DEV-0043 command tests and earlier regression tests continue to pass.

## Verification
### dotnet build

Succeeded for the complete solution: 0 warnings, 0 errors.

The executable `TrailTrainer.Developer.Host` project was also built explicitly with `--no-restore`: 0 warnings, 0 errors.

### dotnet test

Succeeded for the complete solution: 765 passed, 0 failed, 0 skipped.

### git diff --check

Succeeded with no whitespace errors. Git emitted only platform line-ending notices for two modified files; these are not whitespace errors.

## Deviations from DEV-0044

None.

## Open Issues / Known Limitations

None.

## Commit and Push
No commit created.
No push performed.
