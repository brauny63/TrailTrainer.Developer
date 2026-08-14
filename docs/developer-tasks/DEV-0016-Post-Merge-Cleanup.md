# DEV-0016 – Post-Merge Cleanup

## Metadata

- Task ID: `DEV-0016`
- Repository: `TrailTrainer.Developer`
- Expected branch: `feature/dev-0016-post-merge-cleanup`
- Review report: `docs/developer-reviews/REVIEW-0016.md`
- Depends on: `DEV-0002`, `DEV-0005`, `DEV-0015`

## Goal

Add a safe post-merge cleanup capability for a successfully merged Pull Request.

After a Pull Request has been confirmed as merged, the toolkit must be able to return the local repository to the configured base branch, update that branch from its remote, delete the merged local feature branch, and optionally delete the corresponding remote feature branch.

Cleanup must be conservative. It must refuse destructive branch operations when the repository state is unsafe or when the requested feature branch is a protected branch.

## Codex Execution Instructions

Work this Developer Task completely.

- Follow `docs/architecture/README.md`.
- Implement only the scope defined here.
- Reuse existing Git repository/status/process abstractions where appropriate.
- Keep provider-neutral cleanup contracts/models in `TrailTrainer.Developer.Core`.
- Put Git-specific cleanup implementation in `TrailTrainer.Developer.Git`.
- Do not add GitHub REST logic to the Git project.
- Do not duplicate existing repository-status or Git process infrastructure.
- Use the existing shell-free Git execution approach.
- Do not launch a shell, PowerShell, cmd.exe, bash, or `gh`.
- Do not modify this Developer Task or architecture documentation.
- Do not implement anything under **Out of Scope**.
- Do not create a Git commit for DEV-0016.
- Do not push the DEV-0016 implementation branch.
- After implementation and verification create `docs/developer-reviews/REVIEW-0016.md`.

If an ambiguity prevents correct completion, do not invent behavior. Document it and set the review status to `BLOCKED`.

## Scope

Implement a post-merge cleanup operation that accepts an explicitly confirmed merged Pull Request/feature-branch context and:

1. Validates repository state.
2. Refuses cleanup when uncommitted changes exist.
3. Switches to the configured base branch.
4. Updates the base branch from the configured remote using fast-forward-only semantics.
5. Deletes the merged local feature branch when it exists.
6. Optionally deletes the matching remote feature branch.
7. Returns a strongly typed cleanup result.
8. Supports cancellation.

The cleanup operation must not itself determine whether CI passed or perform the Pull Request merge. The caller supplies a merge result proving that the merge succeeded.

## Core Model

### PostMergeCleanupResult

Add an immutable result exposing at least:

- `RepositoryRoot`
- `BaseBranch`
- `FeatureBranch`
- `LocalBranchDeleted`
- `RemoteBranchDeleted`

The booleans indicate whether this invocation actually deleted the corresponding branch.

If a branch did not exist and no deletion was required, the corresponding value is `false`.

## Core Abstraction

### IPostMergeCleaner

Add a mockable asynchronous abstraction.

The operation must accept at least:

- repository directory path,
- `GitHubRepositoryIdentity repository`,
- Pull Request number,
- `PullRequestMergeResult mergeResult`,
- feature branch name,
- base branch name,
- remote name,
- `deleteRemoteBranch`,
- optional `CancellationToken`.

It returns `PostMergeCleanupResult`.

The abstraction must not expose process objects, shell details, command output DTOs, or GitHub HTTP details.

## Merge Confirmation

Cleanup is permitted only when the supplied `PullRequestMergeResult` proves a successful merge.

Before any mutating Git operation:

1. `mergeResult` must not be null.
2. `mergeResult.Merged` must be `true`.
3. `mergeResult.PullRequestNumber` must equal the supplied Pull Request number.
4. The successful merge result must satisfy its existing merge-SHA invariant.

If merge confirmation is invalid:

- perform no switch,
- perform no pull,
- perform no branch deletion,
- fail clearly.

Do not call GitHub to re-check merge state in DEV-0016.

## Input Validation

Before mutation:

- repository directory must be non-empty,
- repository identity must not be null,
- Pull Request number must be > 0,
- feature branch must be non-empty,
- base branch must be non-empty,
- remote name must be non-empty,
- feature branch and base branch must not be equal using ordinal comparison.

Reject unsafe input before mutation.

## Repository Validation

Use the existing repository-status abstraction to resolve the repository root and current state.

Requirements:

1. The supplied directory must be inside a Git repository.
2. The repository must have no uncommitted changes before cleanup begins.
3. Detached HEAD must be rejected.
4. Cleanup may be invoked while currently on the feature branch or another normal branch.
5. The resolved repository root must be returned.

Do not silently discard, stash, stage, commit, or reset user changes.

## Protected Branch Safety

Never delete the configured base branch.

At minimum, reject cleanup when:

- feature branch equals base branch.

Do not implement generalized branch-protection discovery in DEV-0016.

## Base Branch Switch

Switch to the configured base branch before deleting the feature branch.

Requirements:

- use Git directly without a shell,
- fail if the base branch does not exist locally,
- do not create the base branch automatically,
- do not use force checkout/switch,
- propagate failures.

If already on the base branch, do not require an unnecessary switch command.

## Base Branch Update

After being on the base branch, update it from the explicitly supplied remote.

Use fast-forward-only behavior equivalent to:

`git pull --ff-only <remote> <baseBranch>`

Requirements:

- no merge commit,
- no rebase,
- no force,
- no fallback strategy,
- propagate failure.

If update fails, do not continue to branch deletion.

## Local Feature Branch Deletion

After successful base-branch update:

1. Check whether the local feature branch exists using machine-readable Git behavior.
2. If it does not exist, continue and report `LocalBranchDeleted == false`.
3. If it exists, delete it using normal merged-branch deletion semantics equivalent to `git branch -d`.
4. Do not use forced deletion (`-D`).

If normal deletion fails because Git does not consider the branch merged, propagate the failure and do not force-delete it.

## Remote Feature Branch Deletion

Remote deletion is optional and controlled only by `deleteRemoteBranch`.

### When false

- do not issue any remote-delete command,
- return `RemoteBranchDeleted == false`.

### When true

After successful local cleanup:

1. Determine whether the remote feature branch exists using machine-readable Git behavior.
2. If it does not exist, return `RemoteBranchDeleted == false`.
3. If it exists, delete exactly that feature branch from the explicitly supplied remote.
4. Report `RemoteBranchDeleted == true` only when deletion succeeds.

Use a Git operation equivalent to:

`git push <remote> --delete <featureBranch>`

Do not delete any other remote refs.

## Remote Validation

The explicitly supplied remote must exist before the base update begins.

Use machine-readable Git behavior.

Do not infer a remote from branch tracking configuration.

Do not automatically substitute `origin`.

## Ordering

Required mutation order:

```text
Validate inputs
      ↓
Confirm successful merge result
      ↓
Resolve repository + ensure clean
      ↓
Validate remote/base/feature context
      ↓
Switch to base branch if necessary
      ↓
Pull base branch --ff-only
      ↓
Delete local feature branch if present
      ↓
Optionally delete remote feature branch if present
      ↓
Return result
```

Do not delete either feature branch before the base branch has been successfully updated.

## Failure Behavior

The operation must short-circuit.

Examples:

- invalid merge confirmation → no mutation,
- dirty working tree → no mutation,
- detached HEAD → no mutation,
- missing remote → no switch/pull/delete,
- missing local base branch → no pull/delete,
- switch failure → no pull/delete,
- pull failure → no branch deletion,
- local delete failure → no remote delete,
- remote delete failure → propagate failure.

Do not implement rollback of already completed Git operations.

## Cancellation

Propagate the same `CancellationToken` through asynchronous dependencies and Git execution.

Cancellation must stop subsequent operations.

Do not translate cancellation into a normal validation result.

## Tests

Tests must use isolated temporary Git repositories and local bare repositories where remote behavior is needed.

They must not depend on:

- the current TrailTrainer.Developer repository,
- global Git identity/configuration,
- GitHub,
- network access.

Cover at least:

### Input / merge confirmation

1. Null/empty repository directory rejected.
2. Invalid PR number rejected.
3. Empty feature branch rejected.
4. Empty base branch rejected.
5. Empty remote rejected.
6. Feature branch equal to base branch rejected.
7. Null merge result rejected.
8. `Merged == false` rejected without mutation.
9. Merge-result PR number mismatch rejected without mutation.

### Repository safety

10. Non-repository rejected.
11. Dirty repository rejected without mutation.
12. Untracked file counts as dirty.
13. Detached HEAD rejected.
14. Repository subdirectory is supported.
15. Resolved repository root returned.

### Base branch / remote

16. Missing remote rejected before mutation.
17. Missing local base branch rejected before mutation.
18. Cleanup switches from feature branch to base.
19. Already-on-base avoids unnecessary switch behavior where testable.
20. Base update uses the supplied remote.
21. Base update uses the supplied base branch.
22. Base update is fast-forward-only.
23. Pull/update failure prevents local deletion.

### Local branch cleanup

24. Existing merged local feature branch is deleted.
25. Missing local feature branch is tolerated.
26. Missing local branch reports `LocalBranchDeleted == false`.
27. Successful deletion reports `LocalBranchDeleted == true`.
28. Unmerged local branch is not force-deleted.
29. Local deletion failure prevents remote deletion.

### Remote branch cleanup

30. `deleteRemoteBranch == false` performs no remote deletion.
31. Existing remote feature branch is deleted when requested.
32. Missing remote feature branch is tolerated.
33. Missing remote feature branch reports `RemoteBranchDeleted == false`.
34. Successful remote deletion reports `RemoteBranchDeleted == true`.
35. Only the supplied feature branch is deleted.
36. Remote deletion uses the supplied remote.
37. Remote deletion failure is propagated.

### Ordering / cancellation

38. Base update occurs before local deletion.
39. Local deletion occurs before remote deletion.
40. Cancellation prevents subsequent operations.

### Regression

41. Existing DEV-0002 through DEV-0015 tests continue to pass.

Avoid unrelated refactoring.

## Out of Scope

Do not implement:

- Pull Request merge,
- CI/status evaluation,
- GitHub merge-state lookup,
- polling/waiting,
- retries,
- rollback,
- force branch deletion,
- force push,
- reset/clean/stash,
- automatic conflict resolution,
- rebase,
- arbitrary branch cleanup,
- pruning all remotes,
- default-branch discovery,
- GitHub branch-protection discovery,
- automatic next Developer Task selection,
- Codex execution,
- workflow scheduling,
- CLI cleanup command.

These belong to later Developer Tasks.

## Verification

Run:

```text
dotnet build
```

Required:

- 0 errors,
- no new warnings caused by DEV-0016.

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

DEV-0016 is complete when:

1. `PostMergeCleanupResult` exists as an immutable Core model.
2. `IPostMergeCleaner` exists as a mockable asynchronous Core abstraction.
3. Concrete Git cleanup implementation exists in `TrailTrainer.Developer.Git`.
4. Successful merge confirmation is required before mutation.
5. Merge-result PR number must match the requested PR.
6. Repository must be valid, attached, and clean.
7. Feature and base branches cannot be equal.
8. Explicit remote must exist.
9. Local base branch must exist.
10. Cleanup switches to the base branch when needed.
11. Base branch update uses fast-forward-only semantics.
12. Failed update prevents branch deletion.
13. Existing local feature branch is deleted without force.
14. Missing local feature branch is tolerated.
15. Remote deletion is explicitly optional.
16. Existing remote feature branch is deleted only when requested.
17. Missing remote feature branch is tolerated.
18. Local deletion failure prevents remote deletion.
19. Cancellation is supported.
20. No shell, GitHub HTTP, force deletion, reset, stash, retry, or rollback is introduced.
21. Tests use isolated repositories and no public network.
22. Existing tests continue to pass.
23. `dotnet build` succeeds.
24. `dotnet test` succeeds.
25. `git diff --check` succeeds.
26. No out-of-scope functionality is implemented.
27. `docs/developer-reviews/REVIEW-0016.md` is created.

## Codex Completion Protocol

After implementation and verification:

1. Do **not** create a Git commit.
2. Do **not** push changes.
3. Do **not** modify this Developer Task.
4. Create:

   `docs/developer-reviews/REVIEW-0016.md`

5. The review report must contain:

```text
# REVIEW-0016 – Post-Merge Cleanup

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

## Deviations from DEV-0016

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

The review report is part of DEV-0016 and must be included in the later Pull Request.
