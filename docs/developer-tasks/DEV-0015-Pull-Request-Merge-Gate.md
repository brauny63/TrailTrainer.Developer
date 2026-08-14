# DEV-0015 – Pull Request Merge Gate

## Metadata
- Task ID: `DEV-0015`
- Repository: `TrailTrainer.Developer`
- Expected branch: `feature/dev-0015-pull-request-merge-gate`
- Review report: `docs/developer-reviews/REVIEW-0015.md`
- Depends on: `DEV-0014`

## Goal
Add a guarded Pull Request merge capability.

A Pull Request may be merged only when a freshly evaluated CI/status gate reports `Successful` for the Pull Request's current head commit. The merge operation must use that exact head SHA as an expected value so that a new commit pushed after the gate evaluation cannot be merged accidentally.

This task covers guarded merge only. It must not poll for pending checks, delete branches, switch local branches, pull changes, or start the next Developer Task.

## Codex Execution Instructions
Work this Developer Task completely.

- Follow `docs/architecture/README.md`.
- Implement only the scope defined here.
- Reuse `IPullRequestStatusGate` from DEV-0014.
- Keep provider-neutral merge contracts/models in `TrailTrainer.Developer.Core`.
- Put concrete GitHub merge REST integration in `TrailTrainer.Developer.GitHub`.
- Put provider-neutral merge-gate orchestration in `TrailTrainer.Developer.Tasks`.
- Do not introduce HTTP logic in Tasks.
- Use `HttpClient`; do not launch `gh`, `git`, `curl`, PowerShell, a shell, or another process.
- Do not duplicate CI/status retrieval or normalization from DEV-0014.
- Do not modify this Developer Task or architecture documentation.
- Do not implement anything under **Out of Scope**.
- Do not create a Git commit for DEV-0015.
- Do not push the DEV-0015 implementation branch.
- After implementation and verification create `docs/developer-reviews/REVIEW-0015.md`.

If ambiguity prevents correct completion, document it and set the review status to `BLOCKED`.

## Scope
Implement a guarded merge workflow that:

1. Evaluates the Pull Request through the existing `IPullRequestStatusGate`.
2. Requires the gate state to be exactly `Successful`.
3. Uses the returned current head SHA as the expected head commit for the merge request.
4. Performs one GitHub Pull Request merge attempt.
5. Returns a strongly typed result.
6. Supports cancellation.
7. Performs no additional Git or repository mutation.

No merge request may occur when the gate is `Pending` or `Failed`.

## Core Models

### PullRequestMergeMethod
Add a strongly typed merge method supporting exactly:
- `Merge`
- `Squash`
- `Rebase`

### PullRequestMergeResult
Add an immutable provider-neutral result exposing at least:
- `PullRequestNumber`
- `Merged`
- optional `MergeCommitSha`
- `Method`

`Merged == true` means GitHub confirmed that the PR was merged by this operation.

If `Merged == true`, `MergeCommitSha` must be non-empty.

### PullRequestGatedMergeResult
Add an immutable orchestration result exposing at least:
- `StatusGate`
- `Merge`

A successful guarded merge contains the exact status-gate and merge results.

## Core Abstractions

### IPullRequestMerger
Add a mockable asynchronous provider-neutral abstraction accepting:
- `GitHubRepositoryIdentity repository`
- Pull Request number
- expected head SHA
- `PullRequestMergeMethod method`
- optional commit title
- optional commit message
- optional `CancellationToken`

Returns `PullRequestMergeResult`.

### IPullRequestMergeGate
Add a mockable asynchronous orchestration abstraction accepting:
- `GitHubRepositoryIdentity repository`
- Pull Request number
- `PullRequestMergeMethod method`
- optional commit title
- optional commit message
- optional `CancellationToken`

Returns `PullRequestGatedMergeResult`.

## Merge Gate Orchestration
Implement `IPullRequestMergeGate` in `TrailTrainer.Developer.Tasks`.

Expected injected dependencies:
- `IPullRequestStatusGate`
- `IPullRequestMerger`

Required ordering:
1. Evaluate the current Pull Request status gate.
2. Inspect the returned state.
3. If state is not `Successful`, stop.
4. If state is `Successful`, call the merger using the exact `HeadSha` returned by that gate.
5. Return the exact gate and merge results.

Do not evaluate the status gate a second time within the same operation.
Do not perform HTTP directly from Tasks.

## Gate Behavior
- `Pending`: do not call merger; fail clearly that the PR is still pending.
- `Failed`: do not call merger; fail clearly that CI/status checks failed.
- `Successful`: permit exactly one merge call.

The overall DEV-0014 gate state is authoritative.

## Expected Head SHA Safety
The merge request must include the exact head SHA returned by the successful gate evaluation.

The workflow must not accept a caller-supplied expected SHA.

If GitHub rejects the merge because the head changed, propagate the failure. Do not automatically re-evaluate or retry.

## GitHub Merge Implementation
Implement `IPullRequestMerger` in `TrailTrainer.Developer.GitHub`.

Use GitHub's Pull Request merge REST endpoint with `HttpClient` and `System.Text.Json`.

Request fields:
- expected head SHA,
- merge method,
- optional commit title,
- optional commit message.

Method mapping:
- `Merge` -> `merge`
- `Squash` -> `squash`
- `Rebase` -> `rebase`

Do not auto-fallback to another merge method.

## Input Validation
Before HTTP:
1. repository must not be null,
2. PR number must be > 0,
3. expected head SHA must be non-empty,
4. unsupported merge enum values must be rejected.

Optional title/message may be null or empty and must not be semantically rewritten.

## GitHub Response Handling
Parse the GitHub merge response into `PullRequestMergeResult`.

For a successful merge:
- `Merged == true`
- merge SHA must be present.

For a non-merged response:
- `Merged == false`
- no false success may be reported.

Hard HTTP failures must fail clearly. Do not include sensitive response bodies or authorization values in diagnostics.

## HTTP Behavior
Requirements:
- configurable API base URI,
- public GitHub API may be the default,
- proper GitHub JSON headers,
- externally supplied authentication,
- no hard-coded credentials,
- cancellation respected,
- malformed required response data fails clearly.

Tests must not use the public internet.

## Tests
Use injected fakes for Tasks tests and fake/in-memory HTTP for GitHub merger tests.

Cover at least:

### Merge gate orchestration
1. Successful gate permits merger invocation.
2. Exact gate result returned.
3. Exact merger result returned.
4. Pending prevents merge.
5. Failed prevents merge.
6. Pending diagnostic clear.
7. Failed diagnostic clear.
8. Status gate called exactly once.
9. Merger called exactly once on success.
10. Repository identity delegated exactly.
11. PR number delegated exactly.
12. Expected SHA comes from gate result.
13. Caller cannot override expected SHA.
14. Merge method delegated exactly.
15. Optional title delegated exactly.
16. Optional message delegated exactly.
17. Cancellation token propagated to status gate.
18. Cancellation token propagated to merger.
19. Status-gate failure prevents merger.
20. Merger failure propagated.
21. Merger failure does not trigger re-evaluation or retry.

### Merge method mapping
22. Merge -> `merge`.
23. Squash -> `squash`.
24. Rebase -> `rebase`.
25. Unsupported enum rejected before HTTP.

### GitHub merger HTTP
26. Invalid PR number rejected before HTTP.
27. Empty expected SHA rejected before HTTP.
28. Expected SHA sent exactly.
29. Commit title forwarded exactly.
30. Commit message forwarded exactly.
31. Null optional title/message supported.
32. Successful response maps `Merged == true`.
33. Successful response returns merge SHA.
34. Missing merge SHA on success fails clearly.
35. Non-merged response does not report false success.
36. Non-success HTTP response fails clearly.
37. Stale-head/head-change response propagates without retry.
38. Malformed JSON fails clearly.
39. Configurable API base URI honored.
40. Required GitHub headers present.
41. Authorization data not exposed in exceptions.
42. Cancellation propagates to HTTP.

### Regression
43. Existing DEV-0002 through DEV-0014 tests continue to pass.

Avoid unrelated refactoring.

## Out of Scope
Do not implement polling/waiting for CI, automatic retries, second gate evaluation after merge failure, auto-merge, branch protection discovery, required-review discovery, review submission, reviewer requests, comments, labels, merge queue integration, branch deletion, local branch switching, `git pull`, fetch/rebase, post-merge cleanup, automatic next-task selection, Codex execution, scheduling/monitoring, or a CLI merge command.

## Verification
Run:
- `dotnet build` — 0 errors and no new DEV-0015 warnings.
- `dotnet test` — all tests pass.
- `git diff --check` — no whitespace errors.

## Acceptance Criteria
DEV-0015 is complete when:
1. Provider-neutral merge method and merge result models exist.
2. `IPullRequestMerger` exists.
3. `IPullRequestMergeGate` exists.
4. Merge-gate orchestration exists in Tasks.
5. Concrete GitHub merger exists in GitHub project.
6. DEV-0014 status gate is reused.
7. Pending and Failed prevent merge.
8. Successful is required.
9. Exact gate head SHA is sent to merge API.
10. Caller cannot override expected SHA.
11. No second gate evaluation or retry occurs.
12. Merge method mapping is exact.
13. Optional title/message forwarded exactly.
14. Merge success returns merge SHA.
15. Stale-head failure propagates.
16. API base URI configurable.
17. Authentication externalized and secrets protected.
18. No shell/process/Git mutation logic introduced.
19. Tests require no network.
20. Existing tests pass.
21. Build succeeds.
22. Tests succeed.
23. `git diff --check` succeeds.
24. No out-of-scope functionality implemented.
25. `docs/developer-reviews/REVIEW-0015.md` is created.

## Codex Completion Protocol
After implementation and verification:
1. Do **not** create a Git commit.
2. Do **not** push changes.
3. Do **not** modify this Developer Task.
4. Create `docs/developer-reviews/REVIEW-0015.md`.
5. Use these sections:

# REVIEW-0015 – Pull Request Merge Gate

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

## Deviations from DEV-0015

## Open Issues / Known Limitations

## Commit and Push
No commit created.
No push performed.

6. Use `READY FOR REVIEW` only when all acceptance criteria and verification succeed; otherwise use `BLOCKED`.
7. Record build success/failure, warning/error counts, test passed/failed/skipped counts, and diff-check result.
8. List every created, modified, or deleted file.
9. Write `None` when there are no deviations or open issues.

The review report is part of DEV-0015 and must be included in the later Pull Request.
