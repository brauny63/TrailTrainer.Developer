# DEV-0013 – End-to-End Developer Workflow

## Metadata

- Task ID: `DEV-0013`
- Repository: `TrailTrainer.Developer`
- Expected branch: `feature/dev-0013-end-to-end-developer-workflow`
- Review report: `docs/developer-reviews/REVIEW-0013.md`
- Depends on: `DEV-0011`, `DEV-0012`

## Goal

Add a single orchestration workflow that completes an implemented Developer Task and ensures that a GitHub Pull Request exists for the resulting pushed feature branch.

The workflow must reuse the existing review-gated completion capability from DEV-0011 and the Pull Request integration from DEV-0012.

DEV-0013 is orchestration only. It must not duplicate review parsing/validation, Git stage/commit/push, or GitHub REST implementation logic.

## Codex Execution Instructions

Work this Developer Task completely.

- Follow `docs/architecture/README.md`.
- Implement only the scope defined here.
- Reuse `IDeveloperTaskGatedCompleter` from DEV-0011.
- Reuse `IPullRequestService` from DEV-0012.
- Reuse existing task parsing/status abstractions only where required to obtain task metadata or repository/branch information not present in existing result models.
- Keep reusable workflow contracts/models in `TrailTrainer.Developer.Core`.
- Put concrete workflow orchestration in `TrailTrainer.Developer.Tasks`.
- Do not introduce HTTP, Git process, shell, or GitHub REST logic in Tasks.
- Do not duplicate validation rules from DEV-0010/DEV-0011.
- Do not duplicate stage/commit/push logic from DEV-0008.
- Do not duplicate PR lookup/create logic from DEV-0012.
- Do not modify this Developer Task or architecture documentation.
- Do not implement anything under **Out of Scope**.
- Do not create a Git commit for DEV-0013.
- Do not push the DEV-0013 implementation branch.
- After implementation and verification create `docs/developer-reviews/REVIEW-0013.md`.

If an ambiguity prevents correct completion, do not invent behavior. Document it and set the review report status to `BLOCKED`.

## Scope

Implement an end-to-end workflow that:

1. Resolves/parses the selected Developer Task as needed.
2. Executes the existing review-gated completion workflow.
3. Uses the resulting pushed branch to ensure an open GitHub Pull Request exists.
4. Returns one strongly typed result containing both completion and Pull Request information.

The required order is:

```text
Developer Task
      |
      v
Review-Gated Completion
      |
      v
Stage -> Commit -> Push
      |
      v
Ensure Open Pull Request
      |
      +-- existing PR -> return it
      |
      +-- no PR -> create it
```

No Pull Request request may occur before gated completion succeeds.

## Core Model

### DeveloperTaskWorkflowResult

Add an immutable model exposing at least:

- `TaskId`
- `Completion`
- `PullRequest`

Where:

- `Completion` is `DeveloperTaskGatedCompletionResult`.
- `PullRequest` is `PullRequestEnsureResult`.

Do not duplicate nested result fields unless needed for a clear API.

## Core Abstraction

### IDeveloperTaskWorkflow

Add a mockable asynchronous abstraction.

The operation must accept at least:

- Developer Task file path,
- repository directory path,
- expected repository name,
- commit message,
- Git remote name,
- `setUpstream`,
- `GitHubRepositoryIdentity`,
- Pull Request base branch,
- optional Pull Request body,
- optional Pull Request draft flag,
- optional `CancellationToken`.

It returns `DeveloperTaskWorkflowResult`.

The workflow must derive the Pull Request head branch from the successful completion result rather than accepting a separate caller-supplied head branch.

## Pull Request Title

The Pull Request title must be derived from the Developer Task document.

Use:

```text
DEV-NNNN – <task title>
```

Use an en dash between task ID and title.

Do not use the commit message as the Pull Request title.

Do not rewrite the task title.

If the existing parsed task title already contains the task ID prefix, avoid duplicating the ID. Prefer the smallest implementation consistent with the existing `DeveloperTaskDocument` semantics and cover the chosen behavior with tests.

## Repository / Task Metadata

The workflow may inject `IDeveloperTaskParser` if needed to obtain the task title and identity.

Do not reparse the review report.

Do not duplicate task validation already performed by the gated completer.

If task parsing is performed by this orchestration for title generation, parsing must happen before any mutating completion call.

## Completion Delegation

Call `IDeveloperTaskGatedCompleter` with these values unchanged:

- Developer Task file path,
- repository directory path,
- expected repository name,
- commit message,
- Git remote name,
- `setUpstream`,
- cancellation token.

Do not duplicate its review gate or completion behavior.

## Pull Request Head Branch

Use the pushed branch represented by the successful completion result.

Do not infer the head branch from:

- the task filename,
- the task's expected branch alone,
- the current process directory,
- GitHub,
- caller input.

The completed/pushed result is authoritative.

If the existing nested completion result exposes both commit and push results, use the branch reported by the push result.

## Pull Request Delegation

After successful gated completion, call `IPullRequestService.EnsureOpenAsync` with:

- supplied `GitHubRepositoryIdentity`,
- head branch from completion result,
- supplied base branch,
- derived task title,
- supplied PR body,
- supplied PR draft flag,
- cancellation token.

Pass body, base branch, repository identity, and draft flag unchanged.

Do not implement PR lookup/create behavior in this workflow.

## Failure / Short-Circuit Behavior

The workflow must short-circuit.

### Task parsing failure

If task parsing used by this workflow fails:

- do not call gated completion,
- do not call Pull Request service,
- propagate the failure.

### Gated completion failure

If gated completion fails for any reason:

- do not call Pull Request service,
- propagate the failure.

This includes:

- invalid review,
- repository mismatch,
- branch mismatch,
- staging failure,
- commit failure,
- push failure.

### Pull Request failure

If gated completion succeeds but PR ensure fails:

- propagate the PR failure,
- do not attempt to undo the successful commit or push,
- do not retry automatically in this task.

A later repeated workflow call may rely on the idempotent PR behavior from DEV-0012.

## Cancellation

Propagate the same `CancellationToken` to every asynchronous dependency.

Cancellation before PR creation must prevent further workflow operations.

Do not translate cancellation into a normal validation failure.

## Tests

Add unit tests using injected fakes/stubs.

Tests must not require:

- real Git repositories,
- GitHub,
- network access,
- child processes.

Cover at least:

### Successful workflow

1. Successful gated completion calls PR service.
2. Result contains the task ID.
3. Result contains the exact gated completion result.
4. Result contains the exact PR ensure result.
5. Existing PR result (`Created == false`) is returned.
6. Newly created PR result (`Created == true`) is returned.

### Ordering

7. Task parsing, when used, occurs before gated completion.
8. Gated completion occurs before PR ensure.
9. PR ensure is never called before gated completion returns successfully.

### Completion delegation

10. Exact task file path is passed unchanged.
11. Exact repository directory is passed unchanged.
12. Exact expected repository name is passed unchanged.
13. Exact commit message is passed unchanged.
14. Exact Git remote name is passed unchanged.
15. Exact `setUpstream` value is passed unchanged.

### Pull Request delegation

16. Exact `GitHubRepositoryIdentity` is passed unchanged.
17. Exact base branch is passed unchanged.
18. Exact PR body is passed unchanged.
19. Exact draft flag is passed unchanged.
20. Head branch comes from the successful push/completion result.
21. Caller cannot independently override the PR head branch.
22. PR title uses task ID plus task title.
23. PR title does not use the commit message.
24. Task ID is not duplicated in the title when existing task-document semantics already include it.

### Failure behavior

25. Task parser failure prevents gated completion and PR ensure.
26. Gated completion failure prevents PR ensure.
27. Review-gate failure prevents PR ensure.
28. Push/completion failure prevents PR ensure.
29. PR ensure failure is propagated.
30. PR failure does not trigger another completion attempt.
31. PR failure does not trigger rollback behavior.

### Cancellation

32. Cancellation token is passed to task parser when used.
33. Cancellation token is passed to gated completer.
34. Cancellation token is passed to PR service.
35. Cancellation/failure before PR ensure prevents PR invocation.

### Regression

36. Existing DEV-0002 through DEV-0012 tests continue to pass.

Avoid unrelated refactoring.

## Out of Scope

Do not implement:

- new review parsing rules,
- new review validation rules,
- Git staging logic,
- Git commit logic,
- Git push logic,
- Git process execution,
- Git remote discovery,
- GitHub authentication management,
- GitHub REST calls in Tasks,
- Pull Request lookup/create implementation,
- Pull Request merge,
- mergeability checks,
- CI/status checks,
- required-check evaluation,
- Pull Request review submission,
- reviewer requests,
- comments,
- labels,
- assignees,
- milestones,
- auto-merge,
- branch deletion,
- switching back to `main`,
- pull/fetch/rebase,
- rollback of successful commits or pushes,
- retry policy,
- Codex execution,
- automatic next-task selection,
- CLI command for the end-to-end workflow,
- configuration files.

These belong to later Developer Tasks.

## Verification

Run:

```text
dotnet build
```

Required:

- 0 errors,
- no new warnings caused by DEV-0013.

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

DEV-0013 is complete when:

1. `DeveloperTaskWorkflowResult` exists as an immutable Core model.
2. `IDeveloperTaskWorkflow` exists as a mockable asynchronous Core abstraction.
3. Concrete workflow orchestration exists in `TrailTrainer.Developer.Tasks`.
4. Existing gated completion is reused.
5. Existing Pull Request service is reused.
6. No review, Git, push, or GitHub REST rules are duplicated.
7. Gated completion occurs before PR ensure.
8. Failed completion prevents all PR operations.
9. Successful completion supplies the authoritative PR head branch.
10. PR title is derived from task ID and task title.
11. PR repository identity, base branch, body, and draft are delegated unchanged.
12. Existing and newly created PR results are both supported.
13. PR failure is propagated without rollback or repeated completion.
14. Cancellation is propagated.
15. No HTTP/process/shell execution is introduced in Tasks.
16. Required unit tests cover success, ordering, delegation, short-circuiting, failures, title/head derivation, and cancellation.
17. Existing tests continue to pass.
18. `dotnet build` succeeds.
19. `dotnet test` succeeds.
20. `git diff --check` succeeds.
21. No out-of-scope functionality is implemented.
22. `docs/developer-reviews/REVIEW-0013.md` is created according to the completion protocol.

## Codex Completion Protocol

After implementation and verification:

1. Do **not** create a Git commit.
2. Do **not** push changes.
3. Do **not** modify this Developer Task.
4. Create:

   `docs/developer-reviews/REVIEW-0013.md`

5. The review report must contain:

```text
# REVIEW-0013 – End-to-End Developer Workflow

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

## Deviations from DEV-0013

## Open Issues / Known Limitations

## Commit and Push
No commit created.
No push performed.
```

6. Use `READY FOR REVIEW` only when all acceptance criteria and verification succeed.
7. Otherwise use `BLOCKED` and document the reason.
8. Record build success/failure, warning/error counts, test passed/failed/skipped counts, and `git diff --check`.
9. List every file created, modified, or deleted.
10. Write `None` when there are no deviations or open issues.

The review report is part of DEV-0013 and must be included in the later Pull Request.
