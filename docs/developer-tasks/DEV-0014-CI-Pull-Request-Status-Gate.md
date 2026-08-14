# DEV-0014 – CI / Pull Request Status Gate

## Metadata

- Task ID: `DEV-0014`
- Repository: `TrailTrainer.Developer`
- Expected branch: `feature/dev-0014-ci-pull-request-status-gate`
- Review report: `docs/developer-reviews/REVIEW-0014.md`
- Depends on: `DEV-0012`, `DEV-0013`

## Goal

Add a read-only CI / Pull Request status gate.

Given a GitHub repository and Pull Request, the toolkit must retrieve the Pull Request's current head commit, inspect its GitHub check runs and commit status, normalize those provider-specific states, and determine whether the Pull Request is currently ready to proceed from a CI/status perspective.

DEV-0014 must not merge the Pull Request and must not wait or poll for checks to finish.

## Codex Execution Instructions

Work this Developer Task completely.

- Follow `docs/architecture/README.md`.
- Implement only the scope defined here.
- Reuse the GitHub repository and Pull Request models introduced by DEV-0012 where appropriate.
- Keep provider-neutral status/gate models and abstractions in `TrailTrainer.Developer.Core`.
- Put concrete GitHub REST status retrieval in `TrailTrainer.Developer.GitHub`.
- Put provider-neutral gate evaluation/orchestration in `TrailTrainer.Developer.Tasks` only if a separate evaluator is needed.
- Do not introduce HTTP logic in Tasks.
- Use `HttpClient`; do not launch `gh`, `git`, `curl`, PowerShell, a shell, or another process.
- Do not duplicate Pull Request creation/lookup logic from DEV-0012.
- Do not modify this Developer Task or architecture documentation.
- Do not implement anything under **Out of Scope**.
- Do not create a Git commit for DEV-0014.
- Do not push the DEV-0014 implementation branch.
- After implementation and verification create `docs/developer-reviews/REVIEW-0014.md`.

If an ambiguity prevents correct completion, do not invent behavior. Document it and set the review status to `BLOCKED`.

## Scope

Implement functionality that can:

1. Read the current Pull Request head commit SHA.
2. Read GitHub Check Runs for that commit.
3. Read the combined GitHub commit status for that commit.
4. Normalize the retrieved states into provider-neutral Core models.
5. Evaluate the current CI/status state as `Pending`, `Successful`, or `Failed`.
6. Return a strongly typed result containing the head SHA, individual checks/statuses, and gate state.
7. Support cancellation.
8. Perform no mutation.

## Core Models

### PullRequestGateState

Add a strongly typed state supporting exactly:

- `Pending`
- `Successful`
- `Failed`

### PullRequestCheckState

Add a strongly typed normalized state supporting exactly:

- `Pending`
- `Successful`
- `Failed`

### PullRequestCheck

Add an immutable model exposing at least:

- `Name`
- `State`
- optional `DetailsUrl`

`Name` must be non-empty.

### PullRequestStatusGateResult

Add an immutable model exposing at least:

- `PullRequestNumber`
- `HeadSha`
- `State`
- `Checks`

`Checks` must be a read-only collection.

The model must not expose GitHub JSON DTOs, HTTP status codes, raw GitHub conclusion strings, or authentication information.

## Core Abstraction

### IPullRequestStatusGate

Add a mockable asynchronous abstraction.

The operation must accept at least:

- `GitHubRepositoryIdentity repository`
- Pull Request number
- optional `CancellationToken`

It returns `PullRequestStatusGateResult`.

The abstraction must not expose `HttpClient`, GitHub DTOs, HTTP headers, or authentication details.

## GitHub Data Retrieval

Implement the concrete GitHub-backed service in `TrailTrainer.Developer.GitHub`.

Use GitHub REST JSON APIs to retrieve:

1. Pull Request details sufficient to determine the current head SHA.
2. Check Runs for the head SHA.
3. Combined commit status for the head SHA.

Do not scrape HTML.

The service must evaluate the current state from one consistent head SHA obtained from the Pull Request response.

## Check Run Normalization

Normalize GitHub Check Runs as follows.

### Pending

A Check Run is `Pending` when its status is not completed, including states such as:

- queued
- in_progress
- waiting
- pending
- requested

Unknown non-completed statuses must conservatively normalize to `Pending`.

### Successful

A completed Check Run is `Successful` only when its conclusion is:

- success
- neutral
- skipped

### Failed

A completed Check Run is `Failed` when its conclusion is any non-successful terminal conclusion, including:

- failure
- cancelled
- timed_out
- action_required
- startup_failure
- stale

Unknown completed conclusions must conservatively normalize to `Failed`.

A completed Check Run without a conclusion must normalize to `Failed`.

## Commit Status Normalization

The combined commit-status response may contain individual status contexts.

Normalize each context into a `PullRequestCheck` using its context name.

### Pending

GitHub status state:

- pending

### Successful

GitHub status state:

- success

### Failed

GitHub status states:

- failure
- error

Unknown commit-status states must conservatively normalize to `Failed`.

Use the status target URL as `DetailsUrl` when available.

## Check Naming

Check Run names use their GitHub check-run name.

Commit-status contexts use their GitHub context name.

If names collide between Check Runs and commit-status contexts, preserve all entries. Do not silently de-duplicate them in DEV-0014.

## Gate Evaluation

Evaluate the normalized collection using these rules:

1. If any check is `Failed`, overall state is `Failed`.
2. Otherwise, if any check is `Pending`, overall state is `Pending`.
3. Otherwise, if at least one check exists and every check is `Successful`, overall state is `Successful`.
4. If no checks/status contexts exist, overall state is `Pending`.

Failure takes precedence over pending.

Do not infer success merely because the GitHub combined status top-level state says success. Evaluate the normalized individual entries returned by the APIs.

## Pull Request Validation

Before HTTP:

- repository identity must be valid,
- Pull Request number must be greater than zero.

Reject invalid input before any HTTP request.

## HTTP Behavior

Use `HttpClient` and `System.Text.Json`.

Requirements:

- configurable API base URI,
- default production behavior may target GitHub's public API,
- appropriate GitHub REST headers,
- authentication supplied externally,
- no hard-coded credentials,
- no authorization values in diagnostics,
- cancellation respected,
- non-success responses fail clearly,
- malformed required response data fails clearly.

Do not include response bodies in exceptions when they could expose sensitive data.

A small strongly typed internal DTO layer is expected.

## Pagination

Check Runs may be paginated.

The implementation must not silently evaluate only the first page when additional pages exist.

Support GitHub pagination sufficiently to retrieve all Check Runs for the head SHA.

Tests do not need the public internet.

If commit-status contexts require pagination with the selected GitHub endpoint, handle that as well. Do not silently truncate status contexts.

## Tests

Tests must not call GitHub or the public internet.

Use fake/in-memory HTTP handlers and/or injected fakes.

Cover at least:

### Input

1. Invalid repository identity rejected before HTTP.
2. Pull Request number zero rejected before HTTP.
3. Negative Pull Request number rejected before HTTP.

### Pull Request / head SHA

4. Pull Request head SHA is parsed.
5. Empty/missing head SHA fails clearly.
6. Head SHA from the PR response is used for subsequent requests.

### Check Run normalization

7. queued -> Pending.
8. in_progress -> Pending.
9. completed + success -> Successful.
10. completed + neutral -> Successful.
11. completed + skipped -> Successful.
12. completed + failure -> Failed.
13. completed + cancelled -> Failed.
14. completed + timed_out -> Failed.
15. completed + action_required -> Failed.
16. completed + startup_failure -> Failed.
17. completed + stale -> Failed.
18. unknown non-completed status -> Pending.
19. unknown completed conclusion -> Failed.
20. completed with null conclusion -> Failed.
21. check name and details URL are mapped.

### Commit status normalization

22. pending -> Pending.
23. success -> Successful.
24. failure -> Failed.
25. error -> Failed.
26. unknown state -> Failed.
27. context name and target URL are mapped.

### Gate evaluation

28. Any Failed produces overall Failed.
29. Failed takes precedence over Pending.
30. Pending without failure produces overall Pending.
31. All successful entries produce overall Successful.
32. No entries produces overall Pending.
33. Mixed successful and pending produces Pending.
34. Check Runs and commit-status contexts are evaluated together.
35. Duplicate names are preserved.

### HTTP / pagination / failure

36. Pull Request request occurs before commit-specific requests.
37. Check Runs are requested for exact head SHA.
38. Commit status is requested for exact head SHA.
39. Multiple Check Run pages are combined.
40. No additional page is requested when none exists.
41. Non-success PR response fails.
42. Non-success Check Runs response fails.
43. Non-success commit-status response fails.
44. Malformed required JSON fails clearly.
45. Cancellation propagates.
46. Configurable API base URI is honored.
47. Authorization data is not exposed in exception text.

### Regression

48. Existing DEV-0002 through DEV-0013 tests continue to pass.

Avoid unrelated refactoring.

## Out of Scope

Do not implement:

- Pull Request merge,
- auto-merge,
- mergeability evaluation,
- branch protection rule discovery,
- required-check configuration discovery,
- waiting/polling/retry until CI completes,
- scheduled monitoring,
- GitHub Actions workflow triggering,
- rerunning failed jobs,
- cancelling jobs,
- downloading logs or artifacts,
- Pull Request review submission,
- comments,
- reviewer requests,
- labels,
- branch deletion,
- Git mutations,
- stage/commit/push,
- Codex execution,
- automatic remediation,
- automatic next-task selection,
- CLI command for the status gate.

These belong to later Developer Tasks.

## Verification

Run:

```text
dotnet build
```

Required:

- 0 errors,
- no new warnings caused by DEV-0014.

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

DEV-0014 is complete when:

1. Provider-neutral Pull Request gate/check state models exist.
2. `PullRequestCheck` and `PullRequestStatusGateResult` exist as immutable Core models.
3. `IPullRequestStatusGate` exists as a mockable asynchronous Core abstraction.
4. Concrete GitHub REST implementation exists in `TrailTrainer.Developer.GitHub`.
5. Pull Request head SHA is retrieved before commit-specific status queries.
6. Check Runs are normalized according to this task.
7. Commit-status contexts are normalized according to this task.
8. Failure takes precedence over pending.
9. All-success with at least one entry produces Successful.
10. Empty check collection produces Pending.
11. Duplicate check/context names are preserved.
12. Pagination does not silently truncate Check Runs/status contexts.
13. Input validation occurs before HTTP.
14. API base URI is configurable.
15. Authentication is externalized and secrets are not exposed.
16. Cancellation is supported.
17. No mutation, shell, process, or `gh` invocation is introduced.
18. Tests require no GitHub/network access.
19. Existing tests continue to pass.
20. `dotnet build` succeeds.
21. `dotnet test` succeeds.
22. `git diff --check` succeeds.
23. No out-of-scope functionality is implemented.
24. `docs/developer-reviews/REVIEW-0014.md` is created.

## Codex Completion Protocol

After implementation and verification:

1. Do **not** create a Git commit.
2. Do **not** push changes.
3. Do **not** modify this Developer Task.
4. Create:

   `docs/developer-reviews/REVIEW-0014.md`

5. The review report must contain:

```text
# REVIEW-0014 – CI / Pull Request Status Gate

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

## Deviations from DEV-0014

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

The review report is part of DEV-0014 and must be included in the later Pull Request.
