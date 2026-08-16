# REVIEW-0036 – Windows Service Hosting Integration

## Status
READY FOR REVIEW

## Summary

DEV-0036 integrates the existing executable automatic-resume host with the standard .NET Windows Service hosting adapter. The host now uses `Microsoft.Extensions.Hosting.WindowsServices`, configures the stable service name `TrailTrainer Developer`, and preserves the existing automatic-resume pipeline and normal console/debug behavior.

## Requirements Implemented

- Added the minimum official `Microsoft.Extensions.Hosting.WindowsServices` package to the existing executable host project at version `10.0.0`, matching the target framework and existing hosting package.
- Enabled Windows Service integration during host composition through `AddWindowsService(...)`.
- Configured the stable service name `TrailTrainer Developer` at the host boundary only.
- Kept `Program.cs` composition-only and retained the existing pipeline, options, request-provider, build, and run registrations.
- Reused `AddAutomaticResumePipeline()`, `HostedAutomaticResumeService`, and the existing DEV-0032 worker without changing or duplicating orchestration.
- Retained standard Generic Host lifetime, cancellation, startup-failure, and console/debug behavior.
- Added no custom `ServiceBase`, SCM interop, service installation behavior, second executable, custom lifetime protocol, loop, timer, polling, retry, cron, Git/GitHub, process/shell, or automatic Developer Task behavior.

## Files Created

- `src/TrailTrainer.Developer.Host/AutomaticResumeWindowsServiceExtensions.cs`
- `tests/TrailTrainer.Developer.Tests/WindowsServiceHostingIntegrationTests.cs`
- `docs/developer-reviews/REVIEW-0036.md`

## Files Modified

- `src/TrailTrainer.Developer.Host/Program.cs`
- `src/TrailTrainer.Developer.Host/TrailTrainer.Developer.Host.csproj`

## Files Deleted

None.

## Architecture / Refactoring Notes

Windows-specific hosting configuration is isolated in a small host-project extension. Core and Tasks remain unaware of Windows Service or SCM concepts. The official context-aware hosting integration is used, so the same executable remains usable as a normal console/debug host when it is not launched by the Windows Service Control Manager.

## Tests Added

Six tests cover the stable host-boundary service name, null-argument validation, fluent service-collection registration, preservation of exactly one `HostedAutomaticResumeService`, absence of a custom `ServiceBase` implementation, and absence of additional hosted workflow execution during normal console composition. The tests inspect registrations and assembly types only; they do not install or start a real Windows Service.

All existing DEV-0002 through DEV-0035 tests continue to pass.

## Verification
### dotnet build

Succeeded for the complete solution: 0 warnings, 0 errors.

The executable `TrailTrainer.Developer.Host` project was also built explicitly with `--no-restore`: 0 warnings, 0 errors.

### dotnet test

Succeeded for the complete solution: 676 passed, 0 failed, 0 skipped.

### git diff --check

Succeeded with no whitespace errors. Git emitted only platform line-ending notices for two modified files; these are not whitespace errors.

## Deviations from DEV-0036

None.

## Open Issues / Known Limitations

None.

## Commit and Push
No commit created.
No push performed.
