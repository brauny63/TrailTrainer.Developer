# REVIEW-0041 – Windows Service Operational Setup
## Status
READY FOR REVIEW
## Summary

DEV-0041 adds the explicit `setup` management command to operationally configure the already-installed `TrailTrainer Developer` service. The command reuses the existing DEV-0040 delayed-start operation followed by the existing DEV-0039 recovery operation in deterministic, fail-fast order. It performs no installation, service start/restart, rollback, or direct SCM command construction.

## Requirements Implemented

- Added `setup` to the existing Host management-command dispatcher and usage text.
- `setup` calls `ConfigureDelayedStartAsync` exactly once, then `ConfigureRecoveryAsync` exactly once.
- Reuses the existing manager abstraction and operations; the dispatcher does not construct or invoke `sc.exe` commands.
- Service existence is verified by the existing delayed-start operation before its configuration and again by the existing recovery operation before its configuration.
- Missing service fails before any configuration is attempted.
- Failure in delayed-start stops execution immediately and prevents recovery.
- Recovery failure is surfaced after the successful delayed-start operation without rollback or retry.
- The original operation diagnostic is preserved through the established failure exit path.
- Preserved exit codes: 0 success, 1 operation failure, and 2 invalid command/arguments.
- Dispatch occurs before Generic Host composition, so `setup` does not start the Host or automatic-resume pipeline.
- Reuses the exact stable service identity through the existing DEV-0039/0040 operations.
- Performs no install, start, stop, restart, rollback, retry, or polling operation.
- Non-Windows execution fails safely before process execution.
- Existing install, uninstall, start, stop, status, recovery, and delayed-start commands remain unchanged.
- Added no timer, PowerShell, Git/GitHub behavior, Developer Task execution, or automatic-resume changes.

## Files Created

- `docs/developer-reviews/REVIEW-0041.md`

## Files Modified

- `src/TrailTrainer.Developer.Host/WindowsServiceManagementCommandDispatcher.cs`
- `tests/TrailTrainer.Developer.Tests/WindowsServiceManagementCommandTests.cs`

## Files Deleted

None.

## Architecture / Refactoring Notes

Setup orchestration remains at the Windows operational Host boundary. It composes existing service-manager operations and contains no duplicated SCM details. Core, Tasks, lifecycle persistence/discovery, Generic Host worker behavior, automatic-resume orchestration, installation, delayed-start implementation, and recovery-policy implementation are unchanged.

## Tests Added

Seven tests cover setup management dispatch, success exit code, exactly-once delayed-start and recovery calls, deterministic ordering, absence of install/start/stop, delayed-start fail-fast behavior, prevention of recovery after first-step failure, recovery failure propagation without rollback/retry, invalid arguments, the complete existing SCM-operation sequence through the fake process runner, missing-service failure before configuration, and safe non-Windows failure before process execution. All SCM interactions use fakes; no real Windows Service was queried or modified.

All existing DEV-0038 through DEV-0040 management tests and earlier regression tests continue to pass.

## Verification
### dotnet build

Succeeded for the complete solution: 0 warnings, 0 errors.

The executable `TrailTrainer.Developer.Host` project was also built explicitly with `--no-restore`: 0 warnings, 0 errors.

### dotnet test

Succeeded for the complete solution: 730 passed, 0 failed, 0 skipped.

### git diff --check

Succeeded with no whitespace errors. Git emitted only platform line-ending notices for two modified files; these are not whitespace errors.

## Deviations from DEV-0041

None.

## Open Issues / Known Limitations

None.

## Commit and Push
No commit created.
No push performed.
