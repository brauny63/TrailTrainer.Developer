# DEV-0009 – Developer Task CLI

## Metadata

- Task ID: `DEV-0009`
- Repository: `TrailTrainer.Developer`
- Expected branch: `feature/dev-0009-developer-task-cli`
- Review report: `docs/developer-reviews/REVIEW-0009.md`
- Depends on: `DEV-0006`, `DEV-0007`, `DEV-0008`

## Goal

Expose the existing Developer Task capabilities through a small command-line interface.

The CLI must allow a user to list Developer Tasks, inspect one task, start a task workflow, and complete a task workflow by delegating to the existing Core abstractions and Tasks implementations.

The CLI is an adapter only. Business rules and Git workflow logic must remain outside the CLI project.

## Codex Execution Instructions

Work this Developer Task completely.

- Follow `docs/architecture/README.md`.
- Implement only the scope defined here.
- Reuse the existing abstractions and workflows from DEV-0006 through DEV-0008.
- Keep orchestration/business rules out of `TrailTrainer.Developer.CLI`.
- Do not introduce direct Git process execution in the CLI.
- Do not add GitHub API integration.
- Do not modify this Developer Task.
- Do not modify architecture documentation.
- Do not implement anything under **Out of Scope**.
- Do not create a Git commit for DEV-0009.
- Do not push the DEV-0009 implementation branch.
- After implementation and verification create `docs/developer-reviews/REVIEW-0009.md`.

If an ambiguity prevents correct completion, do not invent additional behavior. Document it and set the review status to `BLOCKED`.

## Scope

Implement these CLI commands:

```text
trailtrainer-developer tasks list
trailtrainer-developer tasks show <task>
trailtrainer-developer tasks start <task>
trailtrainer-developer tasks complete <task>
```

The existing project executable name does not need to be changed to literally `trailtrainer-developer`; the command examples describe the logical CLI syntax.

The CLI must support invocation from the repository root or from a directory inside the repository.

## Task Argument

`<task>` must accept either:

```text
DEV-0009
```

or a Developer Task filename such as:

```text
DEV-0009-Developer-Task-CLI.md
```

Task resolution must use `IDeveloperTaskDiscovery`.

Matching by canonical task ID must use `DeveloperTaskId`.

If no matching task exists, fail clearly.

If more than one discovered task resolves to the requested identity, fail clearly rather than choosing arbitrarily.

Do not implement fuzzy matching.

## Repository Resolution

The CLI may use `IGitRepositoryStatusProvider` to resolve the repository root from the current working directory.

For every command:

1. Resolve the current working directory.
2. Require it to be inside a Git repository.
3. Use the resolved repository root for task discovery/workflow calls.
4. Derive the expected repository name from the repository root directory name.

Do not infer repository identity from Git remotes or GitHub.

## Command: tasks list

Use `IDeveloperTaskDiscovery`.

Output one line per task, ordered as returned by discovery.

Each line must contain at least:

```text
DEV-0001  DEV-0001-Bootstrap-Solution.md
```

The command must not parse every task document.

If no tasks exist, succeed and print a clear message indicating that no Developer Tasks were found.

## Command: tasks show

Resolve the requested task, then use `IDeveloperTaskParser`.

Print at least:

- Task ID
- Title
- Repository
- Expected branch
- Review report path
- Task file path

This command must not mutate Git state.

## Command: tasks start

Resolve the requested task and call `IDeveloperTaskStarter`.

Print at least:

- Task ID and title
- Repository root
- Previous branch
- Created branch
- Task file path
- Review report path

Do not duplicate start-workflow validation in the CLI.

## Command: tasks complete

Resolve the requested task and call `IDeveloperTaskCompleter`.

Required options:

```text
--message <commit-message>
```

Optional options:

```text
--remote <remote-name>
--set-upstream
```

Defaults:

- remote: `origin`
- set-upstream: `true`

Examples:

```text
trailtrainer-developer tasks complete DEV-0009 --message "feat: implement developer task CLI"
trailtrainer-developer tasks complete DEV-0009 --message "feat: implement developer task CLI" --remote origin
```

The CLI must pass the supplied/default values to `IDeveloperTaskCompleter` without rewriting them.

Do not duplicate completion-workflow validation in the CLI.

## Argument Parsing

Keep argument parsing intentionally small.

A third-party command-line framework is not required for DEV-0009.

A small internal parser is acceptable.

Requirements:

- unknown commands fail clearly,
- missing required arguments fail clearly,
- unknown options fail clearly,
- an option requiring a value must reject a missing value,
- `--message` is required for `tasks complete`,
- duplicate options must be rejected,
- command names and option names use ordinal case-insensitive comparison.

Do not implement abbreviated command names.

## Exit Codes

Use:

- `0` for successful command execution,
- non-zero for invalid usage or operation failure.

The CLI must not print a success message after a failed operation.

Errors should be written to standard error.

Normal command output should be written to standard output.

Do not expose stack traces for ordinary user/input/workflow errors.

## Testable CLI Design

Refactor the current minimal `Program.cs` as needed so command execution can be tested without launching a child process.

Create an internal or public CLI application/runner abstraction that accepts at least:

- argument array,
- current working directory,
- output writer,
- error writer,
- optional `CancellationToken`.

Dependencies must be injectable so tests can use fakes/stubs.

`Program.cs` should remain a thin composition root.

Do not add a mocking framework only for this task.

## Tests

Add automated tests covering at least:

### Task resolution

1. Resolve by canonical task ID.
2. Resolve by exact task filename.
3. Missing task fails.
4. Ambiguous identity fails.
5. No fuzzy matching.

### list

6. Lists discovered tasks.
7. Preserves discovery order.
8. Empty discovery succeeds with a clear message.
9. Does not parse every task.

### show

10. Shows required parsed fields.
11. Does not invoke start or complete workflows.

### start

12. Calls `IDeveloperTaskStarter` exactly once.
13. Passes resolved task path, repository root, and derived repository name.
14. Prints required result fields.

### complete

15. Requires `--message`.
16. Uses default remote `origin`.
17. Uses default `setUpstream == true`.
18. Passes an explicit remote unchanged.
19. Passes the exact commit message unchanged.
20. Calls `IDeveloperTaskCompleter` exactly once.
21. Prints commit SHA and pushed branch information on success.

### usage/errors

22. Unknown command returns non-zero.
23. Unknown option returns non-zero.
24. Missing option value returns non-zero.
25. Duplicate options return non-zero.
26. Workflow failure returns non-zero and writes to stderr.
27. No success output is emitted after workflow failure.
28. Cancellation is propagated.

Existing DEV-0002 through DEV-0008 tests must continue to pass.

Tests must not require GitHub or network access.

CLI unit tests should use injected fakes and in-memory writers rather than real repositories where practical.

## Out of Scope

Do not implement:

- GitHub API integration,
- Pull Request creation,
- Pull Request merge,
- review report parsing,
- Codex execution,
- automatic task implementation,
- automatic next-task selection,
- task status persistence,
- branch cleanup,
- switching back to `main`,
- pull/fetch/rebase,
- configuration files,
- interactive menus,
- prompts,
- shell/process execution in CLI,
- fuzzy task search,
- task document editing.

These belong to later Developer Tasks.

## Verification

Run for the complete solution:

```text
dotnet build
```

Required:

- 0 errors,
- no new warnings caused by DEV-0009.

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

DEV-0009 is complete when:

1. The CLI supports `tasks list`.
2. The CLI supports `tasks show <task>`.
3. The CLI supports `tasks start <task>`.
4. The CLI supports `tasks complete <task>`.
5. Task arguments resolve by canonical ID or exact filename.
6. Repository root is resolved from the current working directory.
7. Repository name is derived from the repository-root directory name.
8. CLI delegates discovery, parsing, start, and completion to existing abstractions.
9. No Git process/workflow business logic is duplicated in CLI.
10. `tasks complete` requires an exact commit message.
11. Remote defaults to `origin`.
12. `setUpstream` defaults to `true`.
13. Invalid usage and operation failures return non-zero.
14. Errors go to stderr and ordinary output goes to stdout.
15. `Program.cs` remains a thin composition root.
16. Command execution is unit-testable without launching a child process.
17. Required tests cover resolution, commands, options, errors, failure behavior, and cancellation.
18. Existing tests continue to pass.
19. `dotnet build` succeeds.
20. `dotnet test` succeeds.
21. `git diff --check` reports no whitespace errors.
22. No out-of-scope functionality is implemented.
23. `docs/developer-reviews/REVIEW-0009.md` is created according to the completion protocol.

## Codex Completion Protocol

After implementation and verification:

1. Do **not** create a Git commit.
2. Do **not** push changes.
3. Do **not** modify this Developer Task.
4. Create:

   `docs/developer-reviews/REVIEW-0009.md`

5. The review report must contain:

```text
# REVIEW-0009 – Developer Task CLI

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

## Deviations from DEV-0009

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

The review report is part of DEV-0009 and must be included in the later Pull Request.
