# REVIEW-0012 – GitHub Pull Request Integration

## Status

READY FOR REVIEW

## Summary

Implemented a focused GitHub REST integration that idempotently returns an existing matching open Pull Request or creates a new one.

## Requirements Implemented

- Added validated repository identity, Pull Request information, and ensure-result models.
- Added a provider-neutral mockable asynchronous Pull Request service abstraction.
- Added a dedicated `TrailTrainer.Developer.GitHub` project referencing only Core.
- Validates repository, head, base, and title before issuing HTTP requests.
- Queries open Pull Requests before creation and filters head/base exactly using ordinal comparison.
- Returns one existing match, creates on zero matches, and rejects multiple matches.
- Forwards exact title, head, base, body, and draft values during creation.
- Maps only required strongly typed REST response fields.
- Uses externally configured `HttpClient` authentication and configurable API base address.
- Provides useful HTTP status diagnostics without response bodies, credentials, or authorization values.
- Supports cancellation throughout HTTP and JSON operations.

## Files Created

- `src/TrailTrainer.Developer.Core/GitHubRepositoryIdentity.cs`
- `src/TrailTrainer.Developer.Core/PullRequestInfo.cs`
- `src/TrailTrainer.Developer.Core/PullRequestEnsureResult.cs`
- `src/TrailTrainer.Developer.Core/IPullRequestService.cs`
- `src/TrailTrainer.Developer.GitHub/TrailTrainer.Developer.GitHub.csproj`
- `src/TrailTrainer.Developer.GitHub/GitHubPullRequestService.cs`
- `tests/TrailTrainer.Developer.Tests/GitHubPullRequestServiceTests.cs`
- `docs/developer-reviews/REVIEW-0012.md`

## Files Modified

- `TrailTrainer.Developer.sln`
- `tests/TrailTrainer.Developer.Tests/TrailTrainer.Developer.Tests.csproj`

## Files Deleted

None.

## Architecture / Refactoring Notes

GitHub REST concerns are isolated in a new GitHub-facing class library. Core exposes only provider-neutral domain contracts. No GitHub logic was added to the generic Git project, and no shell, process, Git push, workflow, CLI, or provider authentication management was introduced.

## Tests Added

- Invalid owner, repository, head, base, title, and equal-branch rejection before HTTP.
- Zero-match lookup followed by creation with exact payload and complete result mapping.
- Exact ordinal open head/base matching while ignoring closed and nonmatching candidates.
- Existing-result idempotency and no creation request.
- Multiple-match ambiguity rejection.
- Open-state lookup query and lookup-before-create request ordering.
- Configurable API base-address behavior.
- Repeated ensure behavior with only one create request.
- Non-success lookup and creation responses.
- Malformed and incomplete lookup/creation response handling.
- Authorization-secret exclusion from diagnostics.
- HTTP cancellation propagation.

## Verification

### dotnet build

Successful. 0 warnings, 0 errors.

### dotnet test

Successful. 188 passed, 0 failed, 0 skipped.

### git diff --check

Successful. No whitespace errors.

## Deviations from DEV-0012

None.

## Open Issues / Known Limitations

None.

## Commit and Push

No commit created.
No push performed.
