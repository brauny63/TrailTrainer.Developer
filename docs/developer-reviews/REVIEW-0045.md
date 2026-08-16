# REVIEW-0045 – Operational Health Diagnostics
## Status
READY FOR REVIEW
## Summary

DEV-0045 adds the read-only `health` management command. It queries Windows Service status exactly once and, for stable installed states, validates the existing production runtime registration in a short-lived DI container without building or starting a Generic Host, resolving hosted services, or executing the automatic-resume worker. Output is one deterministic `Status: reason` line suitable for scripts.

## Health Semantics Implemented

- `Healthy`: service is `Running` and all required production runtime dependencies resolve.
- `Degraded`: service is stably `Stopped` or `Paused` and all required production runtime dependencies resolve.
- `Unhealthy`: service is `NotInstalled`, `StartPending`, `StopPending`, or `Unknown`; service status fails; or production runtime validation fails.
- Exit code 0 is returned only for `Healthy`.
- Exit code 1 is returned for `Degraded` and `Unhealthy`.
- Exit code 2 remains reserved for invalid commands or arguments.
- Runtime validation is skipped when service status already establishes an unhealthy result.

## Requirements Implemented

- Added `health` to the existing Host management dispatcher and usage text.
- Added small normalized `OperationalHealthStatus` and `OperationalHealthResult` types plus a mockable diagnostics abstraction.
- Reuses `IWindowsServiceManager.GetStatusAsync` and performs exactly one status query.
- Added a focused production runtime validator that reuses `AddDeveloperProductionRuntime`, `AddAutomaticResumePipeline`, existing host options binding, and the existing request-provider registration.
- Uses `ValidateOnBuild = true` and `ValidateScopes = true`.
- Resolves the existing production lifecycle discovery, persisted lifecycle, automatic-resume worker, and request provider without invoking their operational methods.
- Creates and disposes only a short-lived service provider; it neither builds nor starts an `IHost` and does not resolve or execute `IHostedService`.
- Uses the same standard Generic Host configuration sources for the health-specific validation path.
- Production configuration and DI failures become concise `Unhealthy` diagnostics rather than starting the application.
- Service status exceptions become deterministic `Unhealthy` diagnostics.
- Performs no install, uninstall, start, stop, restart, provision, deprovision, recovery, or delayed-start operation.
- Performs no persistence write, Git/GitHub operation, process execution beyond the read-only existing SCM status query, network probe, Developer Task execution, retry, polling, timer, background monitoring, or remediation.
- Existing service-management commands and automatic-resume behavior remain unchanged.

## Files Created

- `src/TrailTrainer.Developer.Host/OperationalHealthDiagnostics.cs`
- `src/TrailTrainer.Developer.Host/ProductionRuntimeHealthValidator.cs`
- `tests/TrailTrainer.Developer.Tests/OperationalHealthDiagnosticsTests.cs`
- `docs/developer-reviews/REVIEW-0045.md`

## Files Modified

- `src/TrailTrainer.Developer.Host/Program.cs`
- `src/TrailTrainer.Developer.Host/WindowsServiceManagementCommandDispatcher.cs`
- `tests/TrailTrainer.Developer.Tests/WindowsServiceManagementCommandTests.cs`

## Files Deleted

None.

## Architecture / Refactoring Notes

Diagnostics remain at the Host operational boundary. Status assessment and runtime composition validation are separated behind small mockable interfaces. The production validator reuses the established DEV-0037 composition instead of constructing dependencies itself. Core, Tasks, lifecycle semantics, automatic-resume orchestration, Windows Service lifecycle operations, and production adapters are unchanged. No general monitoring framework or server was introduced.

## Tests Added

Fifteen test cases cover Healthy, Stopped/Paused Degraded, NotInstalled/pending/unknown Unhealthy, status failure, DI failure, exactly one status query, zero SCM mutations, validator call counts, actual production graph resolution, missing production configuration, absence of persistence/file side effects, deterministic script output, Healthy/Degraded/Unhealthy exit codes, exactly-once diagnostics dispatch, and invalid arguments. The actual production validator test verifies that resolving the graph creates no lifecycle directory and executes no hosted worker. No real Windows Service was modified.

All existing DEV-0038 through DEV-0044 management tests and earlier regression tests continue to pass.

## Verification
### dotnet build

Succeeded for the complete solution: 0 warnings, 0 errors.

The executable `TrailTrainer.Developer.Host` project was also built explicitly with `--no-restore`: 0 warnings, 0 errors.

### dotnet test

Succeeded for the complete solution: 780 passed, 0 failed, 0 skipped.

### git diff --check

Succeeded with no whitespace errors. Git emitted only platform line-ending notices for three modified files; these are not whitespace errors.

## Deviations from DEV-0045

None.

## Open Issues / Known Limitations

None.

## Commit and Push
No commit created.
No push performed.
