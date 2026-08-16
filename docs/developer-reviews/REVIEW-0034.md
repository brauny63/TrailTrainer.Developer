# REVIEW-0034 – Automatic Resume Host Composition Root

## Status
READY FOR REVIEW

## Summary

DEV-0034 adds an idempotent .NET dependency-injection composition root for the automatic-resume pipeline. It wires the existing stateless orchestration implementations and DEV-0033 hosted adapter while leaving runtime-specific request, discovery, and persisted-lifecycle boundaries explicit for the caller.

## Requirements Implemented

- Added `AddAutomaticResumePipeline(IServiceCollection)` as a Tasks extension method.
- Rejects a null service collection and returns the exact same collection instance.
- Registers `IAsyncDelay` to `SystemAsyncDelay`.
- Registers the existing DEV-0024/DEV-0025 candidate and persisted-resume orchestration needed below DEV-0026 without inventing runtime implementations.
- Registers `IAutomaticResumeBatchStep`, `IAutomaticResumeBatchRunner`, `IAutomaticResumeSchedulingDecision`, and `IAutomaticResumeRunOrchestrator` to their existing DEV-0026 through DEV-0029 implementations.
- Registers `IRepeatedDelayedAutomaticResumeExecutor` and `IAutomaticResumeWorker` to their existing DEV-0031/DEV-0032 implementations.
- Registers only `HostedAutomaticResumeService` as an `IHostedService` for this pipeline.
- Uses singleton lifetimes for stateless orchestration components.
- Uses `TryAddSingleton` and `TryAddEnumerable` so repeated registration calls remain idempotent and cannot duplicate hosted worker execution.
- Does not register a concrete `IAutomaticResumeWorkerRequestProvider`, `IDeveloperLifecycleStateDiscovery`, or `IPersistedDeveloperLifecycle`.
- Missing runtime boundaries surface through normal DI validation; supplying test doubles enables complete hosted-pipeline resolution.
- Registration performs no workflow execution, request construction, result inspection, configuration parsing, persistence access, file/environment access, Git/GitHub/network/process work, timing, polling, retry, host startup, Windows-service/systemd integration, or CLI behavior.

## Files Created

- `src/TrailTrainer.Developer.Tasks/AutomaticResumeServiceCollectionExtensions.cs`
- `tests/TrailTrainer.Developer.Tests/AutomaticResumeServiceCollectionExtensionsTests.cs`
- `docs/developer-reviews/REVIEW-0034.md`

## Files Modified

- `tests/TrailTrainer.Developer.Tests/TrailTrainer.Developer.Tests.csproj`

## Files Deleted

None.

## Architecture / Refactoring Notes

The composition root is located in the existing `TrailTrainer.Developer.Tasks` project and uses the hosting/DI abstractions already introduced for DEV-0033. The concrete `Microsoft.Extensions.DependencyInjection` package was added only to the test project to exercise `ServiceCollection`, provider building, validation, and resolution. No new project, executable host, `Program.cs`, or composition-time business logic was introduced.

## Tests Added

ServiceCollection-based tests cover null input, same-instance return, registration side-effect freedom, every required interface-to-concrete mapping and singleton lifetime, absence of invented runtime boundaries, expected validation failure when boundaries are missing, complete graph resolution with test doubles, double-registration idempotency, exactly one hosted adapter after two calls, and stable repeated singleton resolution with scope validation. Test doubles throw if any workflow method is accidentally invoked during registration or resolution.

All existing DEV-0002 through DEV-0033 tests continue to pass.

## Verification

### dotnet build

Succeeded for the complete solution: 0 warnings, 0 errors.

### dotnet test

Succeeded for the complete solution: 655 passed, 0 failed, 0 skipped.

### git diff --check

Succeeded with no whitespace errors. A platform line-ending notice for the modified test project file was emitted and is acceptable under the task requirements.

## Deviations from DEV-0034

None.

## Open Issues / Known Limitations

None.

## Commit and Push
No commit created.
No push performed.
