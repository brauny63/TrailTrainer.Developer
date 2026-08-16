# REVIEW-0042 – Windows Service Provisioning Command

## Status
READY FOR REVIEW

## Summary

DEV-0042 adds the explicit `provision` management command for the `TrailTrainer Developer` service. Provisioning verifies absence, installs through the existing DEV-0038 operation using the current executable path, and then invokes the same extracted operational-setup sequence used by DEV-0041. Execution is deterministic and fail-fast, does not start the service, and intentionally performs no rollback when setup fails after installation.

## Requirements Implemented

- Added `provision` to the existing Host management-command dispatcher and usage text.
- Verifies `GetStatusAsync()` returns `NotInstalled` before installation; any installed state fails without alteration.
- Reuses the exact stable service identity through all existing manager operations.
- Reuses the existing current-executable-path validation shared with `install`.
- Calls the existing `InstallAsync` operation exactly once after a successful absence check.
- Extracted the existing DEV-0041 sequence into one private `ConfigureOperationalSetupAsync` method used by both `setup` and `provision`.
- Operational setup still calls delayed-start exactly once followed by recovery exactly once.
- Enforces deterministic order: absence check, install, delayed start, recovery, return.
- Installation failure prevents all setup operations.
- Delayed-start failure prevents recovery.
- Recovery failure is surfaced after installation and delayed-start configuration.
- Setup failure does not uninstall, stop, restart, or otherwise roll back the installed service.
- Successful provisioning performs no start operation and reports that the service is provisioned and stopped.
- Preserved exit codes: 0 success, 1 operation failure, and 2 invalid command/arguments.
- Dispatch occurs before Generic Host composition, so provisioning does not start the Host or automatic-resume pipeline.
- Non-Windows execution fails before process execution.
- Added no SCM command construction to the dispatcher and no retry, polling, timer, PowerShell, Git/GitHub, or Developer Task behavior.
- All existing service-management commands retain their behavior.

## Files Created

- `docs/developer-reviews/REVIEW-0042.md`

## Files Modified

- `src/TrailTrainer.Developer.Host/WindowsServiceManagementCommandDispatcher.cs`
- `tests/TrailTrainer.Developer.Tests/WindowsServiceManagementCommandTests.cs`

## Files Deleted

None.

## Architecture / Refactoring Notes

Provisioning is orchestration only at the Windows operational Host boundary. Installation remains encapsulated by DEV-0038, while delayed-start and recovery remain encapsulated by DEV-0040 and DEV-0039. The small shared setup method prevents the dispatcher from describing the DEV-0041 sequence twice. Core, persistence/discovery, Generic Host, production runtime registration, and automatic-resume behavior are unchanged.

## Tests Added

Eight test cases cover management dispatch, exact absence/install/setup ordering, current executable-path preservation, existing-service protection, install failure preventing setup, delayed-start and recovery failures, absence of uninstall rollback, absence of start/stop/restart, success and operation-failure exit codes, invalid arguments, safe non-Windows handling, and the complete production manager operation sequence through the fake process runner. The latter verifies the stable service identity on every SCM call and confirms no start, stop, or delete operation occurs. No real Windows Service was queried or modified.

All existing DEV-0038 through DEV-0041 command tests and earlier regression tests continue to pass.

## Verification
### dotnet build

Succeeded for the complete solution: 0 warnings, 0 errors.

The executable `TrailTrainer.Developer.Host` project was also built explicitly with `--no-restore`: 0 warnings, 0 errors.

### dotnet test

Succeeded for the complete solution: 738 passed, 0 failed, 0 skipped.

### git diff --check

Succeeded with no whitespace errors. Git emitted only platform line-ending notices for two modified files; these are not whitespace errors.

## Deviations from DEV-0042

None.

## Open Issues / Known Limitations

None.

## Commit and Push
No commit created.
No push performed.
