# REVIEW-0032 – Automatic Resume Background Worker

## Status
READY FOR REVIEW

## Summary

DEV-0032 adds a host-neutral automatic-resume worker boundary. Each worker invocation delegates exactly once to the bounded DEV-0031 executor, passes the exact request and cancellation token, and returns the exact execution result without inspecting or reinterpreting it.

## Requirements Implemented

- Added an immutable worker request that rejects null and preserves the exact DEV-0031 execution request identity.
- Added an immutable worker result that rejects null and preserves the exact DEV-0031 execution result identity.
- Introduced no duplicate worker state enum.
- Added a mockable asynchronous Core worker abstraction.
- Added a Tasks worker implementation depending exactly on `IRepeatedDelayedAutomaticResumeExecutor`.
- Validates a null worker request before delegation.
- Invokes the DEV-0031 executor exactly once per successful invocation.
- Delegates the exact execution request and exact caller cancellation token.
- Awaits and preserves the exact DEV-0031 result without inspecting state, run history, delay count, ShouldRunAgain, or Immediate.
- Propagates exceptions and cancellation unchanged without retry or a second invocation.
- Introduces no execution loop, delay, DEV-0027/DEV-0028/DEV-0029/IAsyncDelay dependency, persistence, discovery, filesystem, JSON, Git, GitHub, network, process, timer, polling, retry, CLI, hosted-service, Windows-service, or host-registration behavior.

## Files Created

- `src/TrailTrainer.Developer.Core/AutomaticResumeWorkerRequest.cs`
- `src/TrailTrainer.Developer.Core/AutomaticResumeWorkerResult.cs`
- `src/TrailTrainer.Developer.Core/IAutomaticResumeWorker.cs`
- `src/TrailTrainer.Developer.Tasks/AutomaticResumeWorker.cs`
- `tests/TrailTrainer.Developer.Tests/AutomaticResumeWorkerTests.cs`
- `docs/developer-reviews/REVIEW-0032.md`

## Files Modified

None.

## Files Deleted

None.

## Architecture / Refactoring Notes

Provider-neutral request, result, and worker abstraction types are located in `TrailTrainer.Developer.Core`. The single-delegation worker boundary is located in `TrailTrainer.Developer.Tasks` and composes only the DEV-0031 executor abstraction. It is deliberately not a `BackgroundService` or `IHostedService`. No unrelated refactoring was performed.

## Tests Added

Injected-fake unit tests cover null request/result validation, exact wrapper identities, exactly one DEV-0031 invocation, exact request and cancellation-token delegation, exact result preservation for Finished, Failed, ImmediateWorkRemaining, and RunLimitReached, absence of outcome reinterpretation or additional invocation, null worker request short-circuiting, exception propagation without retry, pre-cancellation, executor cancellation identity, exact constructor dependency, and absence of a duplicate worker state enum.

All existing DEV-0002 through DEV-0031 tests continue to pass.

## Verification

### dotnet build

Succeeded for the complete solution: 0 warnings, 0 errors.

### dotnet test

Succeeded for the complete solution: 637 passed, 0 failed, 0 skipped.

### git diff --check

Succeeded with no whitespace errors.

## Deviations from DEV-0032

None.

## Open Issues / Known Limitations

None.

## Commit and Push
No commit created.
No push performed.
