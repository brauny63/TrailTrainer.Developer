# REVIEW-0033 – Hosted Automatic Resume Service Adapter

## Status
READY FOR REVIEW

## Summary

DEV-0033 adds the first .NET Generic Host adapter for the automatic-resume pipeline. The `IHostedService` obtains one request and awaits exactly one DEV-0032 worker invocation for each explicit `StartAsync` call, while `StopAsync` completes without orchestration.

## Requirements Implemented

- Added the synchronous provider-neutral `IAutomaticResumeWorkerRequestProvider` Core abstraction.
- Added the minimum `Microsoft.Extensions.Hosting.Abstractions` dependency to the existing Tasks project.
- Added `HostedAutomaticResumeService` implementing `IHostedService` rather than `BackgroundService`.
- Hosted adapter depends exactly on `IAutomaticResumeWorker` and `IAutomaticResumeWorkerRequestProvider`.
- `StartAsync` obtains the request exactly once and rejects a null provider result before worker invocation.
- Delegates exactly once to DEV-0032 with the exact provider request and exact host cancellation token.
- Awaits worker completion and does not create detached/background work.
- Does not inspect or reinterpret the worker result and performs no subsequent invocation.
- Propagates provider, worker, and cancellation exceptions unchanged without retry.
- `StopAsync` completes immediately without calling the provider or worker and without delay or cleanup orchestration.
- Introduces no DEV-0031/DEV-0029/DEV-0028/DEV-0027/IAsyncDelay dependency, loop, delay, timer, polling, retry, persistence, discovery, filesystem, JSON, Git, GitHub, network, process, CLI, Windows-service, systemd, configuration parsing, host builder, or composition root.

## Files Created

- `src/TrailTrainer.Developer.Core/IAutomaticResumeWorkerRequestProvider.cs`
- `src/TrailTrainer.Developer.Tasks/HostedAutomaticResumeService.cs`
- `tests/TrailTrainer.Developer.Tests/HostedAutomaticResumeServiceTests.cs`
- `docs/developer-reviews/REVIEW-0033.md`

## Files Modified

- `src/TrailTrainer.Developer.Tasks/TrailTrainer.Developer.Tasks.csproj`

## Files Deleted

None.

## Architecture / Refactoring Notes

The provider-neutral request-provider contract is in `TrailTrainer.Developer.Core`. The .NET hosting adapter remains in the existing `TrailTrainer.Developer.Tasks` project, with only `Microsoft.Extensions.Hosting.Abstractions` added. No new project, executable host, registration layer, or unrelated refactoring was introduced.

## Tests Added

Injected-fake unit tests directly construct and invoke the hosted adapter. They cover the synchronous Core provider contract, exact provider and worker call counts, repeated explicit host calls, exact request/token delegation, null provider output, provider failure, awaited worker completion via a controlled completion source, absence of detached work or repeat invocation, worker failure, cancellation, inert StopAsync behavior, `IHostedService` implementation, exact constructor dependencies, and absence of `BackgroundService` inheritance. No real host infrastructure is required.

All existing DEV-0002 through DEV-0032 tests continue to pass.

## Verification

### dotnet build

Succeeded for the complete solution: 0 warnings, 0 errors.

### dotnet test

Succeeded for the complete solution: 647 passed, 0 failed, 0 skipped.

### git diff --check

Succeeded with no whitespace errors. A platform line-ending notice for the modified Tasks project file was emitted and is acceptable under the task requirements.

## Deviations from DEV-0033

None.

## Open Issues / Known Limitations

None.

## Commit and Push
No commit created.
No push performed.
