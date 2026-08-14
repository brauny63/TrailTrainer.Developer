# DEV-0017 – Complete Developer Lifecycle Orchestration

## Metadata

- Task ID: `DEV-0017`
- Repository: `TrailTrainer.Developer`
- Expected branch: `feature/dev-0017-complete-developer-lifecycle-orchestration`
- Review report: `docs/developer-reviews/REVIEW-0017.md`
- Depends on: `DEV-0013`, `DEV-0014`, `DEV-0015`, `DEV-0016`

## Goal

Add a provider-neutral top-level orchestration capability for the complete Developer Task lifecycle already implemented by DEV-0013 through DEV-0016.

The orchestrator must compose the existing capabilities in this order:

1. Complete the Developer Task workflow and ensure an open Pull Request exists.
2. Evaluate the Pull Request CI/status gate.
3. Stop without merging when CI/status is `Pending`.
4. Stop without merging when CI/status is `Failed`.
5. When CI/status is `Successful`, execute the existing guarded Pull Request merge.
6. After a confirmed successful merge, execute post-merge cleanup.
7. Return a strongly typed lifecycle result describing how far the lifecycle progressed.

DEV-0017 is orchestration only. It must not duplicate Git, GitHub REST, review validation, status normalization, merge, or cleanup implementation logic.

## Codex Execution Instructions

Work this Developer Task completely.

- Follow `docs/architecture/README.md`.
- Implement only the scope defined here.
- Reuse `IDeveloperTaskWorkflow` from DEV-0013.
- Reuse `IPullRequestStatusGate` from DEV-0014.
- Reuse `IPullRequestMergeGate` from DEV-0015.
- Reuse `IPostMergeCleaner` from DEV-0016.
- Keep provider-neutral lifecycle contracts/models in `TrailTrainer.Developer.Core`.
- Put concrete lifecycle orchestration in `TrailTrainer.Developer.Tasks`.
- Do not introduce HTTP, Git process, shell, or GitHub REST logic in Tasks.
- Do not duplicate validation or safety rules owned by existing capabilities.
- Do not poll, sleep, retry, or wait for CI.
- Do not modify this Developer Task or architecture documentation.
- Do not implement anything under **Out of Scope**.
- Do not create a Git commit for DEV-0017.
- Do not push the DEV-0017 implementation branch.
- After implementation and verification create `docs/developer-reviews/REVIEW-0017.md`.

If an ambiguity prevents correct completion, do not invent behavior. Document it and set the review status to `BLOCKED`.

## Scope

Implement one top-level lifecycle operation that coordinates the existing workflow, CI gate, merge gate, and cleanup.

Required sequence:

```text
Developer Task
      |
      v
IDeveloperTaskWorkflow
      |
      v
Pull Request ensured
      |
      v
IPullRequestStatusGate
      |
      +-- Pending --> return Pending result
      |
      +-- Failed ---> return Failed result
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
      Completed result
```

No later phase may run when an earlier phase has not completed successfully.

## Lifecycle State

### DeveloperLifecycleState

Add a strongly typed state supporting exactly:

- `Pending`
- `Failed`
- `Completed`

Semantics:

### Pending

The Developer Task workflow succeeded and an open Pull Request exists, but the current CI/status gate is `Pending`.

This is a normal non-terminal-for-the-real-world but terminal-for-this-invocation result.

Do not throw merely because CI is pending.

### Failed

The Developer Task workflow succeeded and an open Pull Request exists, but the current CI/status gate is `Failed`.

This invocation must stop before merge.

Do not call merge or cleanup.

This state represents a CI/status gate failure, not arbitrary exceptions from dependencies.

### Completed

The workflow succeeded, CI/status was successful, the guarded merge succeeded, and post-merge cleanup succeeded.

## Lifecycle Result

### DeveloperLifecycleResult

Add an immutable provider-neutral result exposing at least:

- `State`
- `Workflow`
- `StatusGate`
- optional `GatedMerge`
- optional `Cleanup`

Where:

- `Workflow` is the exact `DeveloperTaskWorkflowResult`.
- `StatusGate` is the exact `PullRequestStatusGateResult` from the explicit lifecycle status check.
- `GatedMerge` is null for `Pending` and `Failed`, and present for `Completed`.
- `Cleanup` is null for `Pending` and `Failed`, and present for `Completed`.

Enforce sensible invariants in the model where consistent with the existing project style.

For `Completed`:

- `GatedMerge` must be present.
- `Cleanup` must be present.
- the merge result must represent a successful merge.

For `Pending` and `Failed`:

- `GatedMerge` must be null.
- `Cleanup` must be null.

Do not duplicate nested fields such as PR URL, head SHA, commit SHA, or repository root unless required for a clear API.

## Core Abstraction

### IDeveloperLifecycleOrchestrator

Add a mockable asynchronous Core abstraction.

The operation must accept the inputs needed by the existing phases, including at least:

### Developer Task workflow inputs

- Developer Task file path
- repository directory path
- expected repository name
- commit message
- Git remote name
- `setUpstream`
- `GitHubRepositoryIdentity`

### Pull Request inputs

- base branch
- optional Pull Request body
- optional Pull Request draft flag

### Merge inputs

- `PullRequestMergeMethod`
- optional merge commit title
- optional merge commit message

### Cleanup inputs

- `deleteRemoteBranch`

### Common

- optional `CancellationToken`

Do not accept:

- Pull Request number from the caller,
- Pull Request head SHA from the caller,
- feature branch for cleanup from the caller.

Those values must be derived from prior successful phase results.

The operation returns `DeveloperLifecycleResult`.

## Phase 1 – Developer Task Workflow

Call `IDeveloperTaskWorkflow` first.

Delegate the workflow inputs unchanged.

Use the returned `DeveloperTaskWorkflowResult` as authoritative for the Pull Request created/found by DEV-0013.

The Pull Request number for all subsequent phases must come from the workflow's `PullRequest` result.

Do not independently search for another Pull Request.

The feature branch used later for cleanup must ultimately be derived from the successful workflow/completion result, not from the task filename or caller input.

## Phase 2 – Explicit CI / Status Evaluation

After the workflow succeeds, call `IPullRequestStatusGate` with:

- the supplied repository identity,
- Pull Request number from the workflow result,
- cancellation token.

This explicit status result determines whether this invocation stops or proceeds.

### Pending

If state is `Pending`:

- do not call `IPullRequestMergeGate`,
- do not call `IPostMergeCleaner`,
- return `DeveloperLifecycleResult` with state `Pending`,
- preserve the exact workflow and status-gate results.

### Failed

If state is `Failed`:

- do not call `IPullRequestMergeGate`,
- do not call `IPostMergeCleaner`,
- return `DeveloperLifecycleResult` with state `Failed`,
- preserve the exact workflow and status-gate results.

### Successful

Proceed to the merge phase.

## Why DEV-0014 Is Called Before DEV-0015

DEV-0015 intentionally performs its own fresh gate evaluation immediately before merge and binds the merge request to that gate's exact head SHA.

DEV-0017 must not bypass that safety.

Therefore, on the successful path:

1. DEV-0017 evaluates DEV-0014 to decide whether this invocation should proceed.
2. DEV-0017 then calls DEV-0015.
3. DEV-0015 performs its own fresh gate evaluation as designed by DEV-0015.

This means two status-gate evaluations on the successful lifecycle path are intentional.

Do not optimize them into one check.

The second evaluation is the stale-head safety boundary immediately before merge.

## Phase 3 – Guarded Merge

When the explicit lifecycle status gate is `Successful`, call `IPullRequestMergeGate`.

Pass:

- supplied repository identity,
- Pull Request number from the workflow result,
- supplied merge method,
- supplied optional merge commit title,
- supplied optional merge commit message,
- cancellation token.

Do not pass or inject the earlier status result into DEV-0015.

DEV-0015 owns its fresh status evaluation.

If DEV-0015 observes `Pending` or `Failed` because status changed between the two evaluations, propagate its failure and do not cleanup.

Do not convert that race into the lifecycle `Pending`/`Failed` result from the earlier check.

Do not retry.

## Merge Result Requirements

After `IPullRequestMergeGate` returns successfully:

- its merge result must represent a confirmed successful merge before cleanup is attempted.

If an inconsistent result is returned by a dependency, fail clearly and do not cleanup.

Use the exact merge result returned by DEV-0015 for DEV-0016 merge confirmation.

## Phase 4 – Post-Merge Cleanup

Call `IPostMergeCleaner` only after a confirmed successful guarded merge.

Pass:

- original repository directory path,
- supplied repository identity,
- Pull Request number from the workflow result,
- exact `PullRequestMergeResult` from the guarded merge result,
- feature branch derived from the successful DEV-0013 workflow/completion result,
- supplied base branch,
- supplied Git remote name,
- supplied `deleteRemoteBranch`,
- cancellation token.

Do not derive the feature branch from:

- task filename,
- expected branch naming convention,
- Pull Request title,
- current local Git state,
- caller input.

Use the branch represented by the successful workflow/completion result.

## Completed Result

After cleanup succeeds, return:

- `State == Completed`
- exact workflow result
- exact explicit lifecycle status-gate result
- exact guarded merge result
- exact cleanup result

## Failure / Short-Circuit Behavior

Dependency exceptions must generally propagate.

### Workflow failure

If `IDeveloperTaskWorkflow` fails:

- do not call status gate,
- do not call merge gate,
- do not call cleanup,
- propagate the failure.

### Explicit status evaluation failure

If `IPullRequestStatusGate` throws:

- do not call merge gate,
- do not call cleanup,
- propagate the failure.

### Explicit Pending

Return `Pending`; do not merge or cleanup.

### Explicit Failed

Return `Failed`; do not merge or cleanup.

### Merge-gate failure

If `IPullRequestMergeGate` throws:

- do not cleanup,
- propagate the failure.

### Cleanup failure

If cleanup throws:

- propagate the failure,
- do not attempt rollback,
- do not repeat merge,
- do not retry cleanup automatically.

A successful merge followed by cleanup failure is therefore an exceptional partial-completion condition, not `Completed`.

## Cancellation

Propagate the same `CancellationToken` to every asynchronous dependency.

Cancellation must prevent subsequent phases.

Do not translate cancellation into `Pending`, `Failed`, or another normal lifecycle state.

## Ordering

Required order:

```text
Workflow
   ↓
Explicit Status Gate
   ↓
[Pending/Failed => return]
   ↓ Successful
Merge Gate
   ↓
Cleanup
   ↓
Completed
```

On the successful path, remember that `Merge Gate` internally performs the second/fresh DEV-0014 status evaluation required by DEV-0015.

## Tests

Use injected fakes/stubs.

Lifecycle orchestration tests must not require:

- real Git repositories,
- GitHub,
- network access,
- child processes.

Cover at least:

### Workflow delegation

1. Workflow is called first.
2. Exact task file path delegated.
3. Exact repository directory delegated.
4. Exact expected repository name delegated.
5. Exact commit message delegated.
6. Exact Git remote delegated.
7. Exact `setUpstream` delegated.
8. Exact repository identity delegated.
9. Exact PR base branch delegated.
10. Exact PR body delegated.
11. Exact PR draft flag delegated.

### Pull Request derivation

12. Status gate receives PR number from workflow result.
13. Merge gate receives PR number from workflow result.
14. Caller cannot supply/override PR number.
15. No independent PR lookup occurs.

### Pending lifecycle

16. Explicit Pending returns `DeveloperLifecycleState.Pending`.
17. Pending result preserves exact workflow result.
18. Pending result preserves exact status result.
19. Pending result has null gated merge.
20. Pending result has null cleanup.
21. Pending does not call merge gate.
22. Pending does not call cleanup.

### Failed lifecycle

23. Explicit Failed returns `DeveloperLifecycleState.Failed`.
24. Failed result preserves exact workflow result.
25. Failed result preserves exact status result.
26. Failed result has null gated merge.
27. Failed result has null cleanup.
28. Failed does not call merge gate.
29. Failed does not call cleanup.

### Successful lifecycle

30. Explicit Successful calls merge gate.
31. Merge gate receives exact repository identity.
32. Merge gate receives exact PR number.
33. Merge method delegated exactly.
34. Merge commit title delegated exactly.
35. Merge commit message delegated exactly.
36. Successful guarded merge calls cleanup.
37. Cleanup receives original repository directory.
38. Cleanup receives exact repository identity.
39. Cleanup receives PR number from workflow.
40. Cleanup receives exact merge result from guarded merge.
41. Cleanup feature branch comes from workflow/completion result.
42. Caller cannot supply/override cleanup feature branch.
43. Cleanup receives exact base branch.
44. Cleanup receives exact Git remote.
45. Cleanup receives exact `deleteRemoteBranch`.
46. Completed returns `DeveloperLifecycleState.Completed`.
47. Completed preserves exact workflow result.
48. Completed preserves exact explicit status result.
49. Completed preserves exact guarded merge result.
50. Completed preserves exact cleanup result.

### Safety / fresh gate semantics

51. Explicit status gate is evaluated before merge gate.
52. DEV-0017 does not replace or bypass DEV-0015's own gate behavior.
53. DEV-0017 does not pass the earlier status result as an override to merge gate.
54. Merge-gate failure after earlier Successful status prevents cleanup.
55. Inconsistent non-merged merge result prevents cleanup.

### Failure short-circuiting

56. Workflow exception prevents status, merge, and cleanup.
57. Status exception prevents merge and cleanup.
58. Merge exception prevents cleanup.
59. Cleanup exception propagates.
60. Cleanup exception does not trigger a second merge call.
61. No retry is performed by the lifecycle orchestrator.

### Cancellation

62. Cancellation token delegated to workflow.
63. Cancellation token delegated to explicit status gate.
64. Cancellation token delegated to merge gate.
65. Cancellation token delegated to cleanup.
66. Cancellation/failure in any phase prevents later phases.

### Result invariants

67. Pending cannot contain merge/cleanup results.
68. Failed cannot contain merge/cleanup results.
69. Completed requires merge and cleanup results.
70. Completed requires a confirmed successful merge result.

### Regression

71. Existing DEV-0002 through DEV-0016 tests continue to pass.

Avoid unrelated refactoring.

## Out of Scope

Do not implement:

- polling or waiting for CI,
- timers/delays,
- scheduled monitoring,
- automatic retries,
- retry after Pending,
- retry after Failed,
- retry after stale-head merge rejection,
- GitHub Actions triggering/rerunning,
- new CI/status normalization,
- new merge REST logic,
- new Git cleanup logic,
- Pull Request discovery beyond DEV-0013,
- automatic review approval,
- reviewer requests,
- comments,
- labels,
- merge queue integration,
- branch-protection discovery,
- rollback,
- automatic next Developer Task selection,
- automatic creation of the next DEV file,
- Codex execution,
- CLI lifecycle command,
- daemon/service/worker execution.

These belong to later Developer Tasks.

## Verification

Run:

```text
dotnet build
```

Required:

- 0 errors,
- no new warnings caused by DEV-0017.

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

DEV-0017 is complete when:

1. `DeveloperLifecycleState` exists with `Pending`, `Failed`, and `Completed`.
2. `DeveloperLifecycleResult` exists as an immutable provider-neutral model.
3. `IDeveloperLifecycleOrchestrator` exists as a mockable asynchronous Core abstraction.
4. Concrete orchestration exists in `TrailTrainer.Developer.Tasks`.
5. DEV-0013 workflow is reused.
6. DEV-0014 status gate is reused for the lifecycle decision.
7. DEV-0015 merge gate is reused without bypassing its fresh-gate safety.
8. DEV-0016 post-merge cleaner is reused.
9. PR number is derived from the DEV-0013 workflow result.
10. Cleanup feature branch is derived from the DEV-0013 workflow/completion result.
11. Pending returns normally without merge or cleanup.
12. Failed returns normally without merge or cleanup.
13. Successful explicit status proceeds to guarded merge.
14. Successful guarded merge proceeds to cleanup.
15. Completed contains exact workflow, explicit status, merge, and cleanup results.
16. Dependency failures short-circuit subsequent phases.
17. Cleanup failure does not cause re-merge or rollback.
18. No polling, waiting, retry, HTTP, shell, process, or duplicated provider logic is introduced in Tasks.
19. Cancellation is propagated.
20. Tests use fakes and require no GitHub/network/process execution.
21. Existing tests continue to pass.
22. `dotnet build` succeeds.
23. `dotnet test` succeeds.
24. `git diff --check` succeeds.
25. No out-of-scope functionality is implemented.
26. `docs/developer-reviews/REVIEW-0017.md` is created.

## Codex Completion Protocol

After implementation and verification:

1. Do **not** create a Git commit.
2. Do **not** push changes.
3. Do **not** modify this Developer Task.
4. Create:

   `docs/developer-reviews/REVIEW-0017.md`

5. The review report must contain:

```text
# REVIEW-0017 – Complete Developer Lifecycle Orchestration

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

## Deviations from DEV-0017

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

The review report is part of DEV-0017 and must be included in the later Pull Request.
