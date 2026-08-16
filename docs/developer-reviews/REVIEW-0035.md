# REVIEW-0035 – Automatic Resume Executable Host

## Status
READY FOR REVIEW

## Summary

DEV-0035 adds the first executable .NET Generic Host for the automatic-resume pipeline. A new thin `TrailTrainer.Developer.Host` project creates the standard application builder, registers the DEV-0034 pipeline and a configured request provider, builds the host, and runs it asynchronously.

## Requirements Implemented

- Added the executable `TrailTrainer.Developer.Host` net10.0 project to the solution under `src`.
- Host project references only `TrailTrainer.Developer.Core`, `TrailTrainer.Developer.Tasks`, and the minimum `Microsoft.Extensions.Hosting` package.
- Added a minimal top-level `Program.cs` using `Host.CreateApplicationBuilder(args)`.
- Program calls `AddAutomaticResumePipeline()`, binds the standard `AutomaticResume` configuration section, registers the concrete request provider, builds the host, and awaits `RunAsync()`.
- Program contains no direct orchestration construction, result inspection, loop, delay, retry, Git/GitHub, persistence, process, or Developer Task behavior.
- Added host-specific `AutomaticResumeHostOptions` exposing only the merge options, DEV-0027 step bound, DEV-0029 batch-run bound, resume delay, and DEV-0031 run bound required by existing request constructors.
- Options have valid non-sensitive defaults and use standard Generic Host configuration binding without custom JSON or environment parsing.
- Added `ConfiguredAutomaticResumeWorkerRequestProvider` at the host boundary implementing `IAutomaticResumeWorkerRequestProvider`.
- Provider constructs the existing nested request graph and relies on existing request constructors for clear positive-bound and delay validation.
- Configured merge values, bounds, delay, optional text, and remote-branch flag are preserved exactly.
- Provider performs no workflow, delay, persistence, Git, GitHub, network, or file operations.
- Standard host startup, cancellation, and exception propagation are retained without custom token sources, suppression, or retry.
- No fake or no-op production implementation was introduced for unresolved runtime dependencies.

## Files Created

- `src/TrailTrainer.Developer.Host/TrailTrainer.Developer.Host.csproj`
- `src/TrailTrainer.Developer.Host/Program.cs`
- `src/TrailTrainer.Developer.Host/AutomaticResumeHostOptions.cs`
- `src/TrailTrainer.Developer.Host/ConfiguredAutomaticResumeWorkerRequestProvider.cs`
- `tests/TrailTrainer.Developer.Tests/AutomaticResumeExecutableHostTests.cs`
- `docs/developer-reviews/REVIEW-0035.md`

## Files Modified

- `TrailTrainer.Developer.sln`
- `tests/TrailTrainer.Developer.Tests/TrailTrainer.Developer.Tests.csproj`

## Files Deleted

None.

## Architecture / Refactoring Notes

A separate host project was created because the existing CLI already owns a distinct Git/Developer-Task command entry point. The new project is limited to Generic Host composition and request configuration. The test project references the host project to verify options, provider behavior, DI resolution, and real Generic Host startup semantics. No existing orchestration implementation was modified or duplicated.

## Tests Added

Tests cover the exact minimal option surface, valid nested request construction, preservation of every configured value, zero/negative delay and bound rejection, concrete provider contract/dependency, complete pipeline and hosted-adapter resolution with injected runtime test doubles, exactly one hosted adapter, host startup invoking and awaiting the existing adapter/worker boundary, startup exception propagation without retry, and pre-cancelled startup remaining cancelled. Tests use no real delays, persistence, Git, GitHub, network, or process execution.

All existing DEV-0002 through DEV-0034 tests continue to pass.

## Verification

### dotnet build

Succeeded for the complete solution: 0 warnings, 0 errors.

The new `TrailTrainer.Developer.Host` project was also built explicitly with `--no-restore`: 0 warnings, 0 errors.

### dotnet test

Succeeded for the complete solution: 670 passed, 0 failed, 0 skipped.

### git diff --check

Succeeded with no whitespace errors. A platform line-ending notice for the modified test project file was emitted and is acceptable under the task requirements.

## Deviations from DEV-0035

None.

## Open Issues / Known Limitations

The production executable intentionally does not register the externally configured `IDeveloperLifecycleStateDiscovery` and `IPersistedDeveloperLifecycle` runtime boundaries. Consequently, the complete production host graph will surface a normal DI resolution failure until later tasks provide and configure those production registrations. Tests verify host and pipeline resolution with injected test doubles, as required by DEV-0035.

## Commit and Push
No commit created.
No push performed.
