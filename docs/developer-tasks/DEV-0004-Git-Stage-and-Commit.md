# DEV-0004 – Git Stage and Commit

## Metadata

- Task ID: `DEV-0004`
- Repository: `TrailTrainer.Developer`
- Expected branch: `feature/dev-0004-git-stage-and-commit`
- Review report: `docs/developer-reviews/REVIEW-0004.md`
- Depends on: `DEV-0002`, `DEV-0003`

## Goal

Add focused local Git capabilities for staging repository changes and creating a commit.

This package builds on the repository inspection and shared Git process infrastructure introduced by DEV-0002 and DEV-0003.

The implementation is local only. It must not push changes or interact with GitHub.

## Codex Execution Instructions

Work this Developer Task completely.

- Follow `docs/architecture/README.md`.
- Implement only the scope defined in this task.
- Use the existing solution and project structure.
- Reuse the existing Git abstractions and `GitProcessRunner` where appropriate.
- Do not introduce a second Git process execution mechanism.
- Do not modify this Developer Task.
- Do not modify architecture documentation.
- Do not implement anything listed under **Out of Scope**.
- Do not create a Git commit for the implementation of DEV-0004.
- Do not push any changes.
- After implementation and verification, create the review report defined in **Codex Completion Protocol**.

If a requirement is unclear, do not invent additional functionality. Record the ambiguity in the review report and set the review status to `BLOCKED` if it prevents correct completion.

## Scope

Implement two focused capabilities:

1. Stage all current changes in a local Git working tree.
2. Create a local Git commit from the staged changes.

The two operations must remain separately callable.

DEV-0004 must not automatically combine staging and committing into a higher-level workflow.

## Core Models

Add immutable result models to `TrailTrainer.Developer.Core`.

### GitStageResult

Must expose at least:

- `RepositoryRoot`
- `HasStagedChanges`

### GitCommitResult

Must expose at least:

- `RepositoryRoot`
- `CommitSha`
- `CommitMessage`

`CommitSha` must represent the commit that was actually created.

Do not expose process, shell, stdout, stderr, or Git command details through Core models.

## Git Abstractions

Define mockable asynchronous abstractions in `TrailTrainer.Developer.Core`.

### IGitStager

The API must accept:

- a directory path identifying a Git repository or a directory inside it,
- an optional `CancellationToken`.

It must stage all current repository changes, including:

- modified tracked files,
- new/untracked files,
- deleted tracked files.

### IGitCommitter

The API must accept:

- a directory path identifying a Git repository or a directory inside it,
- a commit message,
- an optional `CancellationToken`.

It must create a commit from the changes that are already staged.

The committer must not stage files itself.

## Git Implementation

Implement the abstractions in `TrailTrainer.Developer.Git`.

Use the existing repository-status capability to resolve and validate the repository where sensible.

Use the existing shared Git process infrastructure from DEV-0003.

Git process execution must:

- run without a shell,
- use `ProcessStartInfo.ArgumentList` through the existing runner,
- capture standard output and standard error,
- avoid interactive prompts,
- respect cancellation,
- handle paths and commit messages safely.

Do not make application decisions by parsing localized Git error text.

## Staging Behavior

The staging implementation must stage all current working-tree changes.

A Git operation equivalent in behavior to:

```text
git add --all
```

is appropriate.

After staging, determine whether staged changes exist using machine-readable Git behavior.

`HasStagedChanges` must accurately report whether the index contains changes relative to `HEAD`.

A clean repository must not be treated as an exceptional failure. It must return a successful `GitStageResult` with:

```text
HasStagedChanges == false
```

A non-repository directory must be rejected.

## Commit Behavior

The commit implementation must create a commit only from already staged changes.

Requirements:

1. Reject a null, empty, or whitespace-only commit message.
2. Reject a non-repository directory.
3. If no staged changes exist, fail before attempting to create an empty commit.
4. Do not automatically stage unstaged or untracked changes.
5. Create the commit using the supplied commit message.
6. Return the resulting commit SHA.
7. Return the supplied commit message in the result.

The implementation must not create empty commits.

Unstaged changes may remain in the working tree after the commit and must not be silently included.

## Git Identity

Production code must not modify Git user identity.

The commit operation may rely on the repository/user Git configuration normally available to Git.

Tests that create commits must configure repository-local test identity and must not rely on the user's global Git configuration.

## Tests

Add automated tests covering at least the following scenarios.

### Staging

1. A modified tracked file is staged.
2. An untracked file is staged.
3. A deleted tracked file is staged.
4. A clean repository returns `HasStagedChanges == false`.
5. Calling the stager from a nested repository directory works.
6. Calling the stager for a non-repository directory fails.

### Commit

7. Already staged changes can be committed.
8. The returned commit SHA identifies the newly created commit.
9. The supplied commit message is used by the created commit.
10. An empty commit message is rejected.
11. A whitespace-only commit message is rejected.
12. A commit attempt with no staged changes fails.
13. The committer does not automatically include an untracked unstaged file.
14. Calling the committer from a nested repository directory works.
15. Calling the committer for a non-repository directory fails.

Existing DEV-0002 and DEV-0003 tests must continue to pass.

Tests must:

- use isolated temporary Git repositories,
- clean up temporary resources,
- not depend on the current `TrailTrainer.Developer` repository,
- not depend on global Git identity/configuration,
- reuse the existing temporary Git test infrastructure where sensible.

Small test-infrastructure extensions are allowed when directly needed for DEV-0004.

Avoid unrelated refactoring.

## Out of Scope

Do not implement:

- automatic stage-and-commit workflow orchestration,
- push or pull,
- remote branch operations,
- GitHub integration,
- Pull Request creation,
- Developer Task parsing,
- automatic commit-message generation,
- automatic branch naming,
- CLI commands,
- amend,
- rebase,
- merge,
- tags,
- commit signing,
- Git credential management.

These belong to later Developer Tasks.

## Verification

Run verification for the complete solution:

```text
dotnet build
```

The build must complete successfully with:

- 0 errors,
- no new warnings caused by DEV-0004.

Then run:

```text
dotnet test
```

All tests must pass.

Also run:

```text
git diff --check
```

There must be no whitespace errors. Platform line-ending notices alone are not considered whitespace errors.

## Acceptance Criteria

DEV-0004 is complete when:

1. `GitStageResult` exists as an immutable Core model.
2. `GitCommitResult` exists as an immutable Core model.
3. A mockable asynchronous `IGitStager` abstraction exists.
4. A mockable asynchronous `IGitCommitter` abstraction exists.
5. Concrete local implementations exist in `TrailTrainer.Developer.Git`.
6. All modified, new and deleted working-tree files can be staged.
7. A clean repository returns `HasStagedChanges == false`.
8. A commit is created only from already staged changes.
9. No-staged-change commit attempts fail without creating an empty commit.
10. The created commit SHA is returned.
11. The supplied commit message is used and returned.
12. Nested repository directories are supported.
13. Non-repository directories are rejected.
14. Git execution reuses the existing shared runner and does not invoke a shell.
15. No application logic depends on localized Git error messages.
16. Automated tests cover the required scenarios.
17. Existing tests continue to pass.
18. `dotnet build` succeeds.
19. `dotnet test` succeeds.
20. `git diff --check` reports no whitespace errors.
21. No functionality outside the defined scope is implemented.
22. `docs/developer-reviews/REVIEW-0004.md` is created according to the completion protocol.

## Codex Completion Protocol

After implementation and verification:

1. Do **not** create a Git commit.
2. Do **not** push any changes.
3. Do **not** modify this Developer Task file.
4. Create:

   `docs/developer-reviews/REVIEW-0004.md`

5. The review report must contain these sections:

```text
# REVIEW-0004 – Git Stage and Commit

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

## Deviations from DEV-0004

## Open Issues / Known Limitations

## Commit and Push
No commit created.
No push performed.
```

6. Set the status to `READY FOR REVIEW` only when:
   - all acceptance criteria are satisfied,
   - `dotnet build` succeeds,
   - `dotnet test` succeeds,
   - `git diff --check` has no whitespace errors,
   - there are no unresolved implementation blockers.

7. Otherwise set the status to `BLOCKED` and clearly document the reason.

8. In the verification section record:
   - build success/failure,
   - warning count,
   - error count,
   - tests passed,
   - tests failed,
   - tests skipped,
   - result of `git diff --check`.

9. List all files created, modified, or deleted by DEV-0004.

10. Record any deviation from this task explicitly. If there are none, write `None`.

11. Record any known limitations or open issues explicitly. If there are none, write `None`.

The review report is part of the DEV-0004 implementation and must be included in the later Pull Request.
