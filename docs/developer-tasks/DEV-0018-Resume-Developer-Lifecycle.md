# DEV-0018 – Resume Developer Lifecycle

## Metadata

- Task ID: `DEV-0018`
- Repository: `TrailTrainer.Developer`
- Expected branch: `feature/dev-0018-resume-developer-lifecycle`
- Review report: `docs/developer-reviews/REVIEW-0018.md`
- Depends on: `DEV-0014`, `DEV-0015`, `DEV-0016`, `DEV-0017`

## Goal

Add a resume capability for a Developer Task lifecycle that has already reached the Pull Request stage.

DEV-0017 intentionally returns `Pending` when CI/status checks are not finished. Re-running the complete lifecycle must not repeat task completion, staging, committing, pushing, or Pull Request creation.

DEV-0018 therefore resumes an existing lifecycle from an explicitly supplied existing Pull Request context and performs only:

1. current CI/status evaluation,
2. guarded merge when successful,
3. post-merge cleanup after a confirmed merge.

This task does not poll or wait. One invocation performs one fresh status evaluation and either returns `Pending`, `Failed`, or completes merge and cleanup.

## Codex Execution Instructions

Work this Developer Task completely.

- Follow `docs/architecture/README.md`.
- Implement only the scope defined here.
- Reuse `IPullRequestStatusGate` from DEV-0014.
- Reuse `IPullRequestMergeGate` from DEV-0015.
- Reuse `IPostMergeCleaner` from DEV-0016.
- Reuse lifecycle state semantics from DEV-0017 where appropriate.
- Keep provider-neutral contracts/models in `TrailTrainer.Developer.Core`.
- Put orchestration in `TrailTrainer.Developer.Tasks`.
- Do not introduce HTTP, Git process, shell, or GitHub REST logic in Tasks.
- Do not perform Developer Task completion, stage, commit, push, or PR creation.
- Do not poll, delay, sleep, schedule, or retry.
- Do not modify this Developer Task or architecture documentation.
- Do not implement anything under **Out of Scope**.
- Do not create a Git commit for DEV-0018.
- Do not push the DEV-0018 implementation branch.
- After implementation and verification create `docs/developer-reviews/REVIEW-0018.md`.

If ambiguity prevents correct completion, document it and set the review status to `BLOCKED`.

## Scope

Implement a resume workflow for an already existing Pull Request.

Required sequence:

```text
Existing Pull Request
        |
        v
IPullRequestStatusGate
        |
        +-- Pending --> return Pending
        |
        +-- Failed ---> return Failed
        |
        +-- Successful
                |
                v
        IPullRequestMergeGate
                |
                v
        confirmed merge
                |
                v
        IPostMergeCleaner
                |
                v
        Completed
```

No Developer Task workflow, commit, push, or Pull Request creation may occur.

## Resume Context Model

### DeveloperLifecycleResumeContext

Add an immutable provider-neutral model exposing at least:

- `RepositoryDirectory`
- `Repository`
- `PullRequestNumber`
- `FeatureBranch`
- `BaseBranch`
- `GitRemoteName`

Validation:

- repository directory must be non-empty,
- repository identity must not be null,
- Pull Request number must be greater than zero,
- feature branch must be non-empty,
- base branch must be non-empty,
- Git remote name must be non-empty,
- feature branch and base branch must differ using ordinal comparison.

The context represents information already known from the earlier lifecycle/PR stage.

Do not include:

- CI head SHA,
- expected merge SHA,
- merge result,
- credentials,
- HTTP data.

## Resume Result Model

### DeveloperLifecycleResumeResult

Add an immutable provider-neutral model exposing at least:

- `State`
- `Context`
- `StatusGate`
- optional `GatedMerge`
- optional `Cleanup`

Use `DeveloperLifecycleState` from DEV-0017.

Semantics:

### Pending

- `State == Pending`
- exact resume context is preserved
- exact status result is preserved
- `GatedMerge == null`
- `Cleanup == null`

### Failed

- `State == Failed`
- exact resume context is preserved
- exact status result is preserved
- `GatedMerge == null`
- `Cleanup == null`

### Completed

- `State == Completed`
- status result is Successful
- confirmed successful gated merge is present
- cleanup result is present

Enforce these invariants in the model.

## Core Abstraction

### IDeveloperLifecycleResumer

Add a mockable asynchronous abstraction.

The operation must accept:

- `DeveloperLifecycleResumeContext context`
- `PullRequestMergeMethod mergeMethod`
- optional merge commit title
- optional merge commit message
- `deleteRemoteBranch`
- optional `CancellationToken`

It returns `DeveloperLifecycleResumeResult`.

Do not accept:

- Pull Request head SHA,
- merge expected SHA,
- another Pull Request number separate from the context.

## Phase 1 – Current Status Evaluation

Call `IPullRequestStatusGate` using:

- `context.Repository`
- `context.PullRequestNumber`
- cancellation token.

### Pending

Return a `Pending` result.

Do not call merge gate.
Do not call cleanup.

### Failed

Return a `Failed` result.

Do not call merge gate.
Do not call cleanup.

### Successful

Proceed to guarded merge.

## Phase 2 – Guarded Merge

Call `IPullRequestMergeGate` using:

- `context.Repository`
- `context.PullRequestNumber`
- supplied merge method
- supplied optional merge title/message
- cancellation token.

DEV-0015 performs its own fresh status evaluation immediately before the merge.

This second evaluation is intentional.

DEV-0018 must not pass the first status result into DEV-0015 or attempt to bypass its stale-head safety.

If merge gate fails because the status changed to Pending/Failed, propagate the failure.

Do not retry.

## Merge Confirmation

After successful `IPullRequestMergeGate` return:

- require `GatedMerge.Merge.Merged == true`.

If the dependency returns an inconsistent non-merged result:

- fail clearly,
- do not call cleanup.

Use the exact merge result returned by DEV-0015 for cleanup.

## Phase 3 – Cleanup

Call `IPostMergeCleaner` using:

- `context.RepositoryDirectory`
- `context.Repository`
- `context.PullRequestNumber`
- exact merge result from the guarded merge
- `context.FeatureBranch`
- `context.BaseBranch`
- `context.GitRemoteName`
- supplied `deleteRemoteBranch`
- cancellation token.

Do not derive or alter these values.

## Why Resume Context Is Explicit

DEV-0018 must not rediscover the Pull Request or feature branch.

The earlier lifecycle stage already knows:

- which repository is involved,
- which Pull Request exists,
- which feature branch was pushed,
- which base branch is targeted,
- which Git remote is used.

Making this context explicit allows a later process invocation to continue safely without repeating earlier mutations.

Persistence of the context is outside DEV-0018.

## Failure / Short-Circuit Behavior

### Invalid context

Fail before dependency calls.

### Status-gate exception

Propagate.
Do not merge or cleanup.

### Pending

Return normally.
Do not merge or cleanup.

### Failed

Return normally.
Do not merge or cleanup.

### Merge-gate exception

Propagate.
Do not cleanup.

### Inconsistent non-merged result

Fail.
Do not cleanup.

### Cleanup exception

Propagate.
Do not retry merge.
Do not retry cleanup.
Do not rollback.

## Cancellation

Propagate the same `CancellationToken` to every asynchronous dependency.

Cancellation stops later phases.

Do not convert cancellation into a normal lifecycle state.

## Tests

Use injected fakes/stubs.

No test may require GitHub, network access, real Git repositories, or child processes.

Cover at least:

### Resume context

1. Valid context is immutable.
2. Empty repository directory rejected.
3. Null repository rejected.
4. Invalid PR number rejected.
5. Empty feature branch rejected.
6. Empty base branch rejected.
7. Empty remote rejected.
8. Feature/base equality rejected.

### Pending

9. Status gate called with exact repository.
10. Status gate called with exact PR number.
11. Pending returns `DeveloperLifecycleState.Pending`.
12. Exact context preserved.
13. Exact status result preserved.
14. Gated merge is null.
15. Cleanup is null.
16. Merge gate not called.
17. Cleaner not called.

### Failed

18. Failed returns `DeveloperLifecycleState.Failed`.
19. Exact context preserved.
20. Exact status result preserved.
21. Gated merge is null.
22. Cleanup is null.
23. Merge gate not called.
24. Cleaner not called.

### Successful

25. Successful status calls merge gate.
26. Merge gate receives exact repository.
27. Merge gate receives exact PR number.
28. Merge method delegated exactly.
29. Merge title delegated exactly.
30. Merge message delegated exactly.
31. Successful merge calls cleanup.
32. Cleanup receives exact repository directory.
33. Cleanup receives exact repository identity.
34. Cleanup receives exact PR number.
35. Cleanup receives exact merge result.
36. Cleanup receives exact feature branch from context.
37. Cleanup receives exact base branch from context.
38. Cleanup receives exact remote from context.
39. Cleanup receives exact deleteRemoteBranch.
40. Completed result has `DeveloperLifecycleState.Completed`.
41. Completed result preserves exact context.
42. Completed result preserves exact explicit status result.
43. Completed result preserves exact gated merge result.
44. Completed result preserves exact cleanup result.

### Fresh merge-gate safety

45. Explicit status evaluation occurs before merge gate.
46. Resume workflow does not pass/override head SHA.
47. Resume workflow does not bypass DEV-0015.
48. Merge-gate failure after explicit Successful prevents cleanup.
49. No retry after merge-gate failure.

### Failure behavior

50. Status exception prevents merge and cleanup.
51. Inconsistent non-merged merge result prevents cleanup.
52. Cleanup failure propagates.
53. Cleanup failure does not trigger second merge.
54. No retry is performed.

### Cancellation

55. Cancellation token propagated to status gate.
56. Cancellation token propagated to merge gate.
57. Cancellation token propagated to cleanup.
58. Cancellation/failure prevents later phases.

### Result invariants

59. Pending cannot contain merge/cleanup.
60. Failed cannot contain merge/cleanup.
61. Completed requires successful status, confirmed merge, and cleanup.

### Regression

62. Existing DEV-0002 through DEV-0017 tests continue to pass.

Avoid unrelated refactoring.

## Out of Scope

Do not implement:

- Developer Task parsing,
- Developer Task completion,
- review parsing/validation,
- stage,
- commit,
- push,
- Pull Request creation or discovery,
- persistence of resume context,
- serialization of resume context,
- database/files for lifecycle state,
- polling,
- waiting,
- timers,
- delays,
- scheduled monitoring,
- retry policies,
- automatic invocation after CI completes,
- GitHub Actions reruns,
- merge REST implementation,
- status REST implementation,
- cleanup Git implementation,
- rollback,
- automatic next Developer Task,
- Codex execution,
- CLI resume command,
- daemon/service/worker.

These belong to later Developer Tasks.

## Verification

Run:

```text
dotnet build
```

Required:

- 0 errors,
- no new warnings caused by DEV-0018.

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

DEV-0018 is complete when:

1. `DeveloperLifecycleResumeContext` exists as an immutable validated Core model.
2. `DeveloperLifecycleResumeResult` exists as an immutable Core model with lifecycle invariants.
3. `IDeveloperLifecycleResumer` exists as a mockable asynchronous Core abstraction.
4. Concrete resume orchestration exists in Tasks.
5. DEV-0014 status gate is reused.
6. DEV-0015 merge gate is reused with its fresh-gate safety intact.
7. DEV-0016 post-merge cleaner is reused.
8. No DEV-0013 task workflow is invoked.
9. Pending returns normally without merge/cleanup.
10. Failed returns normally without merge/cleanup.
11. Successful status proceeds to guarded merge.
12. Confirmed merge proceeds to cleanup.
13. Context values are delegated unchanged.
14. No caller-supplied head SHA is accepted.
15. Dependency failures short-circuit later phases.
16. No polling, retry, HTTP, Git process, shell, or provider logic is added to Tasks.
17. Cancellation is propagated.
18. Tests require no network/process/real repository.
19. Existing tests continue to pass.
20. `dotnet build` succeeds.
21. `dotnet test` succeeds.
22. `git diff --check` succeeds.
23. No out-of-scope functionality is implemented.
24. `docs/developer-reviews/REVIEW-0018.md` is created.

## Codex Completion Protocol

After implementation and verification:

1. Do **not** create a Git commit.
2. Do **not** push changes.
3. Do **not** modify this Developer Task.
4. Create:

   `docs/developer-reviews/REVIEW-0018.md`

5. The review report must contain:

```text
# REVIEW-0018 – Resume Developer Lifecycle

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

## Deviations from DEV-0018

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

The review report is part of DEV-0018 and must be included in the later Pull Request.
