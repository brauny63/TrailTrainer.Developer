# DEV-0005 – Git Push

## Metadata

- Task ID: `DEV-0005`
- Repository: `TrailTrainer.Developer`
- Expected branch: `feature/dev-0005-git-push`
- Review report: `docs/developer-reviews/REVIEW-0005.md`
- Depends on: `DEV-0002`, `DEV-0003`, `DEV-0004`

## Goal

Add a focused local Git capability for pushing the current local branch to a configured remote repository.

The implementation must reuse the existing Git abstractions and shared Git process infrastructure.

This task covers Git transport only. It must not use the GitHub API and must not create Pull Requests.

## Codex Execution Instructions

Work this Developer Task completely.

- Follow `docs/architecture/README.md`.
- Implement only the scope defined in this task.
- Use the existing solution and project structure.
- Reuse the existing repository-status abstraction and `GitProcessRunner`.
- Do not introduce another Git process execution mechanism.
- Do not modify this Developer Task file.
- Do not modify architecture documentation.
- Do not implement anything listed under **Out of Scope**.
- Do not create a Git commit for the implementation of DEV-0005.
- Do not push the DEV-0005 implementation branch.
- After implementation and verification, create `docs/developer-reviews/REVIEW-0005.md`.

If a requirement is unclear, do not invent additional functionality. Record the ambiguity in the review report and set the review status to `BLOCKED` if it prevents correct completion.

## Scope

Implement functionality that can push the current local branch to a named Git remote.

The public operation must:

1. Resolve the repository from a supplied directory path.
2. Determine the current local branch.
3. Reject detached `HEAD`.
4. Verify that the requested remote exists.
5. Push the current branch to that remote.
6. Optionally establish upstream tracking when requested.
7. Return a strongly typed result describing the push.

No remote provider-specific logic is required.

## Core Model

Add an immutable result model to `TrailTrainer.Developer.Core`.

### GitPushResult

Must expose at least:

- `RepositoryRoot`
- `RemoteName`
- `BranchName`
- `SetUpstream`

Do not expose process, shell, stdout, stderr, credential, or provider-specific details through the Core model.

## Git Abstraction

Define a mockable asynchronous abstraction in `TrailTrainer.Developer.Core`.

### IGitPusher

The API must accept:

- a directory path identifying a Git repository or directory inside it,
- a remote name,
- a boolean indicating whether upstream tracking should be established,
- an optional `CancellationToken`.

The API must asynchronously return `GitPushResult`.

## Git Implementation

Implement the abstraction in `TrailTrainer.Developer.Git`.

Use the installed `git` executable through the existing shared `GitProcessRunner`.

The implementation must:

- use the repository-status capability to resolve repository root and current branch,
- reject non-repository directories,
- reject detached `HEAD`,
- reject null, empty, or whitespace-only remote names,
- verify the remote exists without parsing localized error text,
- push only the current branch,
- establish upstream tracking only when requested,
- avoid interactive prompts,
- respect cancellation,
- handle repository paths and remote names safely.

## Remote Verification

Determine whether the requested remote exists using machine-readable Git behavior.

Do not rely on localized stderr text to decide whether a remote exists.

A missing remote must result in a clear application exception.

## Push Behavior

When `setUpstream == false`, perform behavior equivalent to:

```text
git push <remote> <branch>
```

When `setUpstream == true`, perform behavior equivalent to:

```text
git push --set-upstream <remote> <branch>
```

Do not:

- push all branches,
- push tags,
- force push,
- delete remote refs,
- modify remote configuration,
- automatically choose a different remote.

## Credentials and Authentication

Production code must not manage credentials.

Authentication must be left to Git and the user's environment/configuration.

Do not:

- prompt for credentials interactively,
- store credentials,
- modify credential helpers,
- embed tokens,
- implement GitHub-specific authentication.

The existing non-interactive Git process behavior must be preserved.

## Tests

Automated tests must not depend on network access or GitHub.

Use isolated temporary Git repositories and a local bare Git repository as the remote.

Add tests covering at least:

1. The current branch can be pushed to a local bare remote.
2. The pushed remote branch exists after the operation.
3. The returned repository root is correct.
4. The returned remote name is correct.
5. The returned branch name is correct.
6. `setUpstream == true` establishes upstream tracking.
7. `setUpstream == false` does not require upstream tracking to be created.
8. Calling from a nested repository directory works.
9. A missing remote name is rejected.
10. An empty remote name is rejected.
11. A whitespace-only remote name is rejected.
12. A non-repository directory is rejected.
13. Detached `HEAD` is rejected.
14. Existing DEV-0002, DEV-0003, and DEV-0004 tests continue to pass.

Tests must:

- use isolated temporary directories,
- create a local bare Git repository for remote tests,
- not require internet/network access,
- not require GitHub,
- not depend on the current `TrailTrainer.Developer` repository,
- not depend on global Git identity/configuration,
- clean up temporary resources.

Extend existing temporary Git test infrastructure only as needed for DEV-0005.

Avoid unrelated refactoring.

## Out of Scope

Do not implement:

- pull or fetch,
- remote creation or modification,
- force push,
- push of all branches,
- tag push,
- GitHub API integration,
- Pull Request creation,
- Developer Task parsing,
- workflow orchestration,
- CLI commands,
- credential management,
- token storage,
- SSH key management,
- automatic remote selection,
- automatic branch naming,
- automatic stage or commit operations.

These belong to later Developer Tasks.

## Verification

Run verification for the complete solution:

```text
dotnet build
```

The build must complete successfully with:

- 0 errors,
- no new warnings caused by DEV-0005.

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

DEV-0005 is complete when:

1. `GitPushResult` exists as an immutable Core model.
2. A mockable asynchronous `IGitPusher` abstraction exists.
3. A concrete local implementation exists in `TrailTrainer.Developer.Git`.
4. Repository root and current branch are resolved using existing abstractions.
5. Non-repository directories are rejected.
6. Detached `HEAD` is rejected.
7. Empty and whitespace-only remote names are rejected.
8. Missing remotes are rejected using machine-readable Git behavior.
9. The current branch can be pushed to a named remote.
10. Upstream tracking can be established when requested.
11. No force push or broad push behavior is implemented.
12. Git execution reuses the shared runner and does not invoke a shell.
13. No application logic depends on localized Git error messages.
14. Tests use a local bare repository and require no network access.
15. Existing tests continue to pass.
16. `dotnet build` succeeds.
17. `dotnet test` succeeds.
18. `git diff --check` reports no whitespace errors.
19. No functionality outside the defined scope is implemented.
20. `docs/developer-reviews/REVIEW-0005.md` is created according to the completion protocol.

## Codex Completion Protocol

After implementation and verification:

1. Do **not** create a Git commit.
2. Do **not** push any changes.
3. Do **not** modify this Developer Task file.
4. Create:

   `docs/developer-reviews/REVIEW-0005.md`

5. The review report must contain these sections:

```text
# REVIEW-0005 – Git Push

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

## Deviations from DEV-0005

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

9. List all files created, modified, or deleted by DEV-0005.

10. Record any deviation from this task explicitly. If there are none, write `None`.

11. Record any known limitations or open issues explicitly. If there are none, write `None`.

The review report is part of the DEV-0005 implementation and must be included in the later Pull Request.
