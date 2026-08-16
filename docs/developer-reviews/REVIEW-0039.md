# REVIEW-0039 – Windows Service Recovery Policy
## Status
READY FOR REVIEW
## Summary

DEV-0039 extends the existing DEV-0038 Windows service-management boundary with the explicit `recovery` command. The command configures the installed `TrailTrainer Developer` service for SCM-managed restart after the first, second, and subsequent failures, resets the failure count after one day, waits 60 seconds before every restart, and enables recovery actions for non-crash failures.

## Requirements Implemented

- Added `ConfigureRecoveryAsync` to the existing mockable `IWindowsServiceManager` abstraction.
- Added `recovery` to the existing Host management-command dispatcher and usage text.
- Preserved established exit codes: 0 success, 1 operation failure, and 2 invalid command/arguments.
- Dispatch occurs before Generic Host composition, so `recovery` does not start the Host or automatic-resume pipeline.
- Reused the exact stable service identity `TrailTrainer Developer`.
- Reused the existing Windows platform guard, `sc.exe` adapter, process runner, structured argument lists, captured diagnostics, and cancellation propagation.
- Queries service status first and fails deterministically when the service is absent.
- Configures `reset= 86400` and `actions= restart/60000/restart/60000/restart/60000` through one `sc.exe failure` operation.
- Configures `sc.exe failureflag TrailTrainer Developer 1` for non-crash failures where supported.
- SCM failures are surfaced immediately with exit code and captured diagnostic; no retry or polling occurs.
- Existing install, uninstall, start, stop, and status behavior is unchanged.
- Added no installation changes, timer, retry, polling, application restart loop, Git/GitHub behavior, or Developer Task execution.

## Files Created

- `docs/developer-reviews/REVIEW-0039.md`

## Files Modified

- `src/TrailTrainer.Developer.Host/IWindowsServiceManager.cs`
- `src/TrailTrainer.Developer.Host/ScWindowsServiceManager.cs`
- `src/TrailTrainer.Developer.Host/WindowsServiceManagementCommandDispatcher.cs`
- `tests/TrailTrainer.Developer.Tests/WindowsServiceManagementCommandTests.cs`

## Files Deleted

None.

## Architecture / Refactoring Notes

Recovery policy remains entirely within the established Host/SCM adapter boundary. Windows owns the restart behavior; no recovery semantics or loops were added to the application, Core, Tasks, Generic Host lifetime, or automatic-resume pipeline. The existing structural `sc.exe` process abstraction remains the only production SCM interaction point.

## Tests Added

Six test cases cover exactly-once `recovery` dispatch, exact service identity, the three required restart actions, one-day reset period, 60-second restart delays, non-crash recovery flag, structured fixed `sc.exe` calls, deterministic missing-service failure, safe non-Windows failure before process execution, immediate SCM policy failure propagation, immediate unsupported/non-crash-flag failure propagation, and absence of retries or follow-up calls after failure. All interactions use the existing fake manager or fake process runner; no real Windows Service was queried or modified.

Existing DEV-0038 command tests and all earlier regression tests continue to pass.

## Verification
### dotnet build

Succeeded for the complete solution: 0 warnings, 0 errors.

The executable `TrailTrainer.Developer.Host` project was also built explicitly with `--no-restore`: 0 warnings, 0 errors.

### dotnet test

Succeeded for the complete solution: 718 passed, 0 failed, 0 skipped.

### git diff --check

Succeeded with no whitespace errors. Git emitted only platform line-ending notices for four modified files; these are not whitespace errors.

## Deviations from DEV-0039

None.

## Open Issues / Known Limitations

None.

## Commit and Push
No commit created.
No push performed.
