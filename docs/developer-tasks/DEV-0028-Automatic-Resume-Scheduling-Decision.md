# DEV-0028 – Automatic Resume Scheduling Decision

## Metadata

- Task ID: `DEV-0028`
- Repository: `TrailTrainer.Developer`
- Expected branch: `feature/dev-0028-automatic-resume-scheduling-decision`
- Review report: `docs/developer-reviews/REVIEW-0028.md`
- Depends on: `DEV-0027`

## Goal

Add a provider-neutral scheduling decision component that interprets the result of one bounded automatic resume batch run from DEV-0027 and decides whether orchestration is finished or a later execution is required.

DEV-0027 can execute multiple automatic resume steps in one bounded invocation, but intentionally does not schedule another invocation.

DEV-0028 introduces the pure decision layer needed before a later scheduler/worker is added.

It must not wait, sleep, poll, retry, schedule, execute DEV-0027, access persistence, invoke Git/GitHub, or start background work.

## Codex Execution Instructions

Work this Developer Task completely.

- Follow `docs/architecture/README.md`.
- Implement only the scope defined here.
- Reuse `AutomaticResumeBatchRunResult` and `AutomaticResumeBatchRunState` from DEV-0027.
- Keep provider-neutral contracts/models in `TrailTrainer.Developer.Core`.
- Put decision logic in `TrailTrainer.Developer.Tasks`.
- Do not execute `IAutomaticResumeBatchRunner`.
- Do not access lifecycle persistence or discovery.
- Do not add filesystem, JSON, Git, GitHub REST, HTTP, process, shell, polling, retry, delay, scheduling, timers, clocks, background workers, or CLI behavior.
- Do not modify this Developer Task or architecture documentation.
- Do not implement anything under **Out of Scope**.
- Do not create a Git commit for DEV-0028.
- Do not push the DEV-0028 implementation branch.
- After implementation and verification create `docs/developer-reviews/REVIEW-0028.md`.

If ambiguity prevents correct completion, document it and set the review status to `BLOCKED`.

## Scope

Implement a deterministic decision based only on an `AutomaticResumeBatchRunResult`.

Conceptually:

```text
DEV-0027 result
      |
      v
+----------------+
| decision layer |
+----------------+
      |
      +-- Empty ------------------> Finished
      |
      +-- Completed --------------> Finished
      |
      +-- Pending ----------------> ResumeLater
      |
      +-- Failed -----------------> StopFailed
      |
      +-- LimitReached -----------> ContinueImmediately
```

No external side effect is allowed.

## Decision State

### AutomaticResumeSchedulingDecisionState

Add a strongly typed enum containing exactly:

- `Finished`
- `ContinueImmediately`
- `ResumeLater`
- `StopFailed`

Semantics:

### Finished

The current automatic resume workflow is complete for now.

Produced when DEV-0027 returns:

- `Empty`
- `Completed`

### ContinueImmediately

DEV-0027 reached its configured bound while additional persisted work remains.

Produced when DEV-0027 returns:

- `LimitReached`

This means another bounded run may begin immediately.

DEV-0028 itself must not begin that run.

### ResumeLater

The current candidate is still pending.

Produced when DEV-0027 returns:

- `Pending`

This means a later orchestration layer may schedule another attempt.

DEV-0028 must not define when that later attempt happens.

### StopFailed

DEV-0027 returned a normal failed lifecycle result.

Produced when DEV-0027 returns:

- `Failed`

DEV-0028 must not retry it automatically.

## Result

### AutomaticResumeSchedulingDecision

Add an immutable provider-neutral result exposing at least:

- `AutomaticResumeSchedulingDecisionState State`
- exact `AutomaticResumeBatchRunResult BatchRun`
- `bool ShouldRunAgain`
- `bool Immediate`

Required mapping:

| DEV-0027 state | Decision | ShouldRunAgain | Immediate |
|---|---|---:|---:|
| Empty | Finished | false | false |
| Completed | Finished | false | false |
| Pending | ResumeLater | true | false |
| Failed | StopFailed | false | false |
| LimitReached | ContinueImmediately | true | true |

Requirements:

- Preserve the exact DEV-0027 result object.
- Reject null `BatchRun`.
- Reject unsupported decision states.
- Enforce the state/flag invariants above.
- Do not expose mutable state.

## Core Abstraction

### IAutomaticResumeSchedulingDecision

Add a mockable provider-neutral abstraction equivalent to:

```text
AutomaticResumeSchedulingDecision Decide(
    AutomaticResumeBatchRunResult batchRun)
```

The operation is synchronous because it performs no I/O.

## Concrete Decision Service

### AutomaticResumeSchedulingDecisionService

Implement in `TrailTrainer.Developer.Tasks`.

The service:

- has no dependencies,
- accepts one DEV-0027 batch result,
- maps it deterministically,
- returns one `AutomaticResumeSchedulingDecision`.

It must not inject:

- `IAutomaticResumeBatchRunner`,
- DEV-0026 abstractions,
- lifecycle persistence,
- discovery,
- Git,
- GitHub,
- clock/time providers,
- delay providers.

## Required Mapping Rules

### Empty

Input:

```text
State == Empty
```

Return:

```text
State = Finished
ShouldRunAgain = false
Immediate = false
```

### Completed

Input:

```text
State == Completed
```

Return:

```text
State = Finished
ShouldRunAgain = false
Immediate = false
```

### Pending

Input:

```text
State == Pending
```

Return:

```text
State = ResumeLater
ShouldRunAgain = true
Immediate = false
```

### Failed

Input:

```text
State == Failed
```

Return:

```text
State = StopFailed
ShouldRunAgain = false
Immediate = false
```

### LimitReached

Input:

```text
State == LimitReached
```

Return:

```text
State = ContinueImmediately
ShouldRunAgain = true
Immediate = true
```

## Batch Result Trust Boundary

DEV-0028 may rely on the invariants already guaranteed by `AutomaticResumeBatchRunResult`.

It must not:

- re-evaluate the individual DEV-0026 steps,
- inspect persistence,
- inspect `MoreWork` independently to override the DEV-0027 state,
- derive candidate identities,
- execute another batch.

The DEV-0027 terminal state is the decision input.

## Failure Behavior

- Null input -> throw before any result is created.
- Unsupported DEV-0027 enum value -> reject.
- Do not convert programmer/configuration errors into `StopFailed`.
- `StopFailed` represents only a valid DEV-0027 `Failed` state.

## Cancellation

No cancellation token is required.

DEV-0028 performs only an in-memory synchronous mapping and must not introduce asynchronous behavior.

## Tests

Use in-memory objects/fakes only.

No test may require filesystem, JSON, Git, GitHub, network, HTTP, timers, clocks, delays, or child processes.

Cover at least:

### Decision result invariants

1. Null BatchRun rejected.
2. Unsupported decision state rejected.
3. Finished requires `ShouldRunAgain == false`.
4. Finished requires `Immediate == false`.
5. ContinueImmediately requires `ShouldRunAgain == true`.
6. ContinueImmediately requires `Immediate == true`.
7. ResumeLater requires `ShouldRunAgain == true`.
8. ResumeLater requires `Immediate == false`.
9. StopFailed requires `ShouldRunAgain == false`.
10. StopFailed requires `Immediate == false`.
11. Exact BatchRun object identity preserved.

### Mapping

12. Empty maps to Finished.
13. Empty maps to ShouldRunAgain false.
14. Empty maps to Immediate false.
15. Completed maps to Finished.
16. Completed maps to ShouldRunAgain false.
17. Completed maps to Immediate false.
18. Pending maps to ResumeLater.
19. Pending maps to ShouldRunAgain true.
20. Pending maps to Immediate false.
21. Failed maps to StopFailed.
22. Failed maps to ShouldRunAgain false.
23. Failed maps to Immediate false.
24. LimitReached maps to ContinueImmediately.
25. LimitReached maps to ShouldRunAgain true.
26. LimitReached maps to Immediate true.
27. Every mapping preserves exact BatchRun identity.
28. Unsupported DEV-0027 state rejected.

### Architecture

29. Decision service has no constructor dependencies.
30. No `IAutomaticResumeBatchRunner` dependency.
31. No DEV-0026 dependency.
32. No persistence/discovery dependency.
33. No filesystem/JSON/Git/GitHub/process behavior.
34. No clock/timer/delay behavior.
35. No scheduling is actually performed.
36. No retry or polling behavior.
37. No asynchronous API is introduced.

### Regression

38. Existing DEV-0002 through DEV-0027 tests continue to pass.

Avoid unrelated refactoring.

## Out of Scope

Do not implement:

- execution of DEV-0027,
- another batch run,
- retry,
- retry counters,
- retry policy,
- retry backoff,
- polling,
- polling intervals,
- delays,
- sleep,
- timers,
- clocks,
- scheduler,
- periodic execution,
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

- build succeeds with 0 errors and no new DEV-0028 warnings,
- all tests pass,
- no whitespace errors.

## Acceptance Criteria

DEV-0028 is complete when:

1. `AutomaticResumeSchedulingDecisionState` exists with exactly Finished, ContinueImmediately, ResumeLater, StopFailed.
2. `AutomaticResumeSchedulingDecision` exists and is immutable.
3. Decision result preserves exact DEV-0027 result identity.
4. Decision result enforces all state/flag invariants.
5. `IAutomaticResumeSchedulingDecision` exists as a mockable synchronous Core abstraction.
6. `AutomaticResumeSchedulingDecisionService` exists in Tasks.
7. The service has no dependencies.
8. Empty maps to Finished.
9. Completed maps to Finished.
10. Pending maps to ResumeLater.
11. Failed maps to StopFailed.
12. LimitReached maps to ContinueImmediately.
13. `ShouldRunAgain` matches the required mapping.
14. `Immediate` matches the required mapping.
15. Null and unsupported states are rejected.
16. No individual DEV-0026 steps are re-evaluated.
17. DEV-0027 is not executed.
18. No scheduling, timer, clock, delay, retry, polling, persistence, filesystem, JSON, Git, GitHub, network, process, CLI, or background-worker behavior is introduced.
19. Tests use in-memory objects/fakes only.
20. Existing tests continue to pass.
21. `dotnet build` succeeds.
22. `dotnet test` succeeds.
23. `git diff --check` succeeds.
24. No out-of-scope functionality is implemented.
25. `docs/developer-reviews/REVIEW-0028.md` is created.

## Codex Completion Protocol

After implementation and verification:

1. Do **not** create a Git commit.
2. Do **not** push changes.
3. Do **not** modify this Developer Task.
4. Create `docs/developer-reviews/REVIEW-0028.md`.
5. Use:

```text
# REVIEW-0028 – Automatic Resume Scheduling Decision

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

## Deviations from DEV-0028

## Open Issues / Known Limitations

## Commit and Push
No commit created.
No push performed.
```

6. Use `READY FOR REVIEW` only if all acceptance criteria and verification succeed; otherwise `BLOCKED`.
7. Record build warning/error counts, test passed/failed/skipped counts, and diff-check result.
8. List every created/modified/deleted file.
9. Write `None` when there are no deviations or open issues.

The review report is part of DEV-0028 and must be included in the later Pull Request.
