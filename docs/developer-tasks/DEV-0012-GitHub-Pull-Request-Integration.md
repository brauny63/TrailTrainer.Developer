# DEV-0012 – GitHub Pull Request Integration

## Metadata

- Task ID: `DEV-0012`
- Repository: `TrailTrainer.Developer`
- Expected branch: `feature/dev-0012-github-pull-request-integration`
- Review report: `docs/developer-reviews/REVIEW-0012.md`
- Depends on: `DEV-0005`, `DEV-0008`, `DEV-0011`

## Goal

Add a focused GitHub Pull Request integration layer.

After a Developer Task has been completed and pushed, the toolkit must be able to create a Pull Request for the task branch, or detect and return an already existing open Pull Request for the same head/base pair.

This package covers Pull Request creation and lookup only. It must not merge Pull Requests, submit reviews, inspect CI, or execute Codex.

## Codex Execution Instructions

Work this Developer Task completely.

- Follow `docs/architecture/README.md`.
- Implement only the scope defined here.
- Keep GitHub-specific models/contracts out of generic Git process code.
- Keep reusable provider-neutral contracts/models in `TrailTrainer.Developer.Core` where appropriate.
- Put concrete GitHub REST integration in a dedicated GitHub-facing implementation location consistent with the existing solution structure.
- Do not add GitHub logic to `TrailTrainer.Developer.Git`.
- Do not duplicate Git push logic.
- Use `HttpClient`; do not launch `gh`, `curl`, PowerShell, a shell, or another process.
- Do not modify this Developer Task or architecture documentation.
- Do not implement anything under **Out of Scope**.
- Do not commit or push the DEV-0012 implementation.
- After verification create `docs/developer-reviews/REVIEW-0012.md`.

If ambiguity prevents correct completion, document it and set the review report to `BLOCKED`.

## GitHub API Basis

Use GitHub's REST Pull Requests API.

Creation uses the repository Pull Requests endpoint and requires at least:

- owner,
- repository,
- title,
- head branch,
- base branch.

Lookup of open Pull Requests must use GitHub's Pull Requests listing/query behavior and filter by head/base without scraping HTML.

Use JSON HTTP APIs only.

Do not make application decisions by parsing localized human-readable GitHub error messages.

## Scope

Implement functionality that can:

1. Identify a GitHub repository by owner and repository name.
2. Search for an existing open Pull Request for a specific head branch and base branch.
3. Return the existing Pull Request when exactly one matching open PR exists.
4. Create a Pull Request when no matching open PR exists.
5. Reject an ambiguous state when multiple matching open PRs are returned.
6. Return a strongly typed result.
7. Support cancellation.

The operation must be idempotent with respect to repeated calls for the same open head/base pair: the second call must return the existing open PR rather than create another one.

## Core Models

### GitHubRepositoryIdentity

Add an immutable value/model exposing at least:

- `Owner`
- `Repository`

Reject null, empty, or whitespace-only values.

Do not include tokens or API URLs in this model.

### PullRequestInfo

Add an immutable provider-facing result model exposing at least:

- `Number`
- `Url`
- `Title`
- `HeadBranch`
- `BaseBranch`
- `IsDraft`

### PullRequestEnsureResult

Add an immutable result exposing at least:

- `PullRequest`
- `Created`

Where:

- `Created == true` when this call created a new PR.
- `Created == false` when an existing open PR was returned.

## Core Abstraction

### IPullRequestService

Add a mockable asynchronous abstraction with an operation equivalent to:

`EnsureOpenAsync(...)`

It must accept at least:

- `GitHubRepositoryIdentity repository`
- `string headBranch`
- `string baseBranch`
- `string title`
- optional `string? body`
- optional `bool draft`
- optional `CancellationToken`

It returns `PullRequestEnsureResult`.

The abstraction must not expose `HttpClient`, HTTP status codes, JSON DTOs, authentication headers, or provider implementation details.

## Input Validation

Before any HTTP request:

1. Repository identity must be valid.
2. `headBranch` must not be null/empty/whitespace.
3. `baseBranch` must not be null/empty/whitespace.
4. `title` must not be null/empty/whitespace.
5. Head and base branch names must not be equal using ordinal comparison.

Body may be null or empty.

Do not rewrite title, body, head, or base.

## Existing Pull Request Lookup

Before creating a PR, query open Pull Requests for the target repository and match the requested head/base pair.

Requirements:

1. Only open Pull Requests are considered.
2. Head branch match must be exact.
3. Base branch match must be exact.
4. Comparison in application logic is ordinal.
5. Zero matches means create a new Pull Request.
6. One match means return it with `Created == false`.
7. More than one match must fail clearly as ambiguous.

Do not silently choose one when multiple matches exist.

Do not search closed Pull Requests for idempotency.

## Pull Request Creation

When no matching open PR exists, create a new Pull Request with:

- exact supplied title,
- exact supplied head branch,
- exact supplied base branch,
- supplied body,
- supplied draft flag.

Return the created Pull Request with `Created == true`.

Do not:

- merge it,
- request reviewers,
- add labels,
- assign users,
- modify milestone,
- enable auto-merge,
- post comments,
- submit reviews.

## Authentication

Authentication must be supplied from outside the service.

The concrete implementation may accept a configured `HttpClient` and/or a token provider abstraction.

Requirements:

- never hard-code tokens,
- never store tokens in source,
- never log authorization headers,
- do not modify Git credential helpers,
- do not read GitHub credentials from Git remotes,
- do not prompt interactively.

The service must work with private repositories when the supplied credentials have suitable Pull Request permissions.

## HTTP Behavior

Use `HttpClient`.

Requests should use GitHub's JSON REST API with appropriate headers.

The implementation must:

- serialize request bodies with `System.Text.Json`,
- deserialize only fields needed by the domain result,
- treat non-success responses as operation failures,
- preserve useful non-secret diagnostic context,
- avoid exposing authorization values in exceptions,
- respect cancellation.

A small dedicated DTO layer is expected.

Do not use dynamic JSON when strongly typed internal DTOs are straightforward.

## API Base Address

Do not hard-code assumptions that prevent tests.

The concrete service must allow a configurable API base address so tests can use a local/fake HTTP handler.

Default production behavior may target GitHub's public API.

Do not implement GitHub Enterprise support beyond allowing a configurable base URI.

## Tests

Unit tests must not call GitHub or the public internet.

Use a fake `HttpMessageHandler` or equivalent in-memory HTTP test mechanism.

Cover at least:

### Input validation

1. Empty owner rejected before HTTP.
2. Empty repository name rejected before HTTP.
3. Empty head branch rejected before HTTP.
4. Empty base branch rejected before HTTP.
5. Empty title rejected before HTTP.
6. Same head/base rejected before HTTP.

### Lookup behavior

7. Zero existing matches triggers create.
8. One matching open PR returns existing result.
9. Existing result has `Created == false`.
10. Multiple matching PRs fail as ambiguous.
11. Non-matching head is ignored.
12. Non-matching base is ignored.
13. Exact branch comparison is used.
14. Closed PRs are not considered by the lookup request.
15. Lookup is performed before create.

### Creation behavior

16. Created result has `Created == true`.
17. Exact title is sent.
18. Exact head branch is sent.
19. Exact base branch is sent.
20. Body is forwarded unchanged.
21. Draft flag is forwarded unchanged.
22. Returned PR number is parsed.
23. Returned URL is parsed.
24. Returned head/base/title/draft fields are parsed.

### HTTP / failure behavior

25. Non-success lookup response fails.
26. Non-success create response fails.
27. Malformed required response data fails clearly.
28. Cancellation propagates to HTTP requests.
29. Authorization data is not included in exception text.
30. Configurable API base address is honored.
31. Repeated ensure call with existing open PR does not issue a create request.

Existing DEV-0002 through DEV-0011 tests must continue to pass.

Avoid unrelated refactoring.

## Out of Scope

Do not implement:

- Pull Request merge,
- mergeability checks,
- CI/status checks,
- Pull Request review submission,
- review comments,
- reviewer requests,
- labels,
- assignees,
- milestones,
- auto-merge,
- branch deletion,
- branch cleanup,
- Git push,
- GitHub issue integration,
- release/tag integration,
- webhook handling,
- Codex execution,
- automatic task workflow orchestration,
- CLI command for PR creation,
- GitHub Enterprise-specific features beyond configurable API base URI.

These belong to later Developer Tasks.

## Verification

Run:

```text
dotnet build
```

Required:

- 0 errors,
- no new warnings caused by DEV-0012.

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

DEV-0012 is complete when:

1. Repository identity, PR info, and ensure-result models exist.
2. A mockable asynchronous Pull Request service abstraction exists.
3. A concrete GitHub REST implementation exists.
4. Input validation occurs before HTTP.
5. Existing open PR lookup occurs before creation.
6. Exact head/base matching is enforced.
7. One existing open PR is returned idempotently.
8. Multiple matching open PRs fail as ambiguous.
9. New PR creation forwards title/body/head/base/draft exactly.
10. `HttpClient` is used; no shell/process invocation exists.
11. Authentication is externalized and no secrets are logged.
12. API base URI is configurable.
13. Tests use fake/in-memory HTTP and require no network.
14. Cancellation is supported.
15. Existing tests continue to pass.
16. `dotnet build` succeeds.
17. `dotnet test` succeeds.
18. `git diff --check` succeeds.
19. No out-of-scope functionality is implemented.
20. `docs/developer-reviews/REVIEW-0012.md` is created.

## Codex Completion Protocol

After implementation and verification:

1. Do **not** create a Git commit.
2. Do **not** push changes.
3. Do **not** modify this Developer Task.
4. Create `docs/developer-reviews/REVIEW-0012.md`.
5. Use these sections:

# REVIEW-0012 – GitHub Pull Request Integration

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

## Deviations from DEV-0012

## Open Issues / Known Limitations

## Commit and Push
No commit created.
No push performed.

6. Use `READY FOR REVIEW` only when all acceptance criteria and verification succeed; otherwise use `BLOCKED`.
7. Record build success/failure, warning/error counts, test passed/failed/skipped counts, and diff-check result.
8. List every created, modified, or deleted file.
9. Write `None` when there are no deviations or open issues.

The review report is part of DEV-0012 and must be included in the later Pull Request.
