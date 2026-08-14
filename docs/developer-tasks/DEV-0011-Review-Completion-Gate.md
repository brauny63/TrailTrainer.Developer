# DEV-0011 – Review Completion Gate

## Metadata

- Task ID: `DEV-0011`
- Repository: `TrailTrainer.Developer`
- Expected branch: `feature/dev-0011-review-completion-gate`
- Review report: `docs/developer-reviews/REVIEW-0011.md`
- Depends on: `DEV-0008`, `DEV-0010`

## Goal

Add a review gate in front of the existing Developer Task completion workflow.

Before staging, committing, or pushing an implemented Developer Task, the toolkit must load the review report declared by the task, parse it, validate it, and allow completion only when the review is valid.

The existing completion workflow remains responsible for Git stage, commit, and push. The gate only decides whether that workflow may be entered.

## Codex Execution Instructions

Work this Developer Task completely.

- Follow `docs/architecture/README.md`.
- Implement only the scope defined here.
- Reuse the existing task parser, review parser, review validator, and task completer abstractions.
- Keep reusable contracts/models in `TrailTrainer.Developer.Core`.
- Put concrete orchestration in `TrailTrainer.Developer.Tasks`.
- Do not introduce direct Git/process execution in Tasks.
- Do not duplicate review-validation rules from DEV-0010.
- Do not duplicate completion Git workflow rules from DEV-0008.
- Do not modify this Developer Task or architecture documentation.
- Do not implement anything under **Out of Scope**.
- Do not commit or push the DEV-0011 implementation.
- After verification create `docs/developer-reviews/REVIEW-0011.md`.

If ambiguity prevents correct completion, document it and set the review report to `BLOCKED`.

## Scope

Implement a gated completion workflow that:

1. Parses the selected Developer Task.
2. Resolves the review-report file from the task's `ReviewReportPath`.
3. Parses the review report.
4. Validates the review against the parsed task.
5. Stops when the review validation result is invalid.
6. Calls the existing `IDeveloperTaskCompleter` only when the review is valid.
7. Returns a strongly typed result containing the review validation and completion result.

Warnings from review validation must not block completion.

## Core Model

### DeveloperTaskGatedCompletionResult

Add an immutable model exposing at least:

- `TaskId`
- `ReviewValidation`
- `Completion`

Where:

- `ReviewValidation` is `DeveloperReviewValidationResult`.
- `Completion` is `DeveloperTaskCompletionResult`.

A successful gated completion always contains both.

Do not duplicate fields already represented by these nested result models unless required for a clear API.

## Core Abstraction

### IDeveloperTaskGatedCompleter

Add a mockable asynchronous abstraction.

The API must accept:

- Developer Task file path,
- repository directory path,
- expected repository name,
- commit message,
- remote name,
- `setUpstream`,
- optional `CancellationToken`.

It returns `DeveloperTaskGatedCompletionResult`.

## Implementation

Implement `IDeveloperTaskGatedCompleter` in `TrailTrainer.Developer.Tasks`.

Expected injected dependencies:

- `IDeveloperTaskParser`
- `IDeveloperReviewParser`
- `IDeveloperReviewValidator`
- `IDeveloperTaskCompleter`

The implementation must orchestrate abstractions only.

## Review Path Resolution

The parsed task contains a repository-relative `ReviewReportPath`.

Resolve it against the repository root represented by the supplied repository directory.

Because the supplied directory may be a nested repository directory, do not assume it is the repository root merely from its path.

Use the task file location and the established repository/task layout to resolve the review file without introducing Git process execution into this workflow.

The implementation must ensure that the resolved review file remains within the repository tree.

Path traversal outside the repository must be rejected.

If the existing abstractions do not provide enough information to determine the repository root safely before completion, introduce the smallest read-only abstraction reuse necessary, such as `IGitRepositoryStatusProvider`. Do not add new Git process logic.

## Operation Ordering

The workflow must guarantee:

1. Parse task.
2. Resolve review path.
3. Parse review.
4. Validate review.
5. If invalid: stop.
6. If valid: call existing task completer.
7. Return gated completion result.

No stage, commit, or push may occur before review validation succeeds.

## Invalid Review Behavior

When `DeveloperReviewValidationResult.IsValid == false`:

- do not call `IDeveloperTaskCompleter`,
- throw a clear workflow exception or another existing project-consistent operation exception,
- include the validation errors in the diagnostic information.

Do not silently ignore validation errors.

Warnings alone do not block completion.

## Delegation

When review validation succeeds, pass these values unchanged to `IDeveloperTaskCompleter`:

- task file path,
- repository directory path,
- expected repository name,
- commit message,
- remote name,
- `setUpstream`,
- cancellation token.

Do not reimplement the completer's input validation, repository preconditions, staging, commit, or push logic.

## Tests

Add unit tests using injected fakes/stubs. Workflow tests must not require real Git repositories.

Cover at least:

1. Valid review allows completion.
2. Result contains task ID.
3. Result contains the exact review-validation result.
4. Result contains the exact completion result.
5. Task parser is called before review parser.
6. Review parser is called before validator.
7. Validator is called before completer.
8. Invalid review prevents completer invocation.
9. All review validation errors appear in the failure diagnostic.
10. Review warnings do not block completion.
11. Exact task file path is passed to the completer.
12. Exact repository directory is passed to the completer.
13. Exact expected repository name is passed unchanged.
14. Exact commit message is passed unchanged.
15. Exact remote name is passed unchanged.
16. Exact `setUpstream` value is passed unchanged.
17. Cancellation token is propagated to every dependency.
18. Task parser failure prevents all later operations.
19. Review parser failure prevents validation and completion.
20. Review validator failure prevents completion.
21. Completer failure is propagated.
22. Review report path is resolved from task metadata.
23. Nested repository-directory invocation resolves the correct review report.
24. Repository-relative review paths are accepted.
25. Path traversal outside the repository is rejected before review parsing.
26. Existing DEV-0002 through DEV-0010 tests continue to pass.

If `IGitRepositoryStatusProvider` is reused for safe repository-root resolution, add tests proving it is read-only in this workflow and that failure/non-repository state prevents later operations.

Avoid unrelated refactoring.

## Out of Scope

Do not implement:

- new review validation rules,
- Git staging/commit/push logic,
- GitHub API integration,
- Pull Request creation,
- Pull Request merge,
- Pull Request review submission,
- Codex execution,
- review report generation or editing,
- automatic task discovery/selection,
- automatic next-task selection,
- branch cleanup,
- switching back to `main`,
- pull/fetch/rebase,
- CLI commands for the gate,
- configuration files,
- semantic approval of warnings,
- process/shell execution in Tasks.

## Verification

Run:

```text
dotnet build
```

Required:

- 0 errors,
- no new warnings caused by DEV-0011.

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

DEV-0011 is complete when:

1. `DeveloperTaskGatedCompletionResult` exists as an immutable Core model.
2. `IDeveloperTaskGatedCompleter` exists as a mockable asynchronous abstraction.
3. Concrete gated completion orchestration exists in Tasks.
4. Existing task parser, review parser, validator, and completer abstractions are reused.
5. Review report path is resolved safely from task metadata.
6. Path traversal outside the repository is rejected.
7. Review validation occurs before the existing completion workflow.
8. Invalid reviews prevent completion.
9. Validation warnings alone do not prevent completion.
10. Validation errors are surfaced clearly.
11. Completion parameters are delegated unchanged.
12. No stage/commit/push logic is duplicated.
13. No review validation rules are duplicated.
14. No direct Git/process execution is introduced in Tasks.
15. Required tests cover ordering, short-circuiting, path safety, delegation, failures, warnings, and cancellation.
16. Existing tests continue to pass.
17. `dotnet build` succeeds.
18. `dotnet test` succeeds.
19. `git diff --check` reports no whitespace errors.
20. No out-of-scope functionality is implemented.
21. `docs/developer-reviews/REVIEW-0011.md` is created.

## Codex Completion Protocol

After implementation and verification:

1. Do **not** create a Git commit.
2. Do **not** push changes.
3. Do **not** modify this Developer Task.
4. Create `docs/developer-reviews/REVIEW-0011.md`.
5. Use these sections:

# REVIEW-0011 – Review Completion Gate

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

## Deviations from DEV-0011

## Open Issues / Known Limitations

## Commit and Push
No commit created.
No push performed.

6. Use `READY FOR REVIEW` only when all acceptance criteria and verification succeed; otherwise use `BLOCKED`.
7. Record build success/failure, warning/error counts, test passed/failed/skipped counts, and diff-check result.
8. List every created, modified, or deleted file.
9. Write `None` when there are no deviations or open issues.

The review report is part of DEV-0011 and must be included in the later Pull Request.
