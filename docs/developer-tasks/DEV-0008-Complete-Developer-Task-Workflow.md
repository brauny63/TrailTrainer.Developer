# DEV-0008 – Complete Developer Task Workflow

## Metadata

- Task ID: `DEV-0008`
- Repository: `TrailTrainer.Developer`
- Expected branch: `feature/dev-0008-complete-developer-task-workflow`
- Review report: `docs/developer-reviews/REVIEW-0008.md`
- Depends on: `DEV-0004`, `DEV-0005`, `DEV-0006`, `DEV-0007`

## Goal

Add a focused workflow for completing an already implemented Developer Task.

The workflow must combine the existing task parser, repository status provider, stager, committer, and pusher abstractions.

This package completes the local Git workflow for a Developer Task but does not create or merge a Pull Request and does not use the GitHub API.

## Codex Execution Instructions

Work this Developer Task completely.

- Follow `docs/architecture/README.md`.
- Implement only the scope defined here.
- Reuse existing abstractions from DEV-0004 through DEV-0007.
- Put reusable workflow contracts/models in `TrailTrainer.Developer.Core`.
- Put concrete orchestration in `TrailTrainer.Developer.Tasks`.
- Do not introduce direct Git process execution in `TrailTrainer.Developer.Tasks`.
- Do not modify this Developer Task.
- Do not modify architecture documentation.
- Do not implement anything under **Out of Scope**.
- Do not create a Git commit for the DEV-0008 implementation itself.
- Do not push the DEV-0008 implementation branch.
- After implementation and verification create `docs/developer-reviews/REVIEW-0008.md`.

If an ambiguity prevents correct completion, do not invent behavior. Document it and set the review status to `BLOCKED`.

## Scope

Implement a workflow that completes one already implemented Developer Task.

The operation must:

1. Parse the selected Developer Task file.
2. Validate that the task `Repository` metadata matches the expected repository name supplied to the workflow.
3. Resolve Git repository status for the supplied repository directory.
4. Require the current branch to equal the task's exact `ExpectedBranch`.
5. Require that the working tree contains uncommitted changes before completion.
6. Stage all repository changes using `IGitStager`.
7. Require staged changes after staging.
8. Create a commit using `IGitCommitter`.
9. Push the current branch using `IGitPusher`.
10. Return a strongly typed result describing the completed workflow.

The workflow must not create a Pull Request.

## Core Model

Add an immutable result model in `TrailTrainer.Developer.Core`.

### DeveloperTaskCompletionResult

Must expose at least:

- `TaskId`
- `TaskTitle`
- `RepositoryRoot`
- `BranchName`
- `CommitSha`
- `CommitMessage`
- `RemoteName`
- `SetUpstream`
- `TaskFilePath`
- `ReviewReportPath`

The result must not expose Git process output, parser internals, credentials, or provider-specific details.

## Core Abstraction

Add a mockable asynchronous abstraction in `TrailTrainer.Developer.Core`.

### IDeveloperTaskCompleter

The API must accept:

- Developer Task file path,
- repository directory path,
- expected repository name,
- commit message,
- remote name,
- `setUpstream`,
- optional `CancellationToken`.

It returns `DeveloperTaskCompletionResult`.

## Implementation

Implement `IDeveloperTaskCompleter` in `TrailTrainer.Developer.Tasks`.

The implementation must depend on abstractions.

Expected dependencies:

- `IDeveloperTaskParser`
- `IGitRepositoryStatusProvider`
- `IGitStager`
- `IGitCommitter`
- `IGitPusher`

Constructor injection must be supported so the workflow can be unit tested using fakes/stubs.

A convenience constructor using existing concrete implementations is allowed if consistent with project style.

## Input Validation

Before any mutating Git operation:

1. Reject null, empty, or whitespace-only expected repository name.
2. Reject null, empty, or whitespace-only commit message.
3. Reject null, empty, or whitespace-only remote name.
4. Parse the task.
5. Validate task repository metadata against the expected repository name using ordinal comparison.

Repository metadata mismatch must fail before staging.

## Repository Preconditions

Use `IGitRepositoryStatusProvider`.

Before staging:

1. The supplied directory must resolve to a Git repository.
2. `RepositoryRoot` must be available.
3. `CurrentBranch` must be available.
4. `CurrentBranch` must equal the task's exact `ExpectedBranch` using ordinal comparison.
5. `HasUncommittedChanges` must be `true`.

If any precondition fails:

- stop immediately,
- do not call `IGitStager`,
- do not call `IGitCommitter`,
- do not call `IGitPusher`.

Detached HEAD therefore fails.

Do not allow completion from `main` unless the task's `ExpectedBranch` is literally `main`.

## Staging

Call `IGitStager.StageAllAsync`.

After staging:

- `HasStagedChanges` must be `true`.

If the stager returns `HasStagedChanges == false`:

- fail,
- do not call the committer,
- do not call the pusher.

The workflow must not perform its own Git staging logic.

## Commit

Call `IGitCommitter.CommitAsync` only after successful staging.

Use the exact commit message supplied to the workflow.

Do not rewrite, prefix, normalize, or generate the commit message.

The returned `CommitSha` and `CommitMessage` from the committer must be used in the completion result.

## Push

Call `IGitPusher.PushAsync` only after a successful commit.

Use:

- the exact remote name supplied to the workflow,
- the exact `setUpstream` value supplied to the workflow.

The branch being pushed remains the current task branch as enforced by the existing pusher.

Do not retry using a different remote or branch.

## Operation Ordering

The workflow must guarantee this order:

1. Validate simple inputs.
2. Parse task.
3. Validate repository metadata.
4. Get repository status.
5. Validate repository preconditions.
6. Stage all changes.
7. Validate staged-change result.
8. Commit staged changes.
9. Push branch.
10. Return result.

No later operation may run when an earlier step fails.

## Tests

Add unit tests using hand-written fakes/stubs unless a mocking framework already exists.

Workflow unit tests must not require real Git repositories.

Cover at least:

1. Successful completion returns expected result values.
2. Task ID and title are returned.
3. Repository root and branch are returned.
4. Commit SHA and commit message are returned from committer result.
5. Remote name and `setUpstream` are returned.
6. Task file path and review report path are returned.
7. Exact operation order is parse → status → stage → commit → push.
8. Committer is called exactly once on success.
9. Pusher is called exactly once on success.
10. Exact commit message is passed to the committer.
11. Exact remote name and `setUpstream` are passed to the pusher.
12. Empty/whitespace expected repository name is rejected.
13. Empty/whitespace commit message is rejected.
14. Empty/whitespace remote name is rejected.
15. Repository metadata mismatch fails before any mutation.
16. Non-repository status fails before staging.
17. Detached HEAD fails before staging.
18. Current branch differing from task `ExpectedBranch` fails before staging.
19. Clean working tree fails before staging.
20. `HasStagedChanges == false` after staging fails before commit.
21. Staging failure prevents commit and push.
22. Commit failure prevents push.
23. Push failure is propagated.
24. Cancellation token is propagated to every dependency.
25. Exact task `ExpectedBranch` is used only for validation and is not rewritten.
26. Existing DEV-0002 through DEV-0007 tests continue to pass.

Tests must verify call counts and absence of later side effects after failures.

## Out of Scope

Do not implement:

- Pull Request creation,
- GitHub API integration,
- merging,
- branch deletion,
- checkout/switch back to `main`,
- pull/fetch/rebase,
- automatic task discovery or selection,
- automatic next-task selection,
- automatic commit-message generation,
- automatic remote selection,
- retry policies,
- Codex execution,
- review-report parsing,
- task status persistence,
- editing Developer Task files,
- CLI commands,
- process/shell execution inside Tasks.

These belong to later Developer Tasks.

## Verification

Run for the complete solution:

```text
dotnet build
```

Required:

- 0 errors,
- no new warnings caused by DEV-0008.

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

DEV-0008 is complete when:

1. `DeveloperTaskCompletionResult` exists as an immutable Core model.
2. `IDeveloperTaskCompleter` exists as a mockable asynchronous abstraction.
3. A concrete workflow implementation exists in `TrailTrainer.Developer.Tasks`.
4. The workflow composes existing parser, status, stager, committer, and pusher abstractions.
5. No Git process execution is introduced in Tasks.
6. Repository metadata is validated before mutation.
7. Non-repository, detached HEAD, wrong branch, and clean-tree states are rejected.
8. The current branch must equal the task's exact `ExpectedBranch`.
9. All changes are staged through `IGitStager`.
10. No commit occurs when no staged changes remain after staging.
11. Commit uses the exact supplied message.
12. Push uses the exact supplied remote and `setUpstream`.
13. Later operations are not invoked after an earlier failure.
14. The completion result returns task, repository, branch, commit, push, and path information.
15. Required unit tests cover success, ordering, validation, failure short-circuiting, and cancellation.
16. Existing tests continue to pass.
17. `dotnet build` succeeds.
18. `dotnet test` succeeds.
19. `git diff --check` reports no whitespace errors.
20. No out-of-scope functionality is implemented.
21. `docs/developer-reviews/REVIEW-0008.md` is created according to the completion protocol.

## Codex Completion Protocol

After implementation and verification:

1. Do **not** create a Git commit.
2. Do **not** push changes.
3. Do **not** modify this Developer Task.
4. Create:

   `docs/developer-reviews/REVIEW-0008.md`

5. The review report must contain:

```text
# REVIEW-0008 – Complete Developer Task Workflow

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

## Deviations from DEV-0008

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

The review report is part of DEV-0008 and must be included in the later Pull Request.
