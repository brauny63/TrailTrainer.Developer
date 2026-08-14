# DEV-0007 – Start Developer Task Workflow

## Metadata

- Task ID: `DEV-0007`
- Repository: `TrailTrainer.Developer`
- Expected branch: `feature/dev-0007-start-developer-task-workflow`
- Review report: `docs/developer-reviews/REVIEW-0007.md`
- Depends on: `DEV-0002`, `DEV-0003`, `DEV-0006`

## Goal

Add the first explicit Developer Task workflow to `TrailTrainer.Developer`.

The workflow starts a selected Developer Task by combining the existing task parser, repository status provider, and branch creator.

This package must remain intentionally small. It starts a task but does not stage, commit, push, create Pull Requests, or execute Codex.

## Codex Execution Instructions

Work this Developer Task completely.

- Follow `docs/architecture/README.md`.
- Implement only the scope defined here.
- Reuse existing abstractions from DEV-0002, DEV-0003, and DEV-0006.
- Put reusable workflow contracts/models in `TrailTrainer.Developer.Core`.
- Put the concrete orchestration implementation in `TrailTrainer.Developer.Tasks`.
- Do not introduce direct Git process execution in `TrailTrainer.Developer.Tasks`.
- Do not modify this Developer Task.
- Do not modify architecture documentation.
- Do not implement anything under **Out of Scope**.
- Do not create a Git commit for DEV-0007.
- Do not push the DEV-0007 implementation branch.
- After verification create `docs/developer-reviews/REVIEW-0007.md`.

If an ambiguity prevents correct completion, do not invent behavior. Document it and set the review status to `BLOCKED`.

## Scope

Implement a workflow that starts one selected Developer Task.

The operation must:

1. Parse the selected Developer Task file.
2. Validate that the task's `Repository` metadata matches the expected repository name supplied to the workflow.
3. Resolve the Git repository containing the supplied repository path.
4. Reject non-repository directories.
5. Require the current Git branch to be `main`.
6. Require the working tree to have no uncommitted changes before starting.
7. Read the task's `ExpectedBranch`.
8. Create and switch to that branch using the existing `IGitBranchCreator`.
9. Return a strongly typed result describing the started task.

The workflow must not perform any operation beyond task validation and branch creation.

## Core Model

Add an immutable result model in `TrailTrainer.Developer.Core`.

### DeveloperTaskStartResult

Must expose at least:

- `TaskId`
- `TaskTitle`
- `RepositoryRoot`
- `PreviousBranch`
- `CreatedBranch`
- `TaskFilePath`
- `ReviewReportPath`

The result must not expose Git process output or parser internals.

## Core Abstraction

Add a mockable asynchronous abstraction in `TrailTrainer.Developer.Core`.

### IDeveloperTaskStarter

The API must accept:

- Developer Task file path,
- repository directory path,
- expected repository name,
- optional `CancellationToken`.

It returns `DeveloperTaskStartResult`.

## Implementation

Implement `IDeveloperTaskStarter` in `TrailTrainer.Developer.Tasks`.

The implementation must depend on abstractions, not concrete process execution.

Expected dependencies:

- `IDeveloperTaskParser`
- `IGitRepositoryStatusProvider`
- `IGitBranchCreator`

Constructor injection must be supported so the workflow can be unit tested with mocks/fakes.

A convenience constructor using the existing concrete implementations is allowed if consistent with the current project style.

## Repository Metadata Validation

The workflow receives an `expectedRepositoryName`.

Requirements:

1. Reject null, empty, or whitespace-only expected repository names.
2. Compare the parsed task's `Repository` metadata against the supplied expected repository name.
3. Comparison must be ordinal.
4. A mismatch must fail before any branch is created.
5. The exception must include both expected and actual repository names.

Do not infer repository identity from remote URLs or GitHub.

## Repository Preconditions

Use `IGitRepositoryStatusProvider`.

Before creating a branch:

1. The supplied repository directory must resolve to a Git repository.
2. `CurrentBranch` must be available.
3. `CurrentBranch` must equal `main` using ordinal comparison.
4. `HasUncommittedChanges` must be `false`.

If any precondition fails, the workflow must stop and must not call `IGitBranchCreator`.

Detached HEAD must therefore fail because no current branch is available.

## Branch Creation

Use the parsed task's `ExpectedBranch` exactly as supplied by the task document.

Do not:

- generate a branch name,
- normalize or rewrite it,
- prepend prefixes,
- fall back to another branch name.

Call `IGitBranchCreator` only after all task and repository validation succeeds.

The branch creator already owns validation of branch existence and actual Git branch creation.

## Operation Ordering

The workflow must guarantee this order:

1. Parse task.
2. Validate repository metadata.
3. Get repository status.
4. Validate repository preconditions.
5. Create branch.
6. Return result.

No mutation is allowed before all read-only validation steps succeed.

## Tests

Add unit tests using test doubles for the abstractions.

Tests must not require real Git repositories for workflow logic.

Cover at least:

1. Valid task starts successfully.
2. Parsed task ID/title are returned.
3. Repository root is returned.
4. Previous branch is `main`.
5. Created branch equals the task's `ExpectedBranch`.
6. Task file path is returned.
7. Review report path is returned.
8. Empty expected repository name is rejected.
9. Whitespace-only expected repository name is rejected.
10. Task repository metadata mismatch fails.
11. Non-repository status fails.
12. Detached HEAD fails.
13. Current branch other than `main` fails.
14. Dirty working tree fails.
15. Branch creator is not called when parsing/metadata validation fails.
16. Branch creator is not called when repository preconditions fail.
17. Branch creator is called exactly once for a successful start.
18. The exact `ExpectedBranch` from the parsed task is passed to the branch creator.
19. Cancellation is propagated to dependencies.

Existing integration-style Git tests from DEV-0002 through DEV-0005 and task parsing/discovery tests from DEV-0006 must continue to pass.

Use simple hand-written fakes/stubs unless the solution already contains a mocking framework. Do not add a mocking package only for this task.

## Out of Scope

Do not implement:

- task discovery/selection by ID,
- automatic choice of the next task,
- staging,
- commit,
- push,
- pull/fetch,
- GitHub integration,
- Pull Request creation,
- review report parsing,
- Codex execution,
- shell/process execution in Tasks,
- task status persistence,
- editing Developer Task files,
- CLI commands,
- workflow completion,
- branch cleanup,
- merge operations.

These belong to later Developer Tasks.

## Verification

Run for the complete solution:

```text
dotnet build
```

Required:

- 0 errors,
- no new warnings caused by DEV-0007.

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

DEV-0007 is complete when:

1. `DeveloperTaskStartResult` exists as an immutable Core model.
2. `IDeveloperTaskStarter` exists as a mockable asynchronous abstraction.
3. A concrete workflow implementation exists in `TrailTrainer.Developer.Tasks`.
4. The workflow composes existing parser, repository-status, and branch-creator abstractions.
5. No Git process execution is introduced in Tasks.
6. Repository metadata is validated before Git mutation.
7. Non-repository, detached HEAD, non-`main`, and dirty-tree states are rejected.
8. No branch is created when any validation fails.
9. The exact task `ExpectedBranch` is created on success.
10. Result data contains task identity, repository root, previous branch, created branch, task path, and review path.
11. Required unit tests cover success, validation failures, call ordering/side effects, and cancellation propagation.
12. Existing tests continue to pass.
13. `dotnet build` succeeds.
14. `dotnet test` succeeds.
15. `git diff --check` reports no whitespace errors.
16. No functionality outside the defined scope is implemented.
17. `docs/developer-reviews/REVIEW-0007.md` is created according to the completion protocol.

## Codex Completion Protocol

After implementation and verification:

1. Do **not** create a Git commit.
2. Do **not** push changes.
3. Do **not** modify this Developer Task.
4. Create:

   `docs/developer-reviews/REVIEW-0007.md`

5. The review report must contain:

```text
# REVIEW-0007 – Start Developer Task Workflow

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

## Deviations from DEV-0007

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

The review report is part of DEV-0007 and must be included in the later Pull Request.
