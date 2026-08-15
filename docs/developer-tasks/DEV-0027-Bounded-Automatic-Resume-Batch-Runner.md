# DEV-0027 – Bounded Automatic Resume Batch Runner

## Metadata

- Task ID: `DEV-0027`
- Repository: `TrailTrainer.Developer`
- Expected branch: `feature/dev-0027-bounded-automatic-resume-batch-runner`
- Review report: `docs/developer-reviews/REVIEW-0027.md`
- Depends on: `DEV-0026`

## Goal

Add a provider-neutral bounded batch runner that repeatedly executes the single automatic resume batch step introduced by DEV-0026.

DEV-0026 intentionally processes at most one persisted lifecycle candidate and reports whether additional persisted work remains through `MoreWork`.

DEV-0027 introduces the first controlled continuation mechanism.

One invocation may execute multiple DEV-0026 steps, but must always be bounded by an explicit maximum step count.

The runner must stop when:

1. DEV-0026 reports `MoreWork == false`,
2. the configured maximum number of steps has been reached,
3. a step returns `Pending`,
4. a step returns `Failed`,
5. cancellation or an exception occurs.

DEV-0027 must not introduce scheduling, polling, delays, retries, background workers, CLI execution, Git operations, GitHub calls, or persistence access.

## Codex Execution Instructions

Work this Developer Task completely.

- Follow `docs/architecture/README.md`.
- Implement only the scope defined here.
- Reuse `IAutomaticResumeBatchStep` from DEV-0026.
- Reuse existing DEV-0026 request/result models where appropriate.
- Keep provider-neutral contracts/models in `TrailTrainer.Developer.Core`.
- Put orchestration in `TrailTrainer.Developer.Tasks`.
- Do not access persisted lifecycle state directly.
- Do not call DEV-0025 directly.
- Do not add filesystem, JSON, Git, GitHub REST, HTTP, process, shell, polling, retry, delay, scheduling, timers, or background workers.
- Do not modify this Developer Task or architecture documentation.
- Do not implement anything under **Out of Scope**.
- Do not create a Git commit for DEV-0027.
- Do not push the DEV-0027 implementation branch.
- After implementation and verification create `docs/developer-reviews/REVIEW-0027.md`.

If ambiguity prevents correct completion, document it and set the review status to `BLOCKED`.

## Scope

Implement one bounded automatic resume batch runner.

Conceptually:

```text
             start
               |
               v
        execute DEV-0026
               |
       +-------+--------+
       |                |
   MoreWork=false   MoreWork=true
       |                |
       v                v
      stop       inspect step state
                        |
          +-------------+-------------+
          |             |             |
       Pending        Failed       Completed
          |             |             |
          v             v             v
         stop          stop       limit reached?
                                      |
                              +-------+-------+
                              |               |
                             yes             no
                              |               |
                              v               v
                             stop      execute DEV-0026
```

`Empty` always terminates because DEV-0026 requires `MoreWork == false` for `Empty`.

The runner must never exceed the configured maximum number of DEV-0026 invocations.

## Request

### AutomaticResumeBatchRunRequest

Add an immutable provider-neutral request exposing:

- `AutomaticResumeBatchStepRequest StepRequest`
- `int MaximumSteps`

Requirements:

- `StepRequest` must not be null.
- `MaximumSteps` must be greater than zero.
- Preserve the exact `StepRequest` instance.
- Preserve `MaximumSteps` exactly.

Do not add:

- polling interval,
- delay,
- retry count,
- timeout,
- TaskId,
- candidate selection criteria,
- scheduler configuration.

## Run State

### AutomaticResumeBatchRunState

Add a strongly typed enum containing exactly:

- `Empty`
- `Completed`
- `Pending`
- `Failed`
- `LimitReached`

## Result

### AutomaticResumeBatchRunResult

Add an immutable provider-neutral result exposing at least:

- `AutomaticResumeBatchRunState State`
- `IReadOnlyList<AutomaticResumeBatchStepResult> Steps`
- `bool MoreWork`

Requirements:

- preserve the exact DEV-0026 result objects,
- preserve execution order,
- expose no mutable collection,
- contain at least one step for every successful return from the runner,
- reject unsupported enum values.

## Core Abstraction

### IAutomaticResumeBatchRunner

Add a mockable asynchronous provider-neutral abstraction equivalent to:

```text
RunAsync(
    AutomaticResumeBatchRunRequest request,
    CancellationToken cancellationToken = default)
```

Return `AutomaticResumeBatchRunResult`.

## Concrete Orchestration

### AutomaticResumeBatchRunner

Implement in `TrailTrainer.Developer.Tasks`.

Inject exactly:

- `IAutomaticResumeBatchStep`

Do not inject DEV-0025, discovery, state stores, Git, GitHub, or persistence implementations.

## Required Execution Rules

### Empty

If the first step returns `Empty`:
- stop immediately,
- return `Empty`,
- execute no further step.

### Pending

If any step returns `Pending`:
- stop immediately,
- return `Pending`,
- execute no further step.

### Failed

If any step returns `Failed`:
- stop immediately,
- return `Failed`,
- execute no further step.

### Completed with MoreWork false

Stop and return `Completed`.

### Completed with MoreWork true

If fewer than `MaximumSteps` steps have executed:
- execute DEV-0026 again.

If `MaximumSteps` has been reached:
- stop,
- return `LimitReached`,
- preserve `MoreWork == true`.

## Maximum Step Guarantee

For every invocation:

```text
DEV-0026 invocation count <= MaximumSteps
```

This is a hard invariant.

## Ordering

All DEV-0026 invocations must be sequential.

Do not:
- execute steps concurrently,
- prefetch another step,
- start the next step before the previous step completed.

The result list must reflect exact execution order.

## Failure Behavior

If DEV-0026 throws:
- propagate the exact exception,
- stop immediately,
- execute no additional step,
- do not convert the exception into `Failed`,
- do not retry.

## Cancellation

Pass the exact caller `CancellationToken` to every DEV-0026 invocation.

Cancellation must:
- propagate unchanged,
- stop the run,
- prevent any later DEV-0026 invocation.

## Tests

Use injected fakes/stubs only.

No test may require filesystem, JSON, Git, GitHub, network, HTTP, or child processes.

Cover at least:

1. Null StepRequest rejected.
2. MaximumSteps zero rejected.
3. Negative MaximumSteps rejected.
4. MaximumSteps one accepted.
5. StepRequest identity preserved.
6. MaximumSteps preserved exactly.
7. Unsupported run state rejected.
8. Empty invariants enforced.
9. Completed invariants enforced.
10. Pending invariants enforced.
11. Failed invariants enforced.
12. LimitReached invariants enforced.
13. Exact DEV-0026 result instances preserved.
14. Execution order preserved.
15. Steps collection cannot be mutated through the result API.
16. First Empty returns Empty.
17. Empty executes DEV-0026 exactly once.
18. Completed + MoreWork false returns Completed.
19. Multiple Completed steps terminate when MoreWork becomes false.
20. Pending stops immediately.
21. Failed stops immediately.
22. MaximumSteps 1 prevents a second step.
23. Completed + MoreWork true with MaximumSteps 1 returns LimitReached.
24. Runner executes exactly MaximumSteps when every step returns Completed + MoreWork true.
25. DEV-0026 invocation count never exceeds MaximumSteps.
26. Every invocation receives the exact StepRequest instance.
27. Every invocation receives the exact cancellation token.
28. DEV-0026 calls are sequential.
29. First DEV-0026 exception propagates.
30. Later DEV-0026 exception propagates.
31. Exception prevents subsequent invocation.
32. Exception is not converted into Failed.
33. No retry occurs.
34. Pre-cancelled operation propagates cancellation.
35. Cancellation during a later step propagates.
36. Cancellation prevents subsequent step.
37. Runner depends only on `IAutomaticResumeBatchStep`.
38. No filesystem/JSON/Git/GitHub/process behavior.
39. No scheduling/timer/delay behavior.
40. No polling.
41. No retry.
42. No concurrency.
43. Existing DEV-0002 through DEV-0026 tests continue to pass.

Avoid unrelated refactoring.

## Out of Scope

Do not implement:

- unlimited process-all behavior,
- scheduler,
- periodic execution,
- polling,
- wait loops,
- delay,
- timers,
- retry,
- retry backoff,
- background worker,
- Windows service,
- cron behavior,
- CLI command,
- automatic startup,
- filesystem or JSON persistence changes,
- Git operations,
- GitHub REST calls,
- CI lookup,
- parallel batch processing,
- multiple concurrent runners,
- distributed locking,
- task prioritization,
- automatic next Developer Task selection,
- Codex execution.

## Verification

Run:

```text
dotnet build
dotnet test
git diff --check
```

Required:

- build succeeds with 0 errors and no new DEV-0027 warnings,
- all tests pass,
- no whitespace errors.

## Acceptance Criteria

DEV-0027 is complete when:

1. `AutomaticResumeBatchRunRequest` exists.
2. Request requires a non-null DEV-0026 step request.
3. Request requires `MaximumSteps > 0`.
4. `AutomaticResumeBatchRunState` contains exactly Empty, Completed, Pending, Failed, LimitReached.
5. `AutomaticResumeBatchRunResult` exists with enforced invariants.
6. `IAutomaticResumeBatchRunner` exists as a mockable asynchronous Core abstraction.
7. `AutomaticResumeBatchRunner` exists in Tasks.
8. Runner depends only on `IAutomaticResumeBatchStep`.
9. DEV-0026 is always invoked sequentially.
10. Empty terminates immediately.
11. Pending terminates immediately.
12. Failed terminates immediately.
13. Completed + MoreWork false terminates as Completed.
14. Completed + MoreWork true continues only while capacity remains.
15. MaximumSteps is never exceeded.
16. LimitReached preserves MoreWork true.
17. Exact DEV-0026 result instances are preserved.
18. Result preserves execution order.
19. Exact caller cancellation token is propagated to every step.
20. Exceptions propagate unchanged.
21. No retries occur.
22. No polling, scheduling, delay, timers, background worker, filesystem, JSON, Git, GitHub, network, or process behavior is introduced.
23. Tests use injected fakes only.
24. Existing tests continue to pass.
25. `dotnet build` succeeds.
26. `dotnet test` succeeds.
27. `git diff --check` succeeds.
28. No out-of-scope functionality is implemented.
29. `docs/developer-reviews/REVIEW-0027.md` is created.

## Codex Completion Protocol

After implementation and verification:

1. Do **not** create a Git commit.
2. Do **not** push changes.
3. Do **not** modify this Developer Task.
4. Create `docs/developer-reviews/REVIEW-0027.md`.
5. Use:

```text
# REVIEW-0027 – Bounded Automatic Resume Batch Runner

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

## Deviations from DEV-0027

## Open Issues / Known Limitations

## Commit and Push
No commit created.
No push performed.
```

6. Use `READY FOR REVIEW` only if all acceptance criteria and verification succeed; otherwise `BLOCKED`.
7. Record build warning/error counts, test passed/failed/skipped counts, and diff-check result.
8. List every created/modified/deleted file.
9. Write `None` when there are no deviations or open issues.

The review report is part of DEV-0027 and must be included in the later Pull Request.
