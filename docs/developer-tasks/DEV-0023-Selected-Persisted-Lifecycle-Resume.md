# DEV-0023 – Selected Persisted Lifecycle Resume

## Metadata

- Task ID: `DEV-0023`
- Repository: `TrailTrainer.Developer`
- Expected branch: `feature/dev-0023-selected-persisted-lifecycle-resume`
- Review report: `docs/developer-reviews/REVIEW-0023.md`
- Depends on: `DEV-0020`, `DEV-0021`, `DEV-0022`

## Goal

Connect persisted lifecycle selection from DEV-0022 with persisted lifecycle resume execution from DEV-0020.

A caller can request one persisted lifecycle by:

- exact TaskId,
- oldest persisted state,
- newest persisted state.

DEV-0023 selects the target once and, when found, invokes DEV-0020 Resume exactly once for the selected TaskId.

This task is a thin orchestration layer.

It must not add polling, waiting, retries, scheduling, background execution, filesystem/JSON logic, Git logic, or GitHub REST logic.

## Codex Execution Instructions

Work this Developer Task completely.

- Follow `docs/architecture/README.md`.
- Implement only the scope defined here.
- Reuse `IPersistedLifecycleSelector` from DEV-0022.
- Reuse `IPersistedDeveloperLifecycle` from DEV-0020.
- Reuse existing selection, persistence, resume, merge-method, and lifecycle models.
- Keep provider-neutral contracts/models in `TrailTrainer.Developer.Core`.
- Put orchestration in `TrailTrainer.Developer.Tasks`.
- Do not instantiate concrete selector, persistence, Git, GitHub, HTTP, filesystem, or JSON implementations.
- Do not add process/shell execution.
- Do not modify this Developer Task or architecture documentation.
- Do not implement anything under **Out of Scope**.
- Do not create a Git commit for DEV-0023.
- Do not push the DEV-0023 implementation branch.
- After implementation and verification create `docs/developer-reviews/REVIEW-0023.md`.

If ambiguity prevents correct completion, do not invent behavior. Document it and set the review status to `BLOCKED`.

## Scope

Implement one operation:

1. accept selection criteria plus DEV-0020 resume options,
2. select one persisted state through DEV-0022,
3. return NotFound without resume when no state is selected,
4. when found, construct the DEV-0020 resume request from the selected TaskId and caller-provided resume options,
5. invoke DEV-0020 Resume exactly once,
6. return the exact selection and resume results.

DEV-0023 must not independently load, save, delete, or reconstruct persisted lifecycle state.

## Request

### SelectedPersistedLifecycleResumeRequest

Add an immutable provider-neutral request exposing:

- `PersistedLifecycleSelectionRequest Selection`
- `PullRequestMergeMethod MergeMethod`
- optional `MergeCommitTitle`
- optional `MergeCommitMessage`
- `DeleteRemoteBranch`

Validation:

- Selection must not be null.
- Reject unsupported merge-method enum values if existing DEV-0020 request validation does so.
- Preserve optional merge title/message exactly.
- Do not duplicate TaskId outside `Selection`.

DEV-0023 must not accept a separate TaskId because that could disagree with the selected target.

## Result State

### SelectedPersistedLifecycleResumeState

Add a strongly typed enum with exactly:

- `NotFound`
- `Pending`
- `Failed`
- `Completed`

## Result

### SelectedPersistedLifecycleResumeResult

Add an immutable provider-neutral result exposing at least:

- `State`
- exact `PersistedLifecycleSelectionResult Selection`
- optional exact `PersistedDeveloperLifecycleResumeResult Resume`

Invariants:

### NotFound

- Selection must be `NotFound`.
- Resume must be null.

### Pending

- Selection must be `Found`.
- Resume must be present.
- Resume state must be `Pending`.

### Failed

- Selection must be `Found`.
- Resume must be present.
- Resume state must be `Failed`.

### Completed

- Selection must be `Found`.
- Resume must be present.
- Resume state must be `Completed`.

Reject unsupported result-state enum values.

Do not copy nested lifecycle data into additional fields.

## Core Abstraction

### ISelectedPersistedLifecycleResumer

Add a mockable asynchronous provider-neutral abstraction equivalent to:

`ResumeAsync(SelectedPersistedLifecycleResumeRequest request, CancellationToken cancellationToken = default)`

Return `SelectedPersistedLifecycleResumeResult`.

## Concrete Orchestration

### SelectedPersistedLifecycleResumer

Implement in `TrailTrainer.Developer.Tasks`.

Inject exactly the capabilities required:

- `IPersistedLifecycleSelector`
- `IPersistedDeveloperLifecycle`

Do not inject the state store or discovery directly.

## Required Flow

```text
Validate request
      |
      v
IPersistedLifecycleSelector.SelectAsync
      |
      +-- NotFound --------------------+
      |                                |
      |                                v
      |                         return NotFound
      |
      +-- Found
            |
            v
selected PersistedState.TaskId
            |
            v
construct PersistedDeveloperLifecycleResumeRequest
            |
            v
IPersistedDeveloperLifecycle.ResumeAsync
            |
            +-- NotFound -> inconsistency failure
            +-- Pending  -> return Pending
            +-- Failed   -> return Failed
            +-- Completed-> return Completed
```

## Selection Delegation

Pass the exact `SelectedPersistedLifecycleResumeRequest.Selection` object to DEV-0022.

Do not reconstruct the selection request.

Pass the exact caller cancellation token.

Call the selector exactly once.

## Selection NotFound

When selection returns NotFound:

- do not call DEV-0020 Resume,
- return `SelectedPersistedLifecycleResumeState.NotFound`,
- preserve the exact selection result object.

## Selection Found

When selection returns Found:

- use the selected `PersistedState.TaskId`,
- do not use a filename, branch, PR number, or naming convention,
- construct `PersistedDeveloperLifecycleResumeRequest` with:
  - selected TaskId,
  - exact caller merge method,
  - exact optional merge title,
  - exact optional merge message,
  - exact caller DeleteRemoteBranch value.

Then call DEV-0020 Resume exactly once.

## DEV-0020 NotFound After Found Selection

If DEV-0022 returns Found but DEV-0020 returns NotFound, the persisted state disappeared or changed between selection and resume.

This is an inconsistent/race condition.

Required behavior:

- fail clearly with an exception,
- do not convert it into ordinary NotFound,
- do not retry selection,
- do not retry resume.

This distinction is important:

- Selection NotFound = normal no-target outcome.
- Resume NotFound after Found selection = state changed between operations.

## Resume Pending

Return:

- `SelectedPersistedLifecycleResumeState.Pending`,
- exact selection result,
- exact DEV-0020 resume result.

DEV-0020 owns state retention.

## Resume Failed

Return:

- `SelectedPersistedLifecycleResumeState.Failed`,
- exact selection result,
- exact DEV-0020 resume result.

DEV-0020 owns state retention.

## Resume Completed

Return:

- `SelectedPersistedLifecycleResumeState.Completed`,
- exact selection result,
- exact DEV-0020 resume result.

DEV-0020 owns state deletion.

DEV-0023 must not delete anything itself.

## Failure / Short-Circuit Behavior

- Null request -> selector not called.
- Selector exception -> resume not called.
- Selection NotFound -> resume not called.
- Invalid/inconsistent selection result -> fail clearly.
- DEV-0020 exception -> propagate.
- DEV-0020 NotFound after Found selection -> fail clearly.
- No retry.
- No rollback.
- No second selection.
- No second resume.

## Cancellation

Propagate the exact caller `CancellationToken` to:

- selector,
- DEV-0020 Resume.

If selection is cancelled, Resume must not run.

If Resume is cancelled, cancellation must propagate.

Do not convert cancellation into NotFound, Pending, Failed, or Completed.

## Tests

Use injected fakes/stubs only.

No test may require:

- filesystem,
- JSON,
- Git,
- GitHub,
- network,
- child processes.

Cover at least:

### Request validation

1. Null Selection rejected.
2. Unsupported merge method rejected when consistent with existing DEV-0020 validation.
3. Optional merge title preserved exactly.
4. Optional merge message preserved exactly.
5. DeleteRemoteBranch preserved exactly.

### Result invariants

6. Unsupported result state rejected.
7. NotFound requires Selection.NotFound.
8. NotFound rejects Resume result.
9. Pending requires Selection.Found.
10. Pending requires Resume.
11. Pending requires Resume.Pending.
12. Failed requires Selection.Found.
13. Failed requires Resume.
14. Failed requires Resume.Failed.
15. Completed requires Selection.Found.
16. Completed requires Resume.
17. Completed requires Resume.Completed.
18. Valid results preserve exact Selection object identity.
19. Valid non-NotFound results preserve exact Resume object identity.

### Selection delegation

20. Selector called exactly once.
21. Selector receives exact Selection request object.
22. Selector receives exact cancellation token.
23. Selector exception propagates.
24. Selector exception prevents Resume.
25. Selection NotFound returns NotFound.
26. Selection NotFound preserves exact selection result.
27. Selection NotFound does not call Resume.

### Resume request construction

28. Selected persisted state's exact TaskId is used.
29. Merge method delegated exactly.
30. Merge title delegated exactly.
31. Merge message delegated exactly.
32. DeleteRemoteBranch delegated exactly.
33. Resume receives exact cancellation token.
34. Resume called exactly once after Found selection.
35. Resume is called after selection.

### Resume outcomes

36. Resume Pending maps to Pending.
37. Resume Pending preserves exact selection result.
38. Resume Pending preserves exact resume result.
39. Resume Failed maps to Failed.
40. Resume Failed preserves exact selection result.
41. Resume Failed preserves exact resume result.
42. Resume Completed maps to Completed.
43. Resume Completed preserves exact selection result.
44. Resume Completed preserves exact resume result.

### Race / inconsistency

45. Resume NotFound after Found selection throws clearly.
46. Resume NotFound does not retry selector.
47. Resume NotFound does not retry Resume.

### Failure / cancellation

48. Resume exception propagates.
49. Resume exception does not retry.
50. Pre-cancelled selector cancellation prevents Resume.
51. Resume cancellation propagates.
52. No retry exists anywhere in orchestration.

### Architecture boundary

53. Orchestrator depends only on selector and persisted lifecycle abstraction.
54. Orchestrator does not directly use discovery.
55. Orchestrator does not directly use state store.
56. Orchestrator performs no filesystem/JSON/Git/GitHub/process work.

### Regression

57. Existing DEV-0002 through DEV-0022 tests continue to pass.

Avoid unrelated refactoring.

## Out of Scope

Do not implement:

- CI polling,
- repeated Resume,
- wait loops,
- delays,
- timers,
- scheduler,
- background worker/service,
- automatic retry,
- state-store access,
- discovery access outside DEV-0022 selector,
- filesystem or JSON changes,
- Git operations,
- GitHub REST calls,
- Pull Request creation,
- new merge implementation,
- cleanup implementation,
- CLI command,
- UI,
- interactive selection,
- batch resume,
- resume all pending states,
- automatic next Developer Task selection,
- Codex execution.

These belong to later Developer Tasks.

## Verification

Run:

```text
dotnet build
```

Required:

- 0 errors,
- no new warnings caused by DEV-0023.

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

DEV-0023 is complete when:

1. `SelectedPersistedLifecycleResumeRequest` exists with enforced invariants.
2. `SelectedPersistedLifecycleResumeState` exists with exactly NotFound, Pending, Failed, Completed.
3. `SelectedPersistedLifecycleResumeResult` exists with enforced selection/resume-state invariants.
4. `ISelectedPersistedLifecycleResumer` exists as a mockable asynchronous Core abstraction.
5. `SelectedPersistedLifecycleResumer` exists in Tasks.
6. It depends only on DEV-0022 selector and DEV-0020 persisted lifecycle abstractions.
7. Selector is called exactly once.
8. Exact selection request object is delegated.
9. Selection NotFound returns NotFound without Resume.
10. Found selection uses the selected persisted state's exact TaskId.
11. DEV-0020 resume options are delegated exactly.
12. Resume is called exactly once after Found selection.
13. Resume Pending maps to Pending.
14. Resume Failed maps to Failed.
15. Resume Completed maps to Completed.
16. Resume NotFound after Found selection fails clearly as an inconsistency.
17. Selection and Resume result object identities are preserved.
18. Cancellation is propagated exactly.
19. No retry, polling, delay, scheduling, persistence, discovery, Git, GitHub, filesystem, JSON, or process logic is added.
20. Tests use injected fakes only.
21. Existing tests continue to pass.
22. `dotnet build` succeeds.
23. `dotnet test` succeeds.
24. `git diff --check` succeeds.
25. No out-of-scope functionality is implemented.
26. `docs/developer-reviews/REVIEW-0023.md` is created.

## Codex Completion Protocol

After implementation and verification:

1. Do **not** create a Git commit.
2. Do **not** push changes.
3. Do **not** modify this Developer Task.
4. Create:

   `docs/developer-reviews/REVIEW-0023.md`

5. The review report must contain:

```text
# REVIEW-0023 – Selected Persisted Lifecycle Resume

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

## Deviations from DEV-0023

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

The review report is part of DEV-0023 and must be included in the later Pull Request.
