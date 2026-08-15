# DEV-0029 – Automatic Resume Run Orchestrator

## Metadata

- Task ID: `DEV-0029`
- Repository: `TrailTrainer.Developer`
- Expected branch: `feature/dev-0029-automatic-resume-run-orchestrator`
- Review report: `docs/developer-reviews/REVIEW-0029.md`
- Depends on: `DEV-0028`

## Goal

Add a provider-neutral orchestration component that executes bounded automatic resume batch runs from DEV-0027 and applies the scheduling decision from DEV-0028.

DEV-0029 is the first layer allowed to react to `ContinueImmediately` by executing another bounded batch run in the same invocation.

It must stop when the scheduling decision is:

- `Finished`
- `ResumeLater`
- `StopFailed`

It may continue only while the scheduling decision is `ContinueImmediately`.

The entire orchestration must itself be bounded by an explicit maximum number of batch runs so that a permanently replenished workload cannot create an unbounded loop.

DEV-0029 must not introduce timers, delays, polling, retries, background workers, hosted services, CLI behavior, persistence access, Git operations, GitHub calls, or scheduling infrastructure.

## Codex Execution Instructions

Work this Developer Task completely.

- Follow `docs/architecture/README.md`.
- Implement only the scope defined here.
- Reuse `IAutomaticResumeBatchRunner` from DEV-0027.
- Reuse `IAutomaticResumeSchedulingDecision` from DEV-0028.
- Reuse existing DEV-0027/DEV-0028 request and result models where appropriate.
- Keep provider-neutral contracts/models in `TrailTrainer.Developer.Core`.
- Put orchestration in `TrailTrainer.Developer.Tasks`.
- Do not access persisted lifecycle state directly.
- Do not call DEV-0026 directly.
- Do not implement timing or scheduling infrastructure.
- Do not add filesystem, JSON, Git, GitHub REST, HTTP, process, shell, polling, retry, delay, timers, clocks, background workers, hosted services, or CLI behavior.
- Do not modify this Developer Task or architecture documentation.
- Do not implement anything under **Out of Scope**.
- Do not create a Git commit for DEV-0029.
- Do not push the DEV-0029 implementation branch.
- After implementation and verification create `docs/developer-reviews/REVIEW-0029.md`.

If ambiguity prevents correct completion, document it and set the review status to `BLOCKED`.

## Scope

Implement one bounded automatic resume run orchestrator.

Conceptually:

```text
start
  |
  v
execute DEV-0027
  |
  v
apply DEV-0028 decision
  |
  +-- Finished ----------> stop
  |
  +-- ResumeLater -------> stop
  |
  +-- StopFailed --------> stop
  |
  +-- ContinueImmediately
            |
            v
      run limit reached?
        |           |
       yes          no
        |           |
        v           v
       stop     execute DEV-0027 again
```

The orchestrator must never exceed its configured maximum number of DEV-0027 batch-run invocations.

## Request

### AutomaticResumeRunRequest

Add an immutable provider-neutral request exposing:

- `AutomaticResumeBatchRunRequest BatchRunRequest`
- `int MaximumBatchRuns`

Requirements:

- `BatchRunRequest` must not be null.
- `MaximumBatchRuns` must be greater than zero.
- Preserve the exact `BatchRunRequest` instance.
- Preserve `MaximumBatchRuns` exactly.

Do not add:

- delay,
- polling interval,
- retry count,
- timeout,
- clock,
- schedule,
- candidate identity.

## Run State

### AutomaticResumeRunState

Add a strongly typed enum containing exactly:

- `Finished`
- `ResumeLater`
- `Failed`
- `LimitReached`

Semantics:

### Finished

The final DEV-0028 decision was `Finished`.

### ResumeLater

The final DEV-0028 decision was `ResumeLater`.

No waiting or later scheduling is performed by DEV-0029.

### Failed

The final DEV-0028 decision was `StopFailed`.

### LimitReached

The final DEV-0028 decision was `ContinueImmediately`, but `MaximumBatchRuns` had been reached.

Additional immediate work remains, but DEV-0029 stops because of its own safety bound.

## Result

### AutomaticResumeRunResult

Add an immutable provider-neutral result exposing at least:

- `AutomaticResumeRunState State`
- `IReadOnlyList<AutomaticResumeBatchRunResult> BatchRuns`
- `IReadOnlyList<AutomaticResumeSchedulingDecision> Decisions`
- `bool ShouldRunAgain`
- `bool Immediate`

Required final mapping:

| Final DEV-0028 decision | Run state | ShouldRunAgain | Immediate |
|---|---|---:|---:|
| Finished | Finished | false | false |
| ResumeLater | ResumeLater | true | false |
| StopFailed | Failed | false | false |
| ContinueImmediately with limit reached | LimitReached | true | true |

Requirements:

- preserve exact DEV-0027 batch result instances,
- preserve exact DEV-0028 decision instances,
- preserve execution order,
- expose no mutable collections,
- `BatchRuns.Count == Decisions.Count`,
- contain at least one batch run and one decision,
- each decision must reference the corresponding exact batch result,
- reject unsupported enum values,
- enforce the state/flag invariants above.

## Core Abstraction

### IAutomaticResumeRunOrchestrator

Add a mockable asynchronous provider-neutral abstraction equivalent to:

```text
RunAsync(
    AutomaticResumeRunRequest request,
    CancellationToken cancellationToken = default)
```

Return `AutomaticResumeRunResult`.

## Concrete Orchestration

### AutomaticResumeRunOrchestrator

Implement in `TrailTrainer.Developer.Tasks`.

Inject exactly:

- `IAutomaticResumeBatchRunner`
- `IAutomaticResumeSchedulingDecision`

Do not inject:

- DEV-0026 abstractions,
- lifecycle persistence/discovery,
- Git abstractions,
- GitHub abstractions,
- timers,
- clocks,
- delay providers.

## Required Execution Rules

For every batch-run iteration:

1. Execute DEV-0027 with the exact `BatchRunRequest`.
2. Pass the exact caller `CancellationToken`.
3. Pass the exact returned DEV-0027 result to DEV-0028.
4. Preserve both exact returned objects.
5. Inspect only the DEV-0028 decision state to determine whether orchestration continues.

### Finished

If DEV-0028 returns `Finished`:

- stop immediately,
- return `AutomaticResumeRunState.Finished`,
- execute no additional batch.

### ResumeLater

If DEV-0028 returns `ResumeLater`:

- stop immediately,
- return `AutomaticResumeRunState.ResumeLater`,
- preserve `ShouldRunAgain == true`,
- preserve `Immediate == false`,
- do not wait,
- do not schedule,
- do not execute another batch.

### StopFailed

If DEV-0028 returns `StopFailed`:

- stop immediately,
- return `AutomaticResumeRunState.Failed`,
- execute no additional batch,
- do not retry.

### ContinueImmediately

If DEV-0028 returns `ContinueImmediately` and fewer than `MaximumBatchRuns` batch runs have executed:

- execute DEV-0027 again immediately in the same orchestration call.

If `MaximumBatchRuns` has been reached:

- stop,
- return `AutomaticResumeRunState.LimitReached`,
- preserve `ShouldRunAgain == true`,
- preserve `Immediate == true`.

## Maximum Batch Run Guarantee

For every invocation:

```text
DEV-0027 invocation count <= MaximumBatchRuns
```

This is a hard invariant.

`MaximumBatchRuns` is a safety boundary.

No execution path may exceed it.

## Ordering

All DEV-0027 invocations must be sequential.

For each iteration:

```text
DEV-0027 batch run
        |
        v
DEV-0028 decision
        |
        v
possible next DEV-0027 batch run
```

Do not:

- execute batch runs concurrently,
- prefetch another batch,
- make a decision before its batch completes,
- execute another batch before the current decision is available.

## Trust Boundary

DEV-0029 must use DEV-0028 as the continuation authority.

It must not:

- inspect individual DEV-0026 steps,
- independently reinterpret DEV-0027 `State`,
- independently inspect `MoreWork` to override DEV-0028,
- access persistence/discovery,
- derive candidate identities.

## Failure Behavior

If DEV-0027 throws:

- propagate the exact exception,
- stop immediately,
- execute no later decision or batch,
- do not convert it into `Failed`,
- do not retry.

If DEV-0028 throws:

- propagate the exact exception,
- stop immediately,
- execute no later batch,
- do not convert it into `Failed`,
- do not retry.

A valid `StopFailed` decision is a normal orchestration result.

A thrown exception remains an exception.

## Cancellation

Pass the exact caller `CancellationToken` to every DEV-0027 invocation.

Cancellation must:

- propagate unchanged,
- stop orchestration,
- prevent all later batch runs and decisions.

Do not convert cancellation into a normal run result.

## Tests

Use injected fakes/stubs only.

No test may require filesystem, JSON, Git, GitHub, network, HTTP, timers, clocks, delays, or child processes.

Cover at least:

### Request

1. Null BatchRunRequest rejected.
2. MaximumBatchRuns zero rejected.
3. Negative MaximumBatchRuns rejected.
4. MaximumBatchRuns one accepted.
5. Exact BatchRunRequest identity preserved.
6. MaximumBatchRuns preserved exactly.

### Result

7. Unsupported run state rejected.
8. Empty BatchRuns rejected.
9. Empty Decisions rejected.
10. Mismatched BatchRuns/Decisions counts rejected.
11. Decision-to-batch identity mismatch rejected.
12. Finished invariants enforced.
13. ResumeLater invariants enforced.
14. Failed invariants enforced.
15. LimitReached invariants enforced.
16. Exact batch result instances preserved.
17. Exact decision instances preserved.
18. Execution order preserved.
19. Collections cannot be mutated through the result API.

### Finished

20. First Finished decision returns Finished.
21. Finished executes one batch only.
22. Finished returns ShouldRunAgain false.
23. Finished returns Immediate false.

### ResumeLater

24. First ResumeLater returns ResumeLater.
25. ResumeLater stops immediately.
26. ResumeLater after immediate continuations stops immediately.
27. ResumeLater returns ShouldRunAgain true.
28. ResumeLater returns Immediate false.
29. No batch executes after ResumeLater.

### Failed

30. First StopFailed returns Failed.
31. StopFailed stops immediately.
32. StopFailed after immediate continuations stops immediately.
33. Failed returns ShouldRunAgain false.
34. Failed returns Immediate false.
35. No batch executes after StopFailed.

### Immediate continuation

36. ContinueImmediately executes another batch when capacity remains.
37. Multiple ContinueImmediately decisions execute sequential batches.
38. Continuation stops when a later Finished decision occurs.
39. Continuation stops when a later ResumeLater decision occurs.
40. Continuation stops when a later StopFailed decision occurs.
41. Every DEV-0027 result is passed exactly to DEV-0028.

### Limit

42. MaximumBatchRuns 1 prevents a second batch.
43. ContinueImmediately with MaximumBatchRuns 1 returns LimitReached.
44. Runner executes exactly MaximumBatchRuns when every decision is ContinueImmediately.
45. DEV-0027 invocation count never exceeds MaximumBatchRuns.
46. LimitReached returns ShouldRunAgain true.
47. LimitReached returns Immediate true.

### Delegation

48. Every DEV-0027 invocation receives the exact BatchRunRequest.
49. Every DEV-0027 invocation receives the exact cancellation token.
50. Every DEV-0028 invocation receives the exact corresponding DEV-0027 result.
51. DEV-0027 calls are sequential.
52. DEV-0028 is called exactly once per successful DEV-0027 result.
53. No DEV-0026 call exists.
54. No discovery/persistence dependency exists.

### Exceptions

55. First DEV-0027 exception propagates.
56. Later DEV-0027 exception propagates.
57. DEV-0027 exception prevents decision and later batch.
58. First DEV-0028 exception propagates.
59. Later DEV-0028 exception propagates.
60. DEV-0028 exception prevents later batch.
61. Exceptions are not converted into Failed.
62. No retry occurs.

### Cancellation

63. Pre-cancelled operation propagates cancellation.
64. Cancellation during a later DEV-0027 batch propagates.
65. Cancellation prevents later batch and decision.
66. Cancellation is not converted into a normal result.

### Architecture

67. Orchestrator depends exactly on `IAutomaticResumeBatchRunner` and `IAutomaticResumeSchedulingDecision`.
68. No DEV-0026 dependency.
69. No persistence/discovery dependency.
70. No filesystem/JSON/Git/GitHub/process behavior.
71. No clock/timer/delay behavior.
72. No scheduler/background worker.
73. No polling.
74. No retry.
75. No concurrency.

### Regression

76. Existing DEV-0002 through DEV-0028 tests continue to pass.

Avoid unrelated refactoring.

## Out of Scope

Do not implement:

- delayed resume,
- scheduler,
- periodic execution,
- polling,
- polling intervals,
- wait loops,
- delay,
- timers,
- clocks,
- retry,
- retry backoff,
- background worker,
- hosted service,
- Windows service,
- cron behavior,
- CLI command,
- automatic startup,
- persistence changes,
- filesystem or JSON changes,
- Git operations,
- GitHub REST calls,
- CI lookup,
- notifications,
- parallel batch execution,
- multiple concurrent orchestrators,
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

- build succeeds with 0 errors and no new DEV-0029 warnings,
- all tests pass,
- no whitespace errors.

## Acceptance Criteria

DEV-0029 is complete when:

1. `AutomaticResumeRunRequest` exists.
2. Request requires a non-null DEV-0027 batch request.
3. Request requires `MaximumBatchRuns > 0`.
4. `AutomaticResumeRunState` contains exactly Finished, ResumeLater, Failed, LimitReached.
5. `AutomaticResumeRunResult` exists with enforced invariants.
6. `IAutomaticResumeRunOrchestrator` exists as a mockable asynchronous Core abstraction.
7. `AutomaticResumeRunOrchestrator` exists in Tasks.
8. Orchestrator depends exactly on DEV-0027 runner and DEV-0028 decision abstraction.
9. DEV-0027 is always invoked sequentially.
10. Every successful DEV-0027 result is passed exactly once to DEV-0028.
11. Finished terminates immediately.
12. ResumeLater terminates immediately without waiting or scheduling.
13. StopFailed terminates immediately without retry.
14. ContinueImmediately starts another batch only while capacity remains.
15. MaximumBatchRuns is never exceeded.
16. LimitReached preserves ShouldRunAgain true and Immediate true.
17. Exact DEV-0027 and DEV-0028 result instances are preserved.
18. Result preserves execution order and batch/decision correspondence.
19. Exact caller cancellation token is propagated to every DEV-0027 call.
20. Exceptions propagate unchanged.
21. No retries occur.
22. DEV-0028 is the sole continuation authority.
23. No polling, scheduling, delay, timers, clocks, background worker, persistence, filesystem, JSON, Git, GitHub, network, process, or CLI behavior is introduced.
24. Tests use injected fakes only.
25. Existing tests continue to pass.
26. `dotnet build` succeeds.
27. `dotnet test` succeeds.
28. `git diff --check` succeeds.
29. No out-of-scope functionality is implemented.
30. `docs/developer-reviews/REVIEW-0029.md` is created.

## Codex Completion Protocol

After implementation and verification:

1. Do **not** create a Git commit.
2. Do **not** push changes.
3. Do **not** modify this Developer Task.
4. Create `docs/developer-reviews/REVIEW-0029.md`.
5. Use:

```text
# REVIEW-0029 – Automatic Resume Run Orchestrator

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

## Deviations from DEV-0029

## Open Issues / Known Limitations

## Commit and Push
No commit created.
No push performed.
```

6. Use `READY FOR REVIEW` only if all acceptance criteria and verification succeed; otherwise `BLOCKED`.
7. Record build warning/error counts, test passed/failed/skipped counts, and diff-check result.
8. List every created/modified/deleted file.
9. Write `None` when there are no deviations or open issues.

The review report is part of DEV-0029 and must be included in the later Pull Request.
