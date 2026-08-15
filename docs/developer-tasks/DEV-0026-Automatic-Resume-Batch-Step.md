# DEV-0026 – Automatic Resume Batch Step

## Metadata

- Task ID: `DEV-0026`
- Repository: `TrailTrainer.Developer`
- Expected branch: `feature/dev-0026-automatic-resume-batch-step`
- Review report: `docs/developer-reviews/REVIEW-0026.md`
- Depends on: `DEV-0021`, `DEV-0025`

## Goal

Add a provider-neutral single-step batch orchestration capability for automatic persisted lifecycle resume processing.

DEV-0025 automatically selects and resumes at most one persisted lifecycle state.
DEV-0021 can discover all currently persisted lifecycle states.

DEV-0026 combines those capabilities into one batch step that:

1. executes DEV-0025 exactly once,
2. inspects persisted lifecycle discovery exactly once after the DEV-0025 result when appropriate,
3. reports whether more persisted lifecycle states remain,
4. never loops or processes a second lifecycle in the same invocation.

This prepares the architecture for a later scheduler/worker without introducing scheduling or polling now.

## Codex Execution Instructions

Work this Developer Task completely.

- Follow `docs/architecture/README.md`.
- Implement only the scope defined here.
- Reuse `IAutomaticPersistedLifecycleResumer` from DEV-0025.
- Reuse `IDeveloperLifecycleStateDiscovery` from DEV-0021.
- Reuse existing automatic-resume and persisted-state models.
- Keep provider-neutral contracts/models in `TrailTrainer.Developer.Core`.
- Put orchestration in `TrailTrainer.Developer.Tasks`.
- Do not add filesystem, JSON, Git, GitHub REST, HTTP, process, shell, polling, retry, delay, scheduling, timers, or background workers.
- Do not modify this Developer Task or architecture documentation.
- Do not implement anything under **Out of Scope**.
- Do not create a Git commit for DEV-0026.
- Do not push the DEV-0026 implementation branch.
- After implementation and verification create `docs/developer-reviews/REVIEW-0026.md`.

If ambiguity prevents correct completion, document it and set the review status to `BLOCKED`.

## Scope

Implement one automatic resume batch step.

One invocation:

```text
Automatic Resume (DEV-0025)
        |
        +-- NotFound
        |      |
        |      v
        |   return Empty
        |
        +-- Pending / Failed
        |      |
        |      v
        | discover persisted states
        |      |
        |      v
        | report MoreWork
        |
        +-- Completed
               |
               v
        discover persisted states
               |
               v
        report MoreWork
```

The service must not automatically call DEV-0025 a second time.

## Request

### AutomaticResumeBatchStepRequest

Add an immutable provider-neutral request exposing the DEV-0025 resume options:

- `PullRequestMergeMethod MergeMethod`
- optional `MergeCommitTitle`
- optional `MergeCommitMessage`
- `DeleteRemoteBranch`

Preserve values exactly.

Do not accept:

- TaskId,
- selection criteria,
- iteration count,
- maximum batch size,
- polling interval,
- delay.

## Result State

### AutomaticResumeBatchStepState

Add a strongly typed enum with exactly:

- `Empty`
- `Pending`
- `Failed`
- `Completed`

Semantics:

### Empty

DEV-0025 returned NotFound. There was no candidate to process.

### Pending

DEV-0025 returned Pending.

### Failed

DEV-0025 returned Failed.

### Completed

DEV-0025 returned Completed.

## Result

### AutomaticResumeBatchStepResult

Add an immutable provider-neutral result exposing at least:

- `State`
- exact `AutomaticPersistedLifecycleResumeResult Resume`
- `bool MoreWork`

Invariants:

### Empty

- Resume state must be NotFound.
- MoreWork must be false.

### Pending

- Resume state must be Pending.

### Failed

- Resume state must be Failed.

### Completed

- Resume state must be Completed.

Reject unsupported enum states.

The result must preserve the exact DEV-0025 resume result object.

## MoreWork Semantics

`MoreWork` means:

> After this batch step finished, at least one persisted lifecycle state is currently discoverable.

It does **not** mean that the remaining state is ready to merge or that CI is successful.

This is only a persistence-queue signal.

## Core Abstraction

### IAutomaticResumeBatchStep

Add a mockable asynchronous provider-neutral abstraction equivalent to:

`ExecuteAsync(AutomaticResumeBatchStepRequest request, CancellationToken cancellationToken = default)`

Return `AutomaticResumeBatchStepResult`.

## Concrete Orchestration

### AutomaticResumeBatchStep

Implement in `TrailTrainer.Developer.Tasks`.

Inject exactly:

- `IAutomaticPersistedLifecycleResumer`
- `IDeveloperLifecycleStateDiscovery`

Do not inject concrete persistence, Git, GitHub, or other lifecycle implementations.

## Required Ordering

### First

Call DEV-0025 exactly once.

Construct `AutomaticPersistedLifecycleResumeRequest` using exact caller options.

Pass the exact cancellation token.

### DEV-0025 NotFound

If DEV-0025 returns NotFound:

- do not call discovery,
- return `Empty`,
- `MoreWork == false`.

Reason: DEV-0025's candidate selector already established that no persisted candidate exists for this invocation.

### DEV-0025 Pending

If DEV-0025 returns Pending:

- call discovery exactly once after DEV-0025,
- `MoreWork = discoveredStates.Count > 0`,
- return Pending.

The state that DEV-0025 attempted is expected to remain persisted, so MoreWork will normally be true, but DEV-0026 must use actual discovery output rather than assuming.

### DEV-0025 Failed

If DEV-0025 returns Failed:

- call discovery exactly once after DEV-0025,
- `MoreWork = discoveredStates.Count > 0`,
- return Failed.

### DEV-0025 Completed

If DEV-0025 returns Completed:

- call discovery exactly once after DEV-0025,
- `MoreWork = discoveredStates.Count > 0`,
- return Completed.

DEV-0020/DEV-0025 owns deletion of the completed persisted state.

## Discovery Rules

DEV-0026 must not inspect filesystem or persistence directly.

Use only `IDeveloperLifecycleStateDiscovery.ListAsync`.

Do not filter the discovered states.

Do not attempt to select another state.

Do not invoke DEV-0024 directly.

## Failure / Short-Circuit Behavior

- Null request -> DEV-0025 not called.
- DEV-0025 exception -> discovery not called.
- DEV-0025 NotFound -> discovery not called.
- Discovery exception after Pending/Failed/Completed -> propagate.
- Do not modify the already returned DEV-0025 lifecycle outcome.
- Do not retry DEV-0025.
- Do not retry discovery.
- Do not rollback.
- Do not execute another batch step automatically.

## Cancellation

Propagate the exact caller `CancellationToken` to:

- DEV-0025,
- discovery.

Cancellation must prevent later operations.

Do not convert cancellation into Empty, Pending, Failed, Completed, or MoreWork values.

## Tests

Use injected fakes/stubs only.

No test may require filesystem, JSON, Git, GitHub, network, or child processes.

Cover at least:

### Request

1. MergeMethod preserved exactly.
2. Null optional title/message preserved.
3. Non-null optional title/message preserved exactly.
4. DeleteRemoteBranch preserved exactly.
5. Merge enum behavior consistent with DEV-0025.

### Result invariants

6. Unsupported result state rejected.
7. Empty requires Resume.NotFound.
8. Empty requires MoreWork false.
9. Pending requires Resume.Pending.
10. Failed requires Resume.Failed.
11. Completed requires Resume.Completed.
12. Valid result preserves exact Resume object identity.

### DEV-0025 delegation

13. DEV-0025 called exactly once.
14. DEV-0025 receives exact merge method.
15. DEV-0025 receives exact title.
16. DEV-0025 receives exact message.
17. DEV-0025 receives exact DeleteRemoteBranch.
18. DEV-0025 receives exact cancellation token.
19. DEV-0025 exception propagates.
20. DEV-0025 exception prevents discovery.

### Empty

21. Resume NotFound maps to Empty.
22. Empty has MoreWork false.
23. Empty preserves exact resume result.
24. Empty does not call discovery.

### Pending

25. Resume Pending maps to Pending.
26. Discovery called exactly once after Pending.
27. Empty discovery -> MoreWork false.
28. Non-empty discovery -> MoreWork true.
29. Pending preserves exact resume result.

### Failed

30. Resume Failed maps to Failed.
31. Discovery called exactly once after Failed.
32. Empty discovery -> MoreWork false.
33. Non-empty discovery -> MoreWork true.
34. Failed preserves exact resume result.

### Completed

35. Resume Completed maps to Completed.
36. Discovery called exactly once after Completed.
37. Empty discovery -> MoreWork false.
38. Non-empty discovery -> MoreWork true.
39. Completed preserves exact resume result.

### Ordering / errors

40. DEV-0025 occurs before discovery.
41. Discovery receives exact cancellation token.
42. Discovery exception propagates.
43. Discovery exception does not trigger DEV-0025 again.
44. Discovery is never called more than once.
45. DEV-0025 is never called more than once.
46. No second candidate/lifecycle is processed.

### Cancellation

47. Pre-cancelled DEV-0025 cancellation prevents discovery.
48. Discovery cancellation propagates.
49. Cancellation does not become a normal result.

### Architecture

50. Service depends only on DEV-0025 abstraction and DEV-0021 discovery.
51. No direct state-store dependency.
52. No direct DEV-0020/DEV-0024 dependency.
53. No filesystem/JSON/Git/GitHub/process behavior.

### Regression

54. Existing DEV-0002 through DEV-0025 tests continue to pass.

Avoid unrelated refactoring.

## Out of Scope

Do not implement:

- loops,
- process-all behavior,
- maximum batch size,
- repeated DEV-0025 calls,
- automatic continuation when MoreWork is true,
- polling,
- wait loops,
- delays,
- timers,
- scheduler,
- Windows service,
- background worker,
- cron behavior,
- retries,
- state-store mutation,
- persistence changes,
- filesystem/JSON changes,
- Git operations,
- GitHub REST calls,
- CI lookup,
- CLI command,
- UI,
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

- build succeeds with 0 errors and no new DEV-0026 warnings,
- all tests pass,
- no whitespace errors.

## Acceptance Criteria

DEV-0026 is complete when:

1. `AutomaticResumeBatchStepRequest` exists.
2. `AutomaticResumeBatchStepState` contains exactly Empty, Pending, Failed, Completed.
3. `AutomaticResumeBatchStepResult` exists with enforced invariants.
4. `IAutomaticResumeBatchStep` exists as a mockable asynchronous Core abstraction.
5. `AutomaticResumeBatchStep` exists in Tasks.
6. It depends only on DEV-0025 and DEV-0021 abstractions.
7. DEV-0025 is invoked exactly once.
8. NotFound maps to Empty without discovery.
9. Pending maps to Pending and performs one discovery.
10. Failed maps to Failed and performs one discovery.
11. Completed maps to Completed and performs one discovery.
12. MoreWork reflects whether post-step discovery contains at least one state.
13. Exact DEV-0025 result identity is preserved.
14. Cancellation is propagated.
15. No retry, loop, scheduling, persistence mutation, filesystem, JSON, Git, GitHub, or process logic is introduced.
16. Tests use injected fakes only.
17. Existing tests continue to pass.
18. `dotnet build` succeeds.
19. `dotnet test` succeeds.
20. `git diff --check` succeeds.
21. No out-of-scope functionality is implemented.
22. `docs/developer-reviews/REVIEW-0026.md` is created.

## Codex Completion Protocol

After implementation and verification:

1. Do **not** create a Git commit.
2. Do **not** push changes.
3. Do **not** modify this Developer Task.
4. Create `docs/developer-reviews/REVIEW-0026.md`.
5. Use:

```text
# REVIEW-0026 – Automatic Resume Batch Step

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

## Deviations from DEV-0026

## Open Issues / Known Limitations

## Commit and Push
No commit created.
No push performed.
```

6. Use `READY FOR REVIEW` only if all acceptance criteria and verification succeed; otherwise `BLOCKED`.
7. Record build warning/error counts, test passed/failed/skipped counts, and diff-check result.
8. List every created/modified/deleted file.
9. Write `None` when there are no deviations or open issues.

The review report is part of DEV-0026 and must be included in the later Pull Request.
