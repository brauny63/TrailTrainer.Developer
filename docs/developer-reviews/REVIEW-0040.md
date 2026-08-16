# REVIEW-0040 – Windows Service Delayed Automatic Start
## Status
READY FOR REVIEW
## Summary

DEV-0040 extends the existing Windows service-management boundary with the explicit `delayed-start` command. It verifies that `TrailTrainer Developer` is installed and then configures the service for Windows delayed automatic startup through one structured SCM operation, without starting or restarting the service and without changing automatic-resume behavior.

## Requirements Implemented

- Added `ConfigureDelayedStartAsync` to the existing mockable `IWindowsServiceManager` abstraction.
- Added `delayed-start` to the existing Host management-command dispatcher and usage text.
- Preserved established exit codes: 0 success, 1 operation failure, and 2 invalid command/arguments.
- Dispatch occurs before Generic Host composition, so the command does not start the Host or automatic-resume pipeline.
- Reused the exact stable service identity `TrailTrainer Developer`.
- Reused the existing Windows platform guard, `sc.exe` adapter, process runner, structured arguments, diagnostics, and cancellation behavior.
- Verifies service existence through the established status operation before configuration.
- Configures `sc.exe config TrailTrainer Developer start= delayed-auto`, which sets automatic start mode and enables delayed automatic start through the supported SCM mechanism.
- Issues no service start, stop, or restart operation.
- Missing-service, non-Windows, and SCM failures surface clearly and deterministically.
- Performs no retry or polling.
- Preserved DEV-0038 install/uninstall/start/stop/status behavior and DEV-0039 recovery behavior unchanged.
- Added no PowerShell, shell interpolation, timer, custom restart logic, Git/GitHub behavior, or Developer Task execution.

## Files Created

- `docs/developer-reviews/REVIEW-0040.md`

## Files Modified

- `src/TrailTrainer.Developer.Host/IWindowsServiceManager.cs`
- `src/TrailTrainer.Developer.Host/ScWindowsServiceManager.cs`
- `src/TrailTrainer.Developer.Host/WindowsServiceManagementCommandDispatcher.cs`
- `tests/TrailTrainer.Developer.Tests/WindowsServiceManagementCommandTests.cs`

## Files Deleted

None.

## Architecture / Refactoring Notes

Delayed-start configuration remains entirely within the established Host/SCM adapter boundary. The application lifecycle, Generic Host, automatic-resume pipeline, Core, Tasks, installation behavior, and recovery policy are unchanged. The same fixed `sc.exe` process boundary and structural argument handling from DEV-0038 are reused.

## Tests Added

Five test cases cover exactly-once `delayed-start` dispatch, service existence checking, exact stable identity, exact structured `config` arguments, automatic delayed mode, absence of start/stop/restart operations, deterministic missing-service behavior, safe non-Windows rejection before process execution, immediate SCM diagnostic propagation, and absence of retries. All interactions use the existing fake manager or fake process runner; no real Windows Service was queried or modified.

All DEV-0038 command tests, DEV-0039 recovery tests, and earlier regression tests continue to pass.

## Verification
### dotnet build

Succeeded for the complete solution: 0 warnings, 0 errors.

The executable `TrailTrainer.Developer.Host` project was also built explicitly with `--no-restore`: 0 warnings, 0 errors.

### dotnet test

Succeeded for the complete solution: 723 passed, 0 failed, 0 skipped.

### git diff --check

Succeeded with no whitespace errors. Git emitted only platform line-ending notices for four modified files; these are not whitespace errors.

## Deviations from DEV-0040

None.

## Open Issues / Known Limitations

None.

## Commit and Push
No commit created.
No push performed.
