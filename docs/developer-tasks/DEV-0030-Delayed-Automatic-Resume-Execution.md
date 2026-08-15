# DEV-0030 – Delayed Automatic Resume Execution

## Metadata

- Task ID: `DEV-0030`
- Repository: `TrailTrainer.Developer`
- Expected branch: `feature/dev-0030-delayed-automatic-resume-execution`
- Review report: `docs/developer-reviews/REVIEW-0030.md`
- Depends on: `DEV-0029`

## Goal

Add a provider-neutral delayed execution component that consumes the result of the DEV-0029 automatic resume run orchestrator and, only when that result requests a non-immediate later run, waits for an explicitly supplied delay and executes DEV-0029 once more.

DEV-0029 already handles all immediate continuation internally and stops with `ResumeLater` when work should be resumed later. DEV-0030 introduces the first explicit time boundary for that `ResumeLater` case.

DEV-0030 must remain bounded: one invocation may perform the initial DEV-0029 run and at most one delayed DEV-0029 run.

It must not poll repeatedly, retry failures, schedule recurring work, create background workers, hosted services, CLI commands, persistence access, Git operations, or GitHub calls.

## Codex Execution Instructions

Work this Developer Task completely.

- Follow `docs/architecture/README.md`.
- Implement only the scope defined here.
- Reuse `IAutomaticResumeRunOrchestrator`, `AutomaticResumeRunRequest`, and `AutomaticResumeRunResult` from DEV-0029.
- Keep provider-neutral contracts/models in `TrailTrainer.Developer.Core`.
- Put orchestration in `TrailTrainer.Developer.Tasks`.
- Introduce a mockable provider-neutral delay abstraction; do not call `Task.Delay` directly from the orchestrator.
- Do not access persisted lifecycle state directly.
- Do not call DEV-0027 or DEV-0028 directly.
- Do not implement recurring scheduling or polling.
- Do not add filesystem, JSON, Git, GitHub REST, HTTP, process, shell, background workers, hosted services, or CLI behavior.
- Do not modify this Developer Task or architecture documentation.
- Do not implement anything under **Out of Scope**.
- Do not create a Git commit for DEV-0030.
- Do not push the DEV-0030 implementation branch.
- After implementation and verification create `docs/developer-reviews/REVIEW-0030.md`.

If ambiguity prevents correct completion, document it and set the review status to `BLOCKED`.

## Scope

Implement one bounded delayed automatic resume execution.

Conceptually:

```text
execute DEV-0029
      |
      v
inspect result
      |
      +-- Finished -------> stop
      +-- Failed ---------> stop
      +-- LimitReached ---> stop
      |
      +-- ResumeLater
             |
             v
       wait configured delay
             |
             v
       execute DEV-0029 once
             |
             v
            stop
```

There is no loop.

A DEV-0030 invocation executes DEV-0029 at most twice.

## Request

### DelayedAutomaticResumeRequest

Add an immutable provider-neutral request exposing:

- `AutomaticResumeRunRequest RunRequest`
- `TimeSpan ResumeDelay`

Requirements:

- `RunRequest` must not be null.
- `ResumeDelay` must be greater than `TimeSpan.Zero`.
- Preserve the exact `RunRequest` instance.
- Preserve `ResumeDelay` exactly.

Do not add:

- retry count,
- polling interval,
- recurring schedule,
- maximum attempts,
- candidate identity,
- persistence configuration.

## Execution State

### DelayedAutomaticResumeState

Add a strongly typed enum containing exactly:

- `Finished`
- `Failed`
- `ImmediateWorkRemaining`
- `ResumeLater`
- `DelayedRunCompleted`

Semantics:

### Finished

The first DEV-0029 run returned `Finished`.

### Failed

The first DEV-0029 run returned `Failed`.

### ImmediateWorkRemaining

The first DEV-0029 run returned `LimitReached`.

DEV-0029 has already reached its own immediate safety bound. DEV-0030 must not reinterpret this as delayed work and must not wait.

### ResumeLater

Reserved for a valid result model representing delayed work before execution. The concrete DEV-0030 orchestration must not normally return this state because it performs the one allowed delay before returning.

### DelayedRunCompleted

The first DEV-0029 run returned `ResumeLater`, the configured delay completed, and a second DEV-0029 run completed successfully as a normal result.

The second run's own state is preserved in the result and is not recursively acted upon.

## Result

### DelayedAutomaticResumeResult

Add an immutable provider-neutral result exposing at least:

- `DelayedAutomaticResumeState State`
- `AutomaticResumeRunResult InitialRun`
- `AutomaticResumeRunResult? DelayedRun`
- `bool DelayExecuted`

Requirements:

- preserve exact DEV-0029 result instances,
- reject null `InitialRun`,
- enforce state/result invariants,
- expose no mutable state.

Required invariants:

### Finished

- `InitialRun.State == Finished`
- `DelayedRun == null`
- `DelayExecuted == false`

### Failed

- `InitialRun.State == Failed`
- `DelayedRun == null`
- `DelayExecuted == false`

### ImmediateWorkRemaining

- `InitialRun.State == LimitReached`
- `DelayedRun == null`
- `DelayExecuted == false`

### ResumeLater

- `InitialRun.State == ResumeLater`
- `DelayedRun == null`
- `DelayExecuted == false`

### DelayedRunCompleted

- `InitialRun.State == ResumeLater`
- `DelayedRun != null`
- `DelayExecuted == true`

For `DelayedRunCompleted`, do not restrict the state of `DelayedRun`; preserve whatever valid DEV-0029 result was returned.

## Delay Abstraction

### IAsyncDelay

Add a mockable provider-neutral abstraction equivalent to:

```text
Task DelayAsync(
    TimeSpan delay,
    CancellationToken cancellationToken = default)
```

The abstraction belongs in `TrailTrainer.Developer.Core`.

### SystemAsyncDelay

Add the production implementation in `TrailTrainer.Developer.Tasks`.

It may use `Task.Delay`.

It must:

- pass the exact delay,
- pass the exact cancellation token,
- add no retry/polling behavior.

This is the only DEV-0030 production class allowed to call `Task.Delay`.

## Core Orchestration Abstraction

### IDelayedAutomaticResumeExecutor

Add a mockable asynchronous provider-neutral abstraction equivalent to:

```text
Task<DelayedAutomaticResumeResult> ExecuteAsync(
    DelayedAutomaticResumeRequest request,
    CancellationToken cancellationToken = default)
```

## Concrete Orchestration

### DelayedAutomaticResumeExecutor

Implement in `TrailTrainer.Developer.Tasks`.

Inject exactly:

- `IAutomaticResumeRunOrchestrator`
- `IAsyncDelay`

Execution:

1. Execute DEV-0029 using the exact `RunRequest` and exact caller cancellation token.
2. If the result is `Finished`, return `Finished`.
3. If the result is `Failed`, return `Failed`.
4. If the result is `LimitReached`, return `ImmediateWorkRemaining`.
5. If the result is `ResumeLater`:
   - call `IAsyncDelay.DelayAsync` exactly once with the exact configured `ResumeDelay` and caller cancellation token,
   - after successful delay completion execute DEV-0029 exactly once more with the same exact `RunRequest` and caller cancellation token,
   - return `DelayedRunCompleted`,
   - preserve both exact DEV-0029 results.
6. Do not inspect or act on the second DEV-0029 result beyond preserving it.

## Bounded Execution Guarantee

For every invocation:

```text
DEV-0029 invocation count <= 2
delay invocation count <= 1
```

No execution path may exceed these bounds.

There is no loop.

## Trust Boundary

DEV-0030 uses only `AutomaticResumeRunResult.State` to determine whether the single delay is needed.

It must not:

- inspect DEV-0029 batch runs or decisions,
- call DEV-0028,
- call DEV-0027,
- inspect persisted lifecycle state,
- reinterpret `ShouldRunAgain` or `Immediate` to override the DEV-0029 state.

## Failure Behavior

If the first DEV-0029 invocation throws:

- propagate the exact exception,
- do not delay,
- do not execute a second run.

If `IAsyncDelay` throws:

- propagate the exact exception,
- do not execute a second run.

If the second DEV-0029 invocation throws:

- propagate the exact exception,
- do not retry.

Do not convert exceptions into normal result states.

## Cancellation

Pass the exact caller `CancellationToken` to:

- the first DEV-0029 invocation,
- the delay,
- the second DEV-0029 invocation.

Cancellation must propagate unchanged and prevent all later operations.

A pre-cancelled token must not be converted into a normal result.

## Tests

Use injected fakes/stubs for orchestration tests.

No orchestration test may wait in real time.

Cover at least:

### Request

1. Null RunRequest rejected.
2. Zero ResumeDelay rejected.
3. Negative ResumeDelay rejected.
4. Positive ResumeDelay accepted.
5. Exact RunRequest identity preserved.
6. ResumeDelay preserved exactly.

### Result

7. Null InitialRun rejected.
8. Unsupported state rejected.
9. Finished invariants enforced.
10. Failed invariants enforced.
11. ImmediateWorkRemaining invariants enforced.
12. ResumeLater invariants enforced.
13. DelayedRunCompleted invariants enforced.
14. Exact InitialRun identity preserved.
15. Exact DelayedRun identity preserved.

### Initial terminal states

16. Finished executes DEV-0029 once.
17. Finished performs no delay.
18. Failed executes DEV-0029 once.
19. Failed performs no delay.
20. LimitReached maps to ImmediateWorkRemaining.
21. LimitReached performs no delay.
22. LimitReached executes no second run.

### Delayed resume

23. ResumeLater invokes delay exactly once.
24. Delay receives exact ResumeDelay.
25. Delay receives exact cancellation token.
26. Second DEV-0029 run executes only after delay completes.
27. Second run receives exact same RunRequest instance.
28. Second run receives exact cancellation token.
29. DEV-0029 executes exactly twice for ResumeLater.
30. Result state is DelayedRunCompleted.
31. Exact first and second run results are preserved.
32. Second Finished result is preserved without further action.
33. Second ResumeLater result is preserved without another delay.
34. Second Failed result is preserved without retry.
35. Second LimitReached result is preserved without immediate continuation by DEV-0030.

### Exceptions

36. First DEV-0029 exception propagates.
37. First DEV-0029 exception prevents delay.
38. Delay exception propagates.
39. Delay exception prevents second run.
40. Second DEV-0029 exception propagates.
41. Second DEV-0029 exception is not retried.
42. Exceptions are not converted into normal states.

### Cancellation

43. Pre-cancelled operation propagates cancellation.
44. Cancellation during first run prevents delay.
45. Cancellation during delay prevents second run.
46. Cancellation during second run propagates.
47. Exact cancellation token is passed everywhere.

### Delay implementation

48. `SystemAsyncDelay` completes for a valid delay.
49. `SystemAsyncDelay` honors cancellation.
50. Production executor does not call `Task.Delay` directly.

### Architecture

51. Executor depends exactly on `IAutomaticResumeRunOrchestrator` and `IAsyncDelay`.
52. No DEV-0027 dependency.
53. No DEV-0028 dependency.
54. No persistence/discovery dependency.
55. No filesystem/JSON/Git/GitHub/process behavior.
56. No polling loop.
57. No retry.
58. No recurring scheduling.
59. No background worker/hosted service.
60. DEV-0029 invocation count never exceeds two.
61. Delay invocation count never exceeds one.

### Regression

62. Existing DEV-0002 through DEV-0029 tests continue to pass.

Avoid unrelated refactoring.

## Out of Scope

Do not implement:

- repeated delayed resume,
- polling,
- recurring scheduling,
- cron behavior,
- scheduler service,
- background worker,
- hosted service,
- Windows service,
- retry,
- retry backoff,
- retry counters,
- multiple delays,
- persistence changes,
- filesystem or JSON changes,
- Git operations,
- GitHub REST calls,
- CI lookup,
- notifications,
- CLI command,
- automatic startup,
- parallel execution,
- distributed locking,
- automatic next Developer Task selection,
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

- build succeeds with 0 errors and no new DEV-0030 warnings,
- all tests pass,
- no whitespace errors.

## Acceptance Criteria

DEV-0030 is complete when:

1. `DelayedAutomaticResumeRequest` exists and requires a non-null DEV-0029 request.
2. ResumeDelay must be greater than zero.
3. `DelayedAutomaticResumeState` contains exactly Finished, Failed, ImmediateWorkRemaining, ResumeLater, DelayedRunCompleted.
4. `DelayedAutomaticResumeResult` exists with enforced invariants.
5. `IAsyncDelay` exists as a mockable Core abstraction.
6. `SystemAsyncDelay` exists as the production delay implementation.
7. `IDelayedAutomaticResumeExecutor` exists as a mockable asynchronous Core abstraction.
8. `DelayedAutomaticResumeExecutor` exists in Tasks.
9. Executor depends exactly on DEV-0029 orchestrator and IAsyncDelay.
10. Finished performs no delay or second run.
11. Failed performs no delay or second run.
12. LimitReached performs no delay or second run and maps to ImmediateWorkRemaining.
13. ResumeLater performs exactly one configured delay.
14. ResumeLater performs exactly one second DEV-0029 run after the delay.
15. The second DEV-0029 result is preserved but never recursively acted upon.
16. DEV-0029 is invoked at most twice.
17. Delay is invoked at most once.
18. Exact request, result, delay, and cancellation-token values are preserved/delegated.
19. Exceptions and cancellation propagate unchanged.
20. No retries occur.
21. No polling or recurring scheduling is introduced.
22. No persistence, filesystem, JSON, Git, GitHub, network, process, CLI, background-worker, or hosted-service behavior is introduced.
23. Tests use injected fakes and do not wait in real time for orchestration tests.
24. Existing tests continue to pass.
25. `dotnet build` succeeds.
26. `dotnet test` succeeds.
27. `git diff --check` succeeds.
28. No out-of-scope functionality is implemented.
29. `docs/developer-reviews/REVIEW-0030.md` is created.

## Codex Completion Protocol

After implementation and verification:

1. Do **not** create a Git commit.
2. Do **not** push changes.
3. Do **not** modify this Developer Task.
4. Create `docs/developer-reviews/REVIEW-0030.md`.
5. Use:

```text
# REVIEW-0030 – Delayed Automatic Resume Execution

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

## Deviations from DEV-0030

## Open Issues / Known Limitations

## Commit and Push
No commit created.
No push performed.
```

6. Use `READY FOR REVIEW` only if all acceptance criteria and verification succeed; otherwise `BLOCKED`.
7. Record build warning/error counts, test passed/failed/skipped counts, and diff-check result.
8. List every created/modified/deleted file.
9. Write `None` when there are no deviations or open issues.

The review report is part of DEV-0030 and must be included in the later Pull Request.
