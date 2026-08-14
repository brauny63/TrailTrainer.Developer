# DEV-0020 – Persisted Lifecycle Resume Integration

## Metadata

- Task ID: `DEV-0020`
- Repository: `TrailTrainer.Developer`
- Expected branch: `feature/dev-0020-persisted-lifecycle-resume-integration`
- Review report: `docs/developer-reviews/REVIEW-0020.md`
- Depends on: `DEV-0017`, `DEV-0018`, `DEV-0019`

## Goal

Integrate lifecycle persistence with the existing complete and resume lifecycle orchestrators.

DEV-0017 can stop normally with `Pending`.
DEV-0018 can resume an existing Pull Request.
DEV-0019 can persist the resume context.

DEV-0020 connects these capabilities so that:

1. an initial lifecycle invocation persists resumable state when DEV-0017 returns `Pending`,
2. a later invocation can load that persisted state and resume through DEV-0018,
3. persisted state remains available while the resumed lifecycle is `Pending` or `Failed`,
4. persisted state is deleted only after the resumed lifecycle reaches `Completed`.

DEV-0020 is orchestration only.

It must not add polling, timers, background execution, Git/GitHub provider logic, or new persistence mechanics.

## Codex Execution Instructions

Work this Developer Task completely.

- Follow `docs/architecture/README.md`.
- Implement only the scope defined here.
- Reuse `IDeveloperLifecycleOrchestrator` from DEV-0017.
- Reuse `IDeveloperLifecycleResumer` from DEV-0018.
- Reuse `IDeveloperLifecycleStateStore` from DEV-0019.
- Reuse existing lifecycle models rather than duplicating their semantics.
- Keep provider-neutral contracts/models in `TrailTrainer.Developer.Core`.
- Put orchestration in `TrailTrainer.Developer.Tasks`.
- Do not add filesystem implementation details to Tasks.
- Do not add HTTP, Git process, shell, GitHub REST, polling, delay, retry, or scheduling logic.
- Do not modify this Developer Task or architecture documentation.
- Do not implement anything under **Out of Scope**.
- Do not create a Git commit for DEV-0020.
- Do not push the DEV-0020 implementation branch.
- After implementation and verification create `docs/developer-reviews/REVIEW-0020.md`.

If an ambiguity prevents correct completion, do not invent behavior. Document it and set the review status to `BLOCKED`.

## Scope

Implement two integration operations:

### Start

Run the complete DEV-0017 lifecycle once.

- `Pending` -> create and save a persisted resume state.
- `Failed` -> do not create persisted resume state.
- `Completed` -> do not create persisted resume state.

### Resume

Load an existing persisted resume state by TaskId and run DEV-0018 once.

- no persisted state -> return a clear not-found result,
- `Pending` -> retain persisted state,
- `Failed` -> retain persisted state,
- `Completed` -> delete persisted state.

No operation waits for a later CI transition.

## Important State Ownership Rule

DEV-0020 owns only the persistence lifecycle around DEV-0017/DEV-0018.

It does not own:

- Pull Request creation,
- CI evaluation,
- merge safety,
- Git cleanup,
- JSON serialization,
- filesystem atomicity.

Those remain owned by DEV-0017, DEV-0018, DEV-0019 and their dependencies.

## Persisted Integration State Construction

When the initial DEV-0017 result is `Pending`, construct a `DeveloperLifecyclePersistedState`.

Required values:

- `TaskId` comes from the explicit Start request.
- optional `TaskFilePath` comes from the Start request.
- `ResumeContext` is derived only from the successful DEV-0017 workflow result and the original lifecycle inputs.
- `SavedAtUtc` comes from an injected time abstraction or another deterministic/testable provider-neutral mechanism.

Do not call `DateTimeOffset.UtcNow` directly inside orchestration if the project already has or can minimally introduce a testable clock abstraction.

The persisted resume context must contain:

- original repository directory,
- original repository identity,
- Pull Request number from DEV-0017's workflow result,
- feature branch from DEV-0017's workflow/completion result,
- original base branch,
- original Git remote name.

Do not derive the Pull Request number or feature branch from filenames or naming conventions.

## Clock Abstraction

If no suitable clock abstraction already exists, add a minimal provider-neutral abstraction such as:

### IUtcClock

- exposes current UTC `DateTimeOffset`,
- production implementation may use `DateTimeOffset.UtcNow`,
- tests can supply a deterministic timestamp.

Keep it small and reusable.

Do not introduce a scheduling framework.

## Start Request

### PersistedDeveloperLifecycleStartRequest

Add an immutable provider-neutral request model that contains:

- `TaskId`
- optional `TaskFilePath`
- all inputs required by `IDeveloperLifecycleOrchestrator`

Do not duplicate data unnecessarily if composition with an existing request/input model is already available.

If DEV-0017 currently exposes parameters rather than a request object, use the smallest architecture-consistent design.

Validate:

- TaskId non-empty,
- optional TaskFilePath not whitespace-only,
- other lifecycle inputs consistently with the existing contracts where appropriate.

Do not reimplement all downstream validation.

## Start Result

### PersistedDeveloperLifecycleStartResult

Add an immutable provider-neutral result exposing at least:

- exact `DeveloperLifecycleResult Lifecycle`
- optional `DeveloperLifecyclePersistedState PersistedState`

Invariants:

### Pending

- lifecycle state is `Pending`,
- `PersistedState` must be present.

### Failed

- lifecycle state is `Failed`,
- `PersistedState` must be null.

### Completed

- lifecycle state is `Completed`,
- `PersistedState` must be null.

## Resume Request

### PersistedDeveloperLifecycleResumeRequest

Add an immutable provider-neutral request exposing:

- `TaskId`
- `PullRequestMergeMethod`
- optional merge commit title
- optional merge commit message
- `DeleteRemoteBranch`

Validate TaskId as non-empty.

## Resume Outcome State

### PersistedDeveloperLifecycleResumeState

Add a strongly typed state with exactly:

- `NotFound`
- `Pending`
- `Failed`
- `Completed`

`NotFound` means no persisted state exists for the requested TaskId.

## Resume Result

### PersistedDeveloperLifecycleResumeResult

Add an immutable provider-neutral result exposing at least:

- `State`
- `TaskId`
- optional exact `DeveloperLifecyclePersistedState PersistedState`
- optional exact `DeveloperLifecycleResumeResult Lifecycle`

Invariants:

### NotFound

- no persisted state,
- no lifecycle result.

### Pending

- persisted state present,
- lifecycle result present and Pending.

### Failed

- persisted state present,
- lifecycle result present and Failed.

### Completed

- lifecycle result present and Completed,
- the state has been successfully deleted,
- the result may retain the originally loaded persisted state for audit/context.

Choose and document one consistent model behavior for Completed. Prefer retaining the loaded state in the returned result because deletion concerns storage, not result observability.

## Core Abstraction

### IPersistedDeveloperLifecycle

Add a mockable asynchronous provider-neutral abstraction with operations equivalent to:

- `StartAsync(PersistedDeveloperLifecycleStartRequest request, CancellationToken cancellationToken = default)`
- `ResumeAsync(PersistedDeveloperLifecycleResumeRequest request, CancellationToken cancellationToken = default)`

Return the corresponding Start/Resume result models.

## Concrete Orchestration

### PersistedDeveloperLifecycle

Implement in `TrailTrainer.Developer.Tasks`.

Inject:

- `IDeveloperLifecycleOrchestrator`
- `IDeveloperLifecycleResumer`
- `IDeveloperLifecycleStateStore`
- clock abstraction if introduced.

Do not instantiate concrete Git, GitHub, persistence, or HTTP implementations internally.

## Start Flow

Required order:

```text
Validate Start request
        |
        v
IDeveloperLifecycleOrchestrator
        |
        +-- Failed ----> return; do not save
        |
        +-- Completed -> return; do not save
        |
        +-- Pending
              |
              v
derive ResumeContext
              |
              v
create persisted state
              |
              v
StateStore.SaveAsync
              |
              v
return Pending + persisted state
```

### Start – Pending

When DEV-0017 returns Pending:

1. derive the resume context,
2. create persisted state,
3. save it,
4. return it.

If Save fails:

- propagate the failure,
- do not report successful persisted Pending integration,
- do not retry.

DEV-0020 cannot roll back the already-created Pull Request.

### Start – Failed

Do not save state.

### Start – Completed

Do not save state.

Do not call Delete merely to clean up an unrelated pre-existing state in Start. DEV-0020 Start owns only the state it creates for this invocation.

## Resume Flow

Required order:

```text
Validate Resume request
        |
        v
StateStore.LoadAsync(TaskId)
        |
        +-- null ------> NotFound
        |
        v
IDeveloperLifecycleResumer
        |
        +-- Pending ---> retain state, return Pending
        |
        +-- Failed ----> retain state, return Failed
        |
        +-- Completed
              |
              v
StateStore.DeleteAsync(TaskId)
              |
              v
return Completed
```

## Resume – Not Found

When Load returns null:

- do not call DEV-0018,
- do not call Delete,
- return `NotFound`.

## Resume – Pending

When DEV-0018 returns Pending:

- do not Save again,
- do not Delete,
- preserve the existing persisted state unchanged,
- return Pending.

## Resume – Failed

When DEV-0018 returns Failed:

- do not Save again,
- do not Delete,
- preserve the existing persisted state unchanged,
- return Failed.

A Failed CI result remains resumable because CI may later be rerun/fixed outside DEV-0020.

## Resume – Completed

When DEV-0018 returns Completed:

1. call `DeleteAsync(TaskId)`,
2. only after successful deletion return Completed.

If Delete fails:

- propagate the failure,
- do not invoke DEV-0018 a second time,
- do not retry deletion,
- persisted state may remain,
- the Pull Request may already be merged and cleanup may already have occurred.

This is an exceptional partial-completion condition and must not be hidden.

## Resume Context Delegation

Pass the exact loaded `DeveloperLifecyclePersistedState.ResumeContext` to DEV-0018.

Do not reconstruct it from the current filesystem, repository state, task filename, or naming conventions.

## TaskId Consistency

After Load, the loaded state's `TaskId` must equal the requested TaskId using ordinal comparison.

The DEV-0019 store should already preserve this invariant, but DEV-0020 must not silently continue with an inconsistent state returned by a mocked or alternative store.

If inconsistent:

- fail clearly,
- do not call DEV-0018,
- do not delete state.

## Cancellation

Propagate the same `CancellationToken` to:

- DEV-0017,
- Save,
- Load,
- DEV-0018,
- Delete.

Cancellation prevents subsequent operations.

Do not convert cancellation into NotFound, Pending, Failed, or Completed.

## Failure / Short-Circuit Behavior

### Start

- DEV-0017 exception -> no Save.
- Failed -> no Save.
- Completed -> no Save.
- Pending + state construction failure -> no Save.
- Pending + Save failure -> propagate.
- No retries.

### Resume

- Load exception -> no DEV-0018, no Delete.
- Missing state -> NotFound.
- TaskId mismatch -> no DEV-0018, no Delete.
- DEV-0018 exception -> no Delete.
- Pending -> no Delete.
- Failed -> no Delete.
- Completed + Delete failure -> propagate.
- No retries.
- No rollback.

## Tests

Use injected fakes/stubs.

No test may require:

- GitHub,
- network,
- real Git repositories,
- filesystem persistence implementation,
- child processes.

The DEV-0020 orchestration tests should mock `IDeveloperLifecycleStateStore`.

Cover at least:

### Start request/result validation

1. Empty TaskId rejected.
2. Whitespace TaskId rejected.
3. Whitespace-only optional TaskFilePath rejected.
4. Null request rejected.
5. Pending Start result requires persisted state.
6. Failed Start result rejects persisted state.
7. Completed Start result rejects persisted state.

### Start delegation

8. DEV-0017 called exactly once.
9. Exact DEV-0017 inputs delegated.
10. Cancellation token delegated to DEV-0017.

### Start Pending persistence

11. Pending derives PR number from workflow result.
12. Pending derives feature branch from workflow/completion result.
13. Pending uses original repository directory.
14. Pending uses original repository identity.
15. Pending uses original base branch.
16. Pending uses original Git remote.
17. Pending uses exact TaskId.
18. Pending uses exact optional TaskFilePath.
19. Pending uses injected UTC timestamp.
20. Pending calls Save exactly once.
21. Save receives exact constructed persisted state.
22. Cancellation token delegated to Save.
23. Pending result preserves exact lifecycle result.
24. Pending result exposes exact saved state.

### Start non-persistence paths

25. Failed does not Save.
26. Failed returns exact lifecycle result.
27. Completed does not Save.
28. Completed returns exact lifecycle result.
29. DEV-0017 exception prevents Save.
30. Save exception propagates.
31. Save failure performs no retry.

### Resume request/result validation

32. Resume empty TaskId rejected.
33. Resume whitespace TaskId rejected.
34. Null Resume request rejected.
35. NotFound result has no persisted/lifecycle state.
36. Pending result requires persisted state and Pending lifecycle.
37. Failed result requires persisted state and Failed lifecycle.
38. Completed result requires Completed lifecycle.

### Resume load

39. Load called with exact TaskId.
40. Cancellation token delegated to Load.
41. Missing state returns NotFound.
42. Missing state does not call DEV-0018.
43. Missing state does not Delete.
44. Loaded TaskId mismatch fails.
45. TaskId mismatch does not call DEV-0018.
46. TaskId mismatch does not Delete.

### Resume delegation

47. DEV-0018 receives exact loaded ResumeContext.
48. Merge method delegated exactly.
49. Merge title delegated exactly.
50. Merge message delegated exactly.
51. DeleteRemoteBranch delegated exactly.
52. Cancellation token delegated to DEV-0018.

### Resume Pending

53. Pending retains persisted state.
54. Pending returns exact DEV-0018 result.
55. Pending does not Save.
56. Pending does not Delete.

### Resume Failed

57. Failed retains persisted state.
58. Failed returns exact DEV-0018 result.
59. Failed does not Save.
60. Failed does not Delete.

### Resume Completed

61. Completed calls Delete exactly once.
62. Delete receives exact TaskId.
63. Cancellation token delegated to Delete.
64. Delete occurs after DEV-0018 completion.
65. Completed returns exact DEV-0018 result.
66. Completed result retains loaded persisted state for context.
67. Completed does not Save.

### Failure behavior

68. Load exception prevents DEV-0018 and Delete.
69. DEV-0018 exception prevents Delete.
70. Delete exception propagates.
71. Delete exception does not call DEV-0018 twice.
72. Delete exception does not retry Delete.
73. No orchestration retry exists.

### Clock

74. Persisted SavedAtUtc comes from injected clock.
75. Non-UTC clock value is rejected/fails clearly if the clock contract can violate UTC.

### Ordering

76. Start saves only after Pending lifecycle result.
77. Resume loads before DEV-0018.
78. Resume deletes only after Completed DEV-0018 result.

### Regression

79. Existing DEV-0002 through DEV-0019 tests continue to pass.

Avoid unrelated refactoring.

## Out of Scope

Do not implement:

- CI polling,
- wait-until-complete loops,
- delays,
- timers,
- scheduler,
- background worker/service,
- automatic process restart,
- automatic invocation of Resume,
- retry policies,
- GitHub Actions reruns,
- lifecycle state enumeration,
- processing all pending states,
- state retention/expiration,
- database/cloud persistence,
- changes to DEV-0019 JSON format unless strictly necessary,
- Git implementation,
- GitHub REST implementation,
- new PR creation logic,
- new merge logic,
- new cleanup logic,
- automatic next Developer Task selection,
- automatic DEV file generation,
- Codex execution,
- CLI commands.

These belong to later Developer Tasks.

## Verification

Run:

```text
dotnet build
```

Required:

- 0 errors,
- no new warnings caused by DEV-0020.

Then:

```text
dotnet test
```

All tests must pass.

Also run:

```text
git diff --check
```

There must be no whitespace errors. Platform line-ending notices alone are acceptable.

## Acceptance Criteria

DEV-0020 is complete when:

1. A provider-neutral persisted lifecycle integration abstraction exists.
2. Start and Resume request/result models exist with enforced invariants.
3. Start reuses DEV-0017.
4. Resume reuses DEV-0018.
5. Persistence reuses DEV-0019.
6. Pending Start creates and saves a valid persisted resume state.
7. Failed Start does not save.
8. Completed Start does not save.
9. PR number for persistence comes from DEV-0017 workflow result.
10. Feature branch for persistence comes from DEV-0017 workflow/completion result.
11. Saved timestamp is testable/deterministic through an injected clock or equivalent.
12. Resume loads state by TaskId.
13. Missing state returns NotFound without invoking DEV-0018.
14. Loaded TaskId mismatch is rejected.
15. DEV-0018 receives the exact loaded ResumeContext.
16. Pending Resume retains state.
17. Failed Resume retains state.
18. Completed Resume deletes state.
19. State is deleted only after DEV-0018 returns Completed.
20. Delete failure propagates and does not re-run DEV-0018.
21. No Save is performed during Resume.
22. Cancellation is propagated through every invoked dependency.
23. No polling, retry, scheduling, Git, GitHub REST, HTTP, process, shell, or filesystem implementation logic is added to Tasks.
24. Tests use injected fakes and no external resources.
25. Existing tests continue to pass.
26. `dotnet build` succeeds.
27. `dotnet test` succeeds.
28. `git diff --check` succeeds.
29. No out-of-scope functionality is implemented.
30. `docs/developer-reviews/REVIEW-0020.md` is created.

## Codex Completion Protocol

After implementation and verification:

1. Do **not** create a Git commit.
2. Do **not** push changes.
3. Do **not** modify this Developer Task.
4. Create:

   `docs/developer-reviews/REVIEW-0020.md`

5. The review report must contain:

```text
# REVIEW-0020 – Persisted Lifecycle Resume Integration

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

## Deviations from DEV-0020

## Open Issues / Known Limitations

## Commit and Push
No commit created.
No push performed.
```

6. Use `READY FOR REVIEW` only when all acceptance criteria and verification succeed.
7. Otherwise use `BLOCKED` and document the reason.
8. Record build success/failure, warning/error counts, test passed/failed/skipped counts, and `git diff --check`.
9. List every created, modified, or deleted file.
10. Write `None` when there are no deviations or open issues.

The review report is part of DEV-0020 and must be included in the later Pull Request.
