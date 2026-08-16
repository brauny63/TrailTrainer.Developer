# DEV-0031 – Repeated Delayed Automatic Resume Loop

## Metadata

- Task ID: `DEV-0031`
- Repository: `TrailTrainer.Developer`
- Expected branch: `feature/dev-0031-repeated-delayed-automatic-resume-loop`
- Review report: `docs/developer-reviews/REVIEW-0031.md`
- Depends on: `DEV-0030`

## Goal

Add a provider-neutral bounded loop that repeatedly executes DEV-0029 and performs delayed resumes whenever DEV-0029 returns `ResumeLater`.

DEV-0030 introduced exactly one delayed continuation. DEV-0031 generalizes that concept into a bounded repeated delayed-resume loop while preserving strict safety limits.

One invocation may:

1. execute DEV-0029,
2. stop for `Finished`, `Failed`, or `LimitReached`,
3. for `ResumeLater`, wait the configured delay,
4. execute DEV-0029 again,
5. repeat only while capacity remains.

DEV-0031 must be bounded by an explicit maximum number of DEV-0029 runs. It must not become an infinite polling service or background worker.

## Codex Execution Instructions

Work this Developer Task completely.

- Follow `docs/architecture/README.md`.
- Implement only the scope defined here.
- Reuse `IAutomaticResumeRunOrchestrator`, `AutomaticResumeRunRequest`, and `AutomaticResumeRunResult` from DEV-0029.
- Reuse `IAsyncDelay` from DEV-0030.
- Keep provider-neutral contracts/models in `TrailTrainer.Developer.Core`.
- Put orchestration in `TrailTrainer.Developer.Tasks`.
- Do not call `Task.Delay` directly; use `IAsyncDelay`.
- Do not call DEV-0027 or DEV-0028 directly.
- Do not access lifecycle persistence/discovery directly.
- Do not implement hosted services, background workers, cron, CLI behavior, Git, GitHub, filesystem, JSON, HTTP, shell, or process behavior.
- Do not modify this Developer Task or architecture documentation.
- Do not implement anything under **Out of Scope**.
- Do not create a Git commit for DEV-0031.
- Do not push the DEV-0031 implementation branch.
- After implementation and verification create `docs/developer-reviews/REVIEW-0031.md`.

If ambiguity prevents correct completion, document it and set the review status to `BLOCKED`.

## Scope

Conceptually:

```text
execute DEV-0029
      |
      v
inspect state
      |
      +-- Finished -------> stop Finished
      +-- Failed ---------> stop Failed
      +-- LimitReached ---> stop ImmediateWorkRemaining
      |
      +-- ResumeLater
             |
             v
       run limit reached?
          |       |
         yes      no
          |       |
          v       v
         stop   delay
                  |
                  v
            execute DEV-0029
                  |
                  +---- repeat
```

The loop is bounded by `MaximumRuns`.

## Request

### RepeatedDelayedAutomaticResumeRequest

Add an immutable provider-neutral request exposing:

- `AutomaticResumeRunRequest RunRequest`
- `TimeSpan ResumeDelay`
- `int MaximumRuns`

Requirements:

- `RunRequest` must not be null.
- `ResumeDelay` must be greater than `TimeSpan.Zero`.
- `MaximumRuns` must be greater than zero.
- Preserve the exact `RunRequest` instance.
- Preserve `ResumeDelay` exactly.
- Preserve `MaximumRuns` exactly.

`MaximumRuns` includes the initial DEV-0029 invocation.

## State

### RepeatedDelayedAutomaticResumeState

Add an enum containing exactly:

- `Finished`
- `Failed`
- `ImmediateWorkRemaining`
- `DelayedWorkRemaining`
- `RunLimitReached`

Semantics:

- `Finished`: final DEV-0029 result is `Finished`.
- `Failed`: final DEV-0029 result is `Failed`.
- `ImmediateWorkRemaining`: final DEV-0029 result is `LimitReached`.
- `DelayedWorkRemaining`: reserved for result-model validation of delayed work that remains resumable.
- `RunLimitReached`: final DEV-0029 result is `ResumeLater`, but `MaximumRuns` has been reached before another delayed run may execute.

## Result

### RepeatedDelayedAutomaticResumeResult

Add an immutable provider-neutral result exposing at least:

- `RepeatedDelayedAutomaticResumeState State`
- `IReadOnlyList<AutomaticResumeRunResult> Runs`
- `int DelayCount`
- `bool ShouldRunAgain`
- `bool Immediate`

Required final mapping:

| Final condition | State | ShouldRunAgain | Immediate |
|---|---|---:|---:|
| DEV-0029 Finished | Finished | false | false |
| DEV-0029 Failed | Failed | false | false |
| DEV-0029 LimitReached | ImmediateWorkRemaining | true | true |
| ResumeLater + run limit reached | RunLimitReached | true | false |

Requirements:

- `Runs` contains at least one item.
- Preserve exact DEV-0029 result instances and order.
- Expose no mutable collection.
- `DelayCount >= 0`.
- `DelayCount <= Runs.Count - 1`.
- For a normally completed orchestration, `DelayCount == Runs.Count - 1`.
- Enforce state/flag invariants.
- Reject unsupported enum values.

For `RunLimitReached`, the final exact run must have state `ResumeLater`.

## Core Abstraction

### IRepeatedDelayedAutomaticResumeExecutor

Add a mockable asynchronous Core abstraction equivalent to:

```text
Task<RepeatedDelayedAutomaticResumeResult> ExecuteAsync(
    RepeatedDelayedAutomaticResumeRequest request,
    CancellationToken cancellationToken = default)
```

## Concrete Orchestration

### RepeatedDelayedAutomaticResumeExecutor

Implement in `TrailTrainer.Developer.Tasks`.

Inject exactly:

- `IAutomaticResumeRunOrchestrator`
- `IAsyncDelay`

Execution rules:

1. Execute DEV-0029 with the exact `RunRequest` and caller cancellation token.
2. Preserve the exact result.
3. Inspect only `AutomaticResumeRunResult.State`.
4. For `Finished`, return `Finished`.
5. For `Failed`, return `Failed`.
6. For `LimitReached`, return `ImmediateWorkRemaining`.
7. For `ResumeLater`:
   - if `Runs.Count == MaximumRuns`, return `RunLimitReached`;
   - otherwise execute exactly one delay using exact `ResumeDelay` and cancellation token;
   - then execute DEV-0029 again using the exact same `RunRequest` and token;
   - repeat the decision process.

## Hard Bounds

For every invocation:

```text
DEV-0029 invocation count <= MaximumRuns
delay invocation count <= MaximumRuns - 1
```

These are hard invariants.

Examples:

- `MaximumRuns = 1`: at most 1 run, 0 delays.
- `MaximumRuns = 2`: at most 2 runs, 1 delay.
- `MaximumRuns = 5`: at most 5 runs, 4 delays.

## Trust Boundary

DEV-0031 must not:

- inspect DEV-0029 `BatchRuns`,
- inspect DEV-0029 `Decisions`,
- reinterpret `ShouldRunAgain` or `Immediate`,
- call DEV-0028,
- call DEV-0027,
- access persistence/discovery.

`AutomaticResumeRunResult.State` is the only continuation authority.

## Ordering

Operations must remain strictly sequential:

```text
run 1
delay 1
run 2
delay 2
run 3
...
```

Do not:

- execute runs concurrently,
- start delay before the preceding run completes,
- start the next run before delay completes,
- prefetch work.

## Failure Behavior

If any DEV-0029 invocation throws:

- propagate the exact exception,
- stop immediately,
- perform no later delay/run,
- do not retry.

If any delay throws:

- propagate the exact exception,
- stop immediately,
- perform no later run,
- do not retry.

Do not convert exceptions into normal result states.

## Cancellation

Pass the exact caller `CancellationToken` to every DEV-0029 run and every delay.

Cancellation propagates unchanged and prevents all later operations.

Do not convert cancellation into a normal result.

## Tests

Use injected fakes/stubs only. No orchestration test may wait in real time.

Cover at least:

### Request

1. Null RunRequest rejected.
2. Zero ResumeDelay rejected.
3. Negative ResumeDelay rejected.
4. MaximumRuns zero rejected.
5. MaximumRuns negative rejected.
6. MaximumRuns one accepted.
7. Exact RunRequest identity preserved.
8. ResumeDelay preserved.
9. MaximumRuns preserved.

### Result

10. Empty Runs rejected.
11. Negative DelayCount rejected.
12. DelayCount greater than Runs.Count - 1 rejected.
13. Unsupported state rejected.
14. Finished invariants enforced.
15. Failed invariants enforced.
16. ImmediateWorkRemaining invariants enforced.
17. RunLimitReached invariants enforced.
18. Exact run identities preserved.
19. Run ordering preserved.
20. Runs collection cannot be mutated through result API.

### Terminal first run

21. First Finished -> Finished, one run, zero delays.
22. First Failed -> Failed, one run, zero delays.
23. First LimitReached -> ImmediateWorkRemaining, one run, zero delays.
24. No delay after terminal first result.

### Repeated delayed execution

25. ResumeLater then Finished executes two runs and one delay.
26. ResumeLater, ResumeLater, Finished executes three runs and two delays.
27. Each delay receives exact ResumeDelay.
28. Each delay receives exact cancellation token.
29. Each run receives exact same RunRequest instance.
30. Each run receives exact cancellation token.
31. Runs and delays are strictly sequential.
32. Exact returned run objects are preserved.
33. No delay follows a terminal result.

### Run limit

34. MaximumRuns 1 + ResumeLater -> RunLimitReached.
35. MaximumRuns 1 performs zero delays.
36. MaximumRuns 2 + two ResumeLater results -> RunLimitReached.
37. MaximumRuns N is never exceeded.
38. Delay count never exceeds MaximumRuns - 1.
39. RunLimitReached -> ShouldRunAgain true.
40. RunLimitReached -> Immediate false.
41. Final run for RunLimitReached is exact ResumeLater result.

### Later terminal states

42. Later Finished stops immediately.
43. Later Failed stops immediately.
44. Later LimitReached stops immediately.
45. Later LimitReached maps to ImmediateWorkRemaining.
46. ImmediateWorkRemaining -> ShouldRunAgain true.
47. ImmediateWorkRemaining -> Immediate true.

### Exceptions

48. First run exception propagates.
49. Later run exception propagates.
50. Run exception prevents later delay/run.
51. First delay exception propagates.
52. Later delay exception propagates.
53. Delay exception prevents later run.
54. No retry occurs.
55. Exceptions are not converted to normal results.

### Cancellation

56. Pre-cancelled execution propagates cancellation.
57. Cancellation during run propagates.
58. Cancellation during delay propagates.
59. Cancellation prevents later operations.
60. Exact token is passed everywhere.

### Architecture

61. Executor depends exactly on `IAutomaticResumeRunOrchestrator` and `IAsyncDelay`.
62. No direct DEV-0027 dependency.
63. No direct DEV-0028 dependency.
64. No persistence/discovery dependency.
65. No direct `Task.Delay` in executor.
66. No filesystem/JSON/Git/GitHub/process behavior.
67. No retry.
68. No concurrency.
69. No hosted service/background worker.
70. No CLI.
71. No unbounded loop.

### Regression

72. Existing DEV-0002 through DEV-0030 tests continue to pass.

Avoid unrelated refactoring.

## Out of Scope

Do not implement:

- hosted service,
- background worker,
- Windows service,
- cron,
- recurring external scheduler,
- automatic startup,
- persistence changes,
- retry,
- retry backoff,
- dynamic delay policy,
- exponential backoff,
- jitter,
- filesystem/JSON changes,
- Git operations,
- GitHub REST calls,
- CI lookup,
- notifications,
- CLI command,
- parallel runs,
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

- build succeeds with 0 errors and no new DEV-0031 warnings,
- all tests pass,
- no whitespace errors.

## Acceptance Criteria

DEV-0031 is complete when:

1. `RepeatedDelayedAutomaticResumeRequest` exists.
2. Request validates RunRequest, ResumeDelay, and MaximumRuns.
3. `RepeatedDelayedAutomaticResumeState` exists with exactly the required states.
4. `RepeatedDelayedAutomaticResumeResult` exists with enforced invariants.
5. `IRepeatedDelayedAutomaticResumeExecutor` exists as a mockable asynchronous Core abstraction.
6. `RepeatedDelayedAutomaticResumeExecutor` exists in Tasks.
7. Executor depends exactly on DEV-0029 orchestrator and DEV-0030 delay abstraction.
8. DEV-0029 state is the sole continuation authority.
9. ResumeLater performs a delay and another run only while capacity remains.
10. Finished stops immediately.
11. Failed stops immediately.
12. LimitReached stops immediately as ImmediateWorkRemaining.
13. MaximumRuns is never exceeded.
14. Delay count never exceeds MaximumRuns - 1.
15. RunLimitReached is returned when delayed work remains at the run bound.
16. Exact run request, result instances, delay, and cancellation token are preserved/delegated.
17. Execution is strictly sequential.
18. Exceptions and cancellation propagate unchanged.
19. No retries occur.
20. No direct DEV-0027/DEV-0028 or persistence/discovery access is introduced.
21. No filesystem, JSON, Git, GitHub, network, process, CLI, hosted-service, or background-worker behavior is introduced.
22. Tests use injected fakes and do not wait in real time.
23. Existing tests continue to pass.
24. `dotnet build` succeeds.
25. `dotnet test` succeeds.
26. `git diff --check` succeeds.
27. No out-of-scope functionality is implemented.
28. `docs/developer-reviews/REVIEW-0031.md` is created.

## Codex Completion Protocol

After implementation and verification:

1. Do **not** create a Git commit.
2. Do **not** push changes.
3. Do **not** modify this Developer Task.
4. Create `docs/developer-reviews/REVIEW-0031.md`.
5. Use:

```text
# REVIEW-0031 – Repeated Delayed Automatic Resume Loop

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

## Deviations from DEV-0031

## Open Issues / Known Limitations

## Commit and Push
No commit created.
No push performed.
```

6. Use `READY FOR REVIEW` only if all acceptance criteria and verification succeed; otherwise `BLOCKED`.
7. Record build warning/error counts, test passed/failed/skipped counts, and diff-check result.
8. List every created/modified/deleted file.
9. Write `None` when there are no deviations or open issues.

The review report is part of DEV-0031 and must be included in the later Pull Request.
