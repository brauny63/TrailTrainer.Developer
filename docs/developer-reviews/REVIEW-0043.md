# REVIEW-0043 – Windows Service Deprovisioning Command
## Status
READY FOR REVIEW
## Summary

DEV-0043 adds the explicit `deprovision` management command for the `TrailTrainer Developer` Windows Service. It composes the existing status, stop, and uninstall operations with deterministic state handling and fail-fast semantics. An absent service is an idempotent success; stopped services uninstall directly; running or paused services stop once before uninstall. Pending and unknown states fail safely without polling.

## Requirements Implemented

- Added `deprovision` to the existing Host management-command dispatcher and usage text.
- Reuses the exact stable service identity through existing manager operations.
- Reuses only existing `GetStatusAsync`, `StopAsync`, and `UninstallAsync` operations; no SCM command construction was added.
- Dispatch occurs before Generic Host composition, so deprovisioning does not start the Host or automatic-resume pipeline.
- `NotInstalled` returns exit code 0 without stop or uninstall.
- `Stopped` invokes uninstall exactly once without stop.
- `Running` and `Paused` invoke stop exactly once followed by uninstall exactly once.
- `StartPending`, `StopPending`, and `Unknown` fail clearly because safe continuation would require waiting or polling.
- Stop failure prevents uninstall.
- Uninstall failure after a successful stop is surfaced without restart or rollback.
- Performs no start or restart operation.
- Deletes no application binaries, configuration, logs, lifecycle state, repositories, or user data.
- Leaves removal of recovery and delayed-start SCM configuration to service deletion.
- Preserved exit codes: 0 success, 1 operation failure, and 2 invalid command/arguments.
- Non-Windows execution fails before process execution.
- Added no retry, polling, timer, rollback loop, PowerShell, Git/GitHub behavior, or Developer Task execution.
- All existing management commands, including `provision`, remain unchanged.

## Files Created

- `docs/developer-reviews/REVIEW-0043.md`

## Files Modified

- `src/TrailTrainer.Developer.Host/WindowsServiceManagementCommandDispatcher.cs`
- `tests/TrailTrainer.Developer.Tests/WindowsServiceManagementCommandTests.cs`

## Files Deleted

None.

## Architecture / Refactoring Notes

Deprovisioning is orchestration only at the Windows operational Host boundary. It contains state branching but delegates every platform operation to the existing DEV-0038 manager abstraction. Core, automatic-resume orchestration, lifecycle persistence/discovery, Generic Host behavior, production runtime registration, provisioning, and SCM adapter implementation are unchanged.

## Tests Added

Thirteen test cases cover management dispatch, idempotent absent-service success, stopped-service direct uninstall, running and paused stop-before-uninstall ordering, exactly-once operations, conservative StartPending/StopPending/Unknown failures, stop failure preventing uninstall, uninstall failure without restart/rollback, invalid arguments, safe non-Windows handling, the complete production-manager sequence through the fake process runner, exact stable identity, absence of start operations, and preservation of temporary application/lifecycle sentinel files. No real Windows Service was queried or modified.

All existing DEV-0038 through DEV-0042 command tests and earlier regression tests continue to pass.

## Verification
### dotnet build

Succeeded for the complete solution: 0 warnings, 0 errors.

The executable `TrailTrainer.Developer.Host` project was also built explicitly with `--no-restore`: 0 warnings, 0 errors.

### dotnet test

Succeeded for the complete solution: 751 passed, 0 failed, 0 skipped.

### git diff --check

Succeeded with no whitespace errors. Git emitted only platform line-ending notices for two modified files; these are not whitespace errors.

## Deviations from DEV-0043

None.

## Open Issues / Known Limitations

None.

## Commit and Push
No commit created.
No push performed.
