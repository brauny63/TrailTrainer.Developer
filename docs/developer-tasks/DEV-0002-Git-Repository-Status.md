# DEV-0002 – Git Repository Status

## Goal

Implement the first Git capability of `TrailTrainer.Developer`: inspect a local directory and return a small, strongly typed description of its Git repository status.

The implementation must follow the architecture defined in `docs/architecture/README.md` and keep process execution isolated from the domain model.

## Scope

Implement functionality that can determine for a supplied directory:

- whether the directory is inside a Git working tree,
- the repository root directory when it is a Git repository,
- the current branch name when available,
- whether the working tree contains uncommitted changes.

No remote GitHub access is required.

## Core Model

Add a small immutable result model to `TrailTrainer.Developer.Core` representing repository status.

The model must expose at least:

- `IsRepository`
- `RepositoryRoot`
- `CurrentBranch`
- `HasUncommittedChanges`

For a directory that is not a Git repository:

- `IsRepository` must be `false`,
- repository-specific values must not contain invented data.

Use nullable values where a value is genuinely unavailable.

## Git Abstraction

Define an abstraction for obtaining repository status without exposing shell/process details to callers.

The public API should accept a directory path and asynchronously return the repository status model.

The abstraction must be suitable for mocking in future workflow tests.

## Git Implementation

Implement the abstraction in `TrailTrainer.Developer.Git` using the installed `git` executable.

Git process execution must:

- run without a shell,
- set the supplied directory as the working directory where appropriate,
- capture standard output and standard error,
- respect cancellation,
- avoid interactive prompts,
- handle paths containing spaces.

Use Git commands appropriate for determining repository root, branch and working-tree status.

A normal non-repository directory must be represented as `IsRepository == false`; it must not be treated as an exceptional application failure.

Unexpected failures, such as Git not being executable, may surface as exceptions with useful diagnostic information.

## Tests

Add automated tests covering at least:

1. A temporary initialized Git repository is recognized as a repository.
2. The repository root is returned correctly.
3. The current branch is returned after creating/checking out a known branch.
4. A clean repository reports `HasUncommittedChanges == false`.
5. Creating an untracked file causes `HasUncommittedChanges == true`.
6. A normal temporary directory that is not a Git repository reports `IsRepository == false`.

Tests must create their own temporary directories/repositories and clean them up afterwards.

Tests must not depend on the developer's existing TrailTrainer repository state.

If a test creates Git commits, configure repository-local test identity rather than relying on the user's global Git configuration.

## Out of Scope

Do not implement:

- branch creation,
- commits,
- push or pull,
- remote repository inspection,
- GitHub API integration,
- Developer Task parsing,
- workflow orchestration,
- CLI commands for repository status.

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

DEV-0002 is complete when:

1. A strongly typed repository-status model exists in `TrailTrainer.Developer.Core`.
2. A mockable abstraction exists for obtaining Git repository status.
3. `TrailTrainer.Developer.Git` provides a concrete local Git implementation.
4. Repository detection, root path, current branch and dirty/clean state are implemented.
5. Non-repository directories return a valid `IsRepository == false` result.
6. The implementation does not invoke Git through a shell.
7. Automated tests cover the required scenarios using isolated temporary repositories.
8. `dotnet build` succeeds.
9. `dotnet test` succeeds.
10. No functionality outside the defined scope is implemented.
