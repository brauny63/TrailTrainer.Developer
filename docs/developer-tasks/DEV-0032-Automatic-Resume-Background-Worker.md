# DEV-0032 – Automatic Resume Background Worker

## Metadata
- Task ID: `DEV-0032`
- Repository: `TrailTrainer.Developer`
- Expected branch: `feature/dev-0032-automatic-resume-background-worker`
- Review report: `docs/developer-reviews/REVIEW-0032.md`
- Depends on: `DEV-0031`

## Goal
Add a host-neutral background-worker boundary that executes the bounded repeated delayed automatic resume flow from DEV-0031 exactly once per worker invocation.

DEV-0031 owns all repeated run/delay behavior. DEV-0032 must not duplicate that logic. It introduces the application boundary needed for a later concrete host/service.

The worker delegates exactly once to `IRepeatedDelayedAutomaticResumeExecutor`, preserves the exact result, and propagates cancellation and exceptions unchanged.

## Codex Execution Instructions
Work this Developer Task completely.

- Follow `docs/architecture/README.md`.
- Implement only the scope defined here.
- Reuse `IRepeatedDelayedAutomaticResumeExecutor`, `RepeatedDelayedAutomaticResumeRequest`, and `RepeatedDelayedAutomaticResumeResult` from DEV-0031.
- Keep provider-neutral contracts/models in `TrailTrainer.Developer.Core`.
- Put the implementation in `TrailTrainer.Developer.Tasks`.
- Do not duplicate DEV-0031 run/delay logic.
- Do not call DEV-0029, DEV-0028, or DEV-0027 directly.
- Do not call `Task.Delay`.
- Do not access persistence/discovery directly.
- Do not add filesystem, JSON, Git, GitHub REST, HTTP, process, shell, timer, cron, retry, polling, hosted-service, Windows-service, or CLI behavior.
- Do not modify this Developer Task or architecture documentation.
- Do not create a Git commit or push.
- After verification create `docs/developer-reviews/REVIEW-0032.md`.

## Request
### AutomaticResumeWorkerRequest
Immutable Core model exposing:
- `RepeatedDelayedAutomaticResumeRequest ExecutionRequest`

Requirements:
- reject null;
- preserve exact object identity;
- add no schedule, interval, retry, host, service, or persistence configuration.

## Result
### AutomaticResumeWorkerResult
Immutable Core model exposing:
- `RepeatedDelayedAutomaticResumeResult ExecutionResult`

Requirements:
- reject null;
- preserve exact result identity;
- do not introduce another state enum;
- do not reinterpret DEV-0031 outcome.

## Core Abstraction
### IAutomaticResumeWorker
Add a mockable asynchronous Core abstraction equivalent to:

```text
Task<AutomaticResumeWorkerResult> RunAsync(
    AutomaticResumeWorkerRequest request,
    CancellationToken cancellationToken = default)
```

## Concrete Worker
### AutomaticResumeWorker
Implement in `TrailTrainer.Developer.Tasks`.

Inject exactly:
- `IRepeatedDelayedAutomaticResumeExecutor`

Execution:
1. Receive `AutomaticResumeWorkerRequest`.
2. Invoke `IRepeatedDelayedAutomaticResumeExecutor.ExecuteAsync` exactly once.
3. Pass the exact `ExecutionRequest`.
4. Pass the exact caller cancellation token.
5. Await completion.
6. Return `AutomaticResumeWorkerResult` containing the exact executor result.

The worker must not inspect DEV-0031 state, run history, delay count, `ShouldRunAgain`, or `Immediate`.

## Single Delegation Guarantee
For every successful invocation:

```text
DEV-0031 executor invocation count == 1
```

DEV-0032 contains no execution loop.

## Failure Behavior
If DEV-0031 throws:
- propagate the exact exception;
- create no normal worker result;
- do not retry;
- do not invoke DEV-0031 again.

## Cancellation
Pass the exact caller `CancellationToken` to DEV-0031.
Cancellation propagates unchanged.
Do not create linked/replacement tokens or convert cancellation into a normal result.

## Tests
Use injected fakes/stubs only.

Cover at least:

1. Null ExecutionRequest rejected.
2. Exact ExecutionRequest identity preserved.
3. Null ExecutionResult rejected.
4. Exact ExecutionResult identity preserved.
5. No duplicate worker state enum.
6. Worker invokes DEV-0031 exactly once.
7. Exact ExecutionRequest delegated.
8. Exact caller cancellation token delegated.
9. Exact returned DEV-0031 result preserved.
10. Finished outcome does not cause another invocation.
11. Failed outcome does not cause another invocation.
12. ImmediateWorkRemaining does not cause another invocation.
13. RunLimitReached does not cause another invocation.
14. Worker performs no delay.
15. DEV-0031 exception propagates unchanged.
16. Exception is not retried.
17. Pre-cancelled operation propagates cancellation.
18. Cancellation from DEV-0031 propagates unchanged.
19. `AutomaticResumeWorker` has exactly one constructor dependency.
20. Dependency is exactly `IRepeatedDelayedAutomaticResumeExecutor`.
21. No direct DEV-0029 dependency.
22. No direct DEV-0028 dependency.
23. No direct DEV-0027 dependency.
24. No `IAsyncDelay` dependency.
25. No direct `Task.Delay`.
26. No persistence/discovery dependency.
27. No filesystem/JSON/Git/GitHub/process behavior.
28. No timer/polling/retry.
29. No hosted-service/Windows-service behavior.
30. No CLI behavior.
31. No internal loop.
32. Existing DEV-0002 through DEV-0031 tests continue to pass.

Avoid unrelated refactoring.

## Out of Scope
Do not implement:
- `BackgroundService` or `IHostedService`;
- Generic Host registration;
- DI composition root;
- Windows Service/systemd integration;
- automatic startup;
- recurring scheduling;
- timers or cron;
- polling;
- retry/backoff;
- persistence changes;
- filesystem/JSON changes;
- Git/GitHub operations;
- notifications;
- CLI;
- parallel worker execution;
- distributed locking;
- automatic next Developer Task selection;
- Codex execution.

These belong to later Developer Tasks.

## Verification
Run:

```text
dotnet build
dotnet test
git diff --check
```

Required:
- build succeeds with 0 errors and no new DEV-0032 warnings;
- all tests pass;
- no whitespace errors.

## Acceptance Criteria
DEV-0032 is complete when:

1. `AutomaticResumeWorkerRequest` exists and preserves an exact non-null DEV-0031 request.
2. `AutomaticResumeWorkerResult` exists and preserves an exact non-null DEV-0031 result.
3. No duplicate worker state enum is introduced.
4. `IAutomaticResumeWorker` exists in Core.
5. `AutomaticResumeWorker` exists in Tasks.
6. Worker depends exactly on `IRepeatedDelayedAutomaticResumeExecutor`.
7. Worker delegates exactly once per invocation.
8. Exact request and cancellation token are delegated.
9. Exact DEV-0031 result is preserved.
10. Worker does not inspect or reinterpret DEV-0031 outcome.
11. Exceptions and cancellation propagate unchanged.
12. No retry or loop is introduced.
13. No direct DEV-0027/DEV-0028/DEV-0029/IAsyncDelay dependency is introduced.
14. No persistence, filesystem, JSON, Git, GitHub, network, process, timer, polling, CLI, hosted-service, or Windows-service behavior is introduced.
15. Existing tests continue to pass.
16. `dotnet build`, `dotnet test`, and `git diff --check` succeed.
17. `docs/developer-reviews/REVIEW-0032.md` is created.

## Codex Completion Protocol
After implementation and verification:

1. Do not create a Git commit.
2. Do not push changes.
3. Do not modify this Developer Task.
4. Create `docs/developer-reviews/REVIEW-0032.md`.
5. Use:

```text
# REVIEW-0032 – Automatic Resume Background Worker

## Status
READY FOR REVIEW | BLOCKED

## Summary

## Requirements Implemented

## Files Created

## Files Modified

## Files Deleted

## Architecture / Refactoring Notes

## Tests Added

## Verification
### dotnet build
### dotnet test
### git diff --check

## Deviations from DEV-0032

## Open Issues / Known Limitations

## Commit and Push
No commit created.
No push performed.
```

6. Use `READY FOR REVIEW` only if all criteria and verification succeed; otherwise `BLOCKED`.
7. Record build warning/error counts, test passed/failed/skipped counts, and diff-check result.
8. List every created/modified/deleted file.
9. Write `None` when there are no deviations or open issues.
