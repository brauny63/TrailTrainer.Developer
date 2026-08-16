# REVIEW-0037 – Production Runtime Dependency Registration

## Status
READY FOR REVIEW

## Summary

DEV-0037 closes the production runtime dependency boundaries left explicit by DEV-0034 and DEV-0035. The executable host now registers the existing lifecycle discovery, persistence, Tasks, Git, GitHub, clock, and HTTP implementations through one reusable, idempotent host-boundary extension. The only required runtime setting is the lifecycle-state storage directory, bound and validated through standard .NET options.

## Production Dependency Chain Discovered

- `HostedAutomaticResumeService` (`Tasks`) depends on `IAutomaticResumeWorker` and the DEV-0035 `IAutomaticResumeWorkerRequestProvider`.
- `AutomaticResumeWorker` -> `RepeatedDelayedAutomaticResumeExecutor` -> `AutomaticResumeRunOrchestrator`.
- `AutomaticResumeRunOrchestrator` -> `AutomaticResumeBatchRunner` plus `AutomaticResumeSchedulingDecisionService`.
- `AutomaticResumeBatchRunner` -> `AutomaticResumeBatchStep`.
- `AutomaticResumeBatchStep` -> `AutomaticPersistedLifecycleResumer` plus `IDeveloperLifecycleStateDiscovery`.
- `AutomaticPersistedLifecycleResumer` -> `AutomaticResumeCandidateSelector` plus `IPersistedDeveloperLifecycle`; the selector also depends on `IDeveloperLifecycleStateDiscovery`.
- The existing discovery implementation is `LocalJsonDeveloperLifecycleStateDiscovery` in `TrailTrainer.Developer.Persistence`. Its sole constructor dependency is the configured lifecycle-state storage-directory string.
- The existing lifecycle implementation is `PersistedDeveloperLifecycle` in `TrailTrainer.Developer.Tasks`. It depends on `IDeveloperLifecycleOrchestrator`, `IDeveloperLifecycleResumer`, `IDeveloperLifecycleStateStore`, and `IUtcClock`.
- `IDeveloperLifecycleStateStore` is implemented by `LocalJsonDeveloperLifecycleStateStore` in `Persistence` and uses the same configured storage directory. `IUtcClock` is implemented by `SystemUtcClock` in `Tasks`.
- `DeveloperLifecycleOrchestrator` and `DeveloperLifecycleResumer` transitively use the existing task workflow, review/parser/completion, Pull Request gate, GitHub, and post-merge cleanup abstractions.
- Those boundaries resolve to the existing Tasks implementations, local Git adapters in `TrailTrainer.Developer.Git`, GitHub HTTP adapters in `TrailTrainer.Developer.GitHub`, and a standard side-effect-free `HttpClient` construction.
- No unresolved production boundary remains. DI construction performs no file, Git, GitHub, process, network, or workflow operation.

## Requirements Implemented

- Added `AddDeveloperProductionRuntime(IServiceCollection, IConfiguration)` with null checks, fluent return, and idempotent registrations.
- Registered only established concrete production implementations; no fake, in-memory, or no-op production adapter was introduced.
- Bound `DeveloperProductionRuntime:LifecycleStateStorageDirectory` using standard configuration/options APIs.
- Added clear missing/blank-value validation and startup validation for the required storage directory.
- Registered both JSON discovery and JSON state store against the same preserved configuration value.
- Registered only the transitive Tasks, Git, GitHub, clock, and HTTP dependencies required to make the complete automatic-resume graph production-resolvable.
- Added the required Host project references to the existing implementation assemblies.
- Kept `Program.cs` thin with one production-runtime registration call.
- Preserved DEV-0036 Windows Service integration and the existing automatic-resume composition unchanged.
- Registration and DI resolution remain side-effect-free.

## Files Created

- `src/TrailTrainer.Developer.Host/DeveloperProductionRuntimeOptions.cs`
- `src/TrailTrainer.Developer.Host/DeveloperProductionRuntimeServiceCollectionExtensions.cs`
- `tests/TrailTrainer.Developer.Tests/ProductionRuntimeDependencyRegistrationTests.cs`
- `docs/developer-reviews/REVIEW-0037.md`

## Files Modified

- `src/TrailTrainer.Developer.Host/Program.cs`
- `src/TrailTrainer.Developer.Host/TrailTrainer.Developer.Host.csproj`

## Files Deleted

None.

## Architecture / Refactoring Notes

Runtime composition remains in the executable Host, while Core retains only abstractions and Tasks retains orchestration. The Host references the existing outer adapter projects directly and exposes one composition extension. Factory registrations are used only where the existing persistence constructors require the configured directory string. `TryAddSingleton` preserves caller overrides and prevents harmful duplicate registrations.

## Tests Added

Ten test cases cover null service/configuration arguments, fluent return, idempotency, concrete lifecycle discovery/store/persistence resolution, concrete Git and GitHub adapter resolution, full automatic-resume graph validation with `ValidateOnBuild` and `ValidateScopes`, worker/request-provider/hosted-adapter resolution, exactly one hosted adapter, missing/empty/whitespace configuration failures, exact valid-option preservation, and absence of filesystem workflow side effects during registration and resolution. No runtime service is replaced with a test double.

All existing DEV-0002 through DEV-0036 tests continue to pass.

## Verification
### dotnet build

Succeeded for the complete solution: 0 warnings, 0 errors.

The executable `TrailTrainer.Developer.Host` project was also built explicitly with `--no-restore`: 0 warnings, 0 errors.

### dotnet test

Succeeded for the complete solution: 686 passed, 0 failed, 0 skipped.

### git diff --check

Succeeded with no whitespace errors. Git emitted only platform line-ending notices for two modified Host files; these are not whitespace errors.

## Deviations from DEV-0037

None.

## Open Issues / Known Limitations

None.

## Commit and Push
No commit created.
No push performed.
