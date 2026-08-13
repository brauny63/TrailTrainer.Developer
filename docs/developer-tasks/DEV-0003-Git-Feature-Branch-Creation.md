# DEV-0003 – Git Feature Branch Creation

## Goal

Add a focused Git capability to `TrailTrainer.Developer` for creating and switching to a local feature branch in an existing Git repository.

The implementation must build on the existing Git abstractions and repository-status functionality introduced by DEV-0002.

## Scope

Implement functionality that can:

- create a new local Git branch from the repository's current `HEAD`,
- switch the working tree to the newly created branch,
- return a strongly typed result describing the created branch.

The operation is local only.

## Core Model

Add an immutable result model to `TrailTrainer.Developer.Core` representing the result of branch creation.

The model must expose at least:

- `RepositoryRoot`
- `BranchName`

Do not expose process or shell details through the Core model.

## Git Abstraction

Define a mockable abstraction in `TrailTrainer.Developer.Core` for creating a local branch.

The public API should accept:

- a directory path identifying a Git repository or a directory inside it,
- the desired branch name,
- an optional `CancellationToken`.

The API must be asynchronous.

## Git Implementation

Implement the abstraction in `TrailTrainer.Developer.Git`.

The implementation must:

1. Verify that the supplied directory belongs to a Git repository.
2. Reject the operation when the directory is not inside a Git repository.
3. Reject an empty or whitespace-only branch name.
4. Create the branch from the current `HEAD`.
5. Switch the working tree to the new branch.
6. Return the repository root and resulting branch name.

Use the installed `git` executable.

Git process execution must:

- run without a shell,
- use `ProcessStartInfo.ArgumentList`,
- capture standard output and standard error,
- avoid interactive prompts,
- respect cancellation,
- handle paths and branch names safely.

Use a Git command suitable for creating and switching to a new branch in one operation where possible.

## Existing Branch

If the requested branch already exists, the operation must fail.

Do not silently switch to an existing branch.

The exception must contain useful diagnostic information without depending on localized Git error text for application logic.

## Dirty Working Tree

DEV-0003 does not require a clean working tree before branch creation.

Git's normal behavior may determine whether the switch is possible.

Do not add additional workflow policy beyond Git's own constraints.

## Tests

Add automated tests covering at least:

1. A new branch can be created in a temporary Git repository.
2. After the operation, the repository's current branch is the newly created branch.
3. The returned repository root is correct.
4. A branch name containing a slash such as `feature/test-branch` is supported.
5. Creating a branch that already exists fails.
6. Calling the operation for a normal non-repository directory fails.
7. Empty and whitespace-only branch names are rejected.

Tests must:

- create isolated temporary repositories,
- clean up their temporary files,
- not depend on the current TrailTrainer.Developer repository,
- not depend on the user's global Git identity or Git configuration.

Reuse test infrastructure where sensible, but do not introduce unrelated refactoring.

## Out of Scope

Do not implement:

- commits,
- staging,
- push or pull,
- remote branch creation,
- GitHub integration,
- Pull Request creation,
- Developer Task parsing,
- workflow orchestration,
- CLI commands,
- automatic branch naming from Developer Task IDs.

These belong to later Developer Tasks.

## Verification

The complete solution must build successfully:

```text
dotnet build
```

All tests must pass:

```text
dotnet test
```

## Acceptance Criteria

DEV-0003 is complete when:

1. A strongly typed branch-creation result model exists in `TrailTrainer.Developer.Core`.
2. A mockable asynchronous abstraction exists for local branch creation.
3. `TrailTrainer.Developer.Git` provides the concrete implementation.
4. A new local branch is created from current `HEAD`.
5. The working tree is switched to the new branch.
6. Existing branch names cause a failure instead of silently switching branches.
7. Non-repository directories are rejected.
8. Empty and whitespace-only branch names are rejected.
9. Git is not invoked through a shell.
10. Automated tests cover the required scenarios using isolated temporary repositories.
11. `dotnet build` succeeds.
12. `dotnet test` succeeds.
13. No functionality outside the defined scope is implemented.
