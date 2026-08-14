# REVIEW-0015 – Pull Request Merge Gate

## Status
READY FOR REVIEW

## Summary

Implemented a guarded Pull Request merge workflow. It evaluates the existing DEV-0014 status gate exactly once, permits a merge only for `Successful`, and sends the exact head SHA returned by that evaluation to one GitHub merge request.

## Requirements Implemented

- Added provider-neutral merge method, merge result, and gated merge result models.
- Added mockable asynchronous merger and merge-gate abstractions.
- Added provider-neutral orchestration in Tasks using `IPullRequestStatusGate` and `IPullRequestMerger`.
- Pending and Failed gates stop before merge with distinct diagnostics.
- Successful gates delegate repository, PR number, method, optional text, cancellation token, and authoritative head SHA exactly.
- The status gate is evaluated once and the merger is invoked at most once.
- Added GitHub REST merge integration using `HttpClient` and `System.Text.Json`.
- Maps Merge, Squash, and Rebase exactly without fallback.
- Validates repository, PR number, expected SHA, and merge method before HTTP.
- Parses merged/non-merged responses and requires a merge SHA for confirmed success.
- Supports configurable API base URI, external authentication, safe diagnostics, and cancellation.
- Stale-head and other HTTP failures propagate without retry or gate re-evaluation.
- Introduces no shell, process, Git mutation, polling, retry, or CLI behavior.

## Files Created

- `src/TrailTrainer.Developer.Core/IPullRequestMergeGate.cs`
- `src/TrailTrainer.Developer.Core/IPullRequestMerger.cs`
- `src/TrailTrainer.Developer.Core/PullRequestGatedMergeResult.cs`
- `src/TrailTrainer.Developer.Core/PullRequestMergeMethod.cs`
- `src/TrailTrainer.Developer.Core/PullRequestMergeResult.cs`
- `src/TrailTrainer.Developer.GitHub/GitHubPullRequestMerger.cs`
- `src/TrailTrainer.Developer.Tasks/PullRequestMergeGate.cs`
- `tests/TrailTrainer.Developer.Tests/GitHubPullRequestMergerTests.cs`
- `tests/TrailTrainer.Developer.Tests/PullRequestMergeGateTests.cs`
- `docs/developer-reviews/REVIEW-0015.md`

## Files Modified

None.

## Files Deleted

None.

## Architecture / Refactoring Notes

Provider-neutral contracts and models remain in Core. Tasks performs only orchestration and contains no HTTP details. The GitHub project owns the concrete REST request and internal JSON DTOs. DEV-0014 status retrieval and normalization are reused without duplication.

## Tests Added

- Successful, Pending, and Failed gate paths with distinct diagnostics.
- Exact result identity, call count, ordering outcome, repository, PR number, authoritative SHA, method, optional text, and cancellation delegation.
- Status-gate and merger failure propagation without retry or second evaluation.
- Exact Merge, Squash, and Rebase JSON mapping.
- Pre-HTTP validation for null repository, invalid PR number, empty SHA, and unsupported enum values.
- Exact expected SHA, title, and message payload mapping, including null optional values.
- Successful and non-merged response mapping and required successful merge SHA validation.
- Non-success and stale-head responses, malformed/incomplete JSON, configurable base URI, required GitHub headers, authorization secrecy, and cancellation.
- Provider-neutral merge-result invariant validation.
- The full existing regression suite remains passing.

## Verification

### dotnet build

Successful. 0 warnings, 0 errors.

### dotnet test

Successful. 268 passed, 0 failed, 0 skipped.

### git diff --check

Successful. No whitespace errors.

## Deviations from DEV-0015

None.

## Open Issues / Known Limitations

None.

## Commit and Push
No commit created.
No push performed.
