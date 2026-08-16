# REVIEW-0038 – Windows Service Installation Command

## Status
READY FOR REVIEW

## Summary

DEV-0038 adds explicit `install`, `uninstall`, `start`, `stop`, and `status` commands to the existing executable Host. Commands are dispatched before Generic Host composition and use a mockable Windows SCM adapter backed by `sc.exe` and a structural, non-shell process runner. Normal execution with no management command continues through the unchanged DEV-0036/DEV-0037 Generic Host path.

## Requirements Implemented

- Added five explicit service-management commands at the executable Host boundary.
- Added deterministic exit codes: 0 success, 1 operation failure, and 2 invalid command/arguments.
- Added a minimal command dispatcher that prevents management commands from building or starting the automatic-resume Host.
- Reused the exact existing service identity `TrailTrainer Developer` as both SCM service key and display name; no second machine-oriented identity was necessary.
- Install uses `Environment.ProcessPath`, converts it to a full path, quotes it safely for SCM storage, configures automatic service startup, and does not start the service.
- Install queries first and rejects an existing service without overwriting it.
- Uninstall treats an absent service as deterministic success and otherwise requests deletion without stopping it, deleting application files, or touching persistence.
- Start and stop issue exactly one SCM operation with no retry or polling.
- Status maps native numeric SCM states to `NotInstalled`, `Stopped`, `StartPending`, `StopPending`, `Running`, `Paused`, or `Unknown` without exposing localized output.
- Non-Windows management is rejected before process execution.
- Added mockable service-manager, platform, and process-runner abstractions.
- Production process execution fixes the executable to `sc.exe`, uses `ProcessStartInfo.ArgumentList`, disables shell execution, captures stdout/stderr and exit code, supports cancellation, and surfaces diagnostics.
- Added no elevation bypass, credentials, arbitrary commands, PowerShell, timers, retry, polling, Git/GitHub, or automatic Developer Task behavior.
- DEV-0036 Windows Service hosting, DEV-0037 production DI, and automatic-resume orchestration remain unchanged.

## Files Created

- `src/TrailTrainer.Developer.Host/IWindowsPlatform.cs`
- `src/TrailTrainer.Developer.Host/IWindowsServiceManager.cs`
- `src/TrailTrainer.Developer.Host/ScWindowsServiceManager.cs`
- `src/TrailTrainer.Developer.Host/WindowsServiceManagementCommandDispatcher.cs`
- `src/TrailTrainer.Developer.Host/WindowsServiceProcessRunner.cs`
- `src/TrailTrainer.Developer.Host/WindowsServiceState.cs`
- `tests/TrailTrainer.Developer.Tests/WindowsServiceManagementCommandTests.cs`
- `docs/developer-reviews/REVIEW-0038.md`

## Files Modified

- `src/TrailTrainer.Developer.Host/Program.cs`

## Files Deleted

None.

## Architecture / Refactoring Notes

Service-management concepts remain entirely in the Host project and do not enter Core, Tasks, or automatic-resume orchestration. The top-level entry point only selects the management dispatcher when arguments are present; otherwise its previous composition and run sequence is preserved. The production SCM adapter owns Windows-specific state mapping and delegates only fixed `sc.exe` operations with structurally separated arguments.

## Tests Added

Twenty-six tests cover the normal no-command path, exact one-time dispatch for all five commands, invalid commands and extra arguments, executable-path forwarding, absence of implicit start, deterministic exit codes and diagnostics, normalized status output, safe non-Windows failure, structured `sc.exe` arguments, stable service identity, quoted paths containing spaces, existing-service conflict protection, deterministic absent-service uninstall, installed-service uninstall, start/stop targeting, single-attempt failure behavior, all required native state mappings, unknown state, and absent-service mapping.

All service-management and process interactions use fakes. No test constructs the production process runner or installs, starts, stops, queries, or removes a real Windows Service. Existing DEV-0002 through DEV-0037 tests continue to pass.

## Verification
### dotnet build

Succeeded for the complete solution: 0 warnings, 0 errors.

The executable `TrailTrainer.Developer.Host` project was also built explicitly with `--no-restore`: 0 warnings, 0 errors.

### dotnet test

Succeeded for the complete solution: 712 passed, 0 failed, 0 skipped.

### git diff --check

Succeeded with no whitespace errors. Git emitted only a platform line-ending notice for modified `Program.cs`; this is not a whitespace error.

## Deviations from DEV-0038

None.

## Open Issues / Known Limitations

None.

## Commit and Push
No commit created.
No push performed.
