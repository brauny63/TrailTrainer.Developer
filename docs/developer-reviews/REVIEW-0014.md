# REVIEW-0014 – CI / Pull Request Status Gate

## Status
READY FOR REVIEW

## Summary

Implemented a read-only GitHub Pull Request CI/status gate. The service reads the Pull Request head SHA, retrieves all Check Runs and commit-status contexts for that SHA, normalizes them into provider-neutral Core models, and evaluates the current gate state without polling or mutation.

## Requirements Implemented

- Added provider-neutral gate and check states with exactly Pending, Successful, and Failed.
- Added immutable check and result models with validation and a defensive read-only check collection.
- Added a mockable asynchronous status-gate abstraction with cancellation support.
- Added GitHub REST retrieval for Pull Request details, Check Runs, and combined commit statuses.
- Uses the exact head SHA from the Pull Request response for both commit-specific requests.
- Implemented conservative Check Run and commit-status normalization.
- Implemented failure-first gate evaluation; an empty collection is Pending.
- Preserves duplicate Check Run and status-context names.
- Follows GitHub `Link` pagination for Check Runs and commit-status contexts.
- Validates inputs before HTTP and rejects incomplete or malformed required response data.
- Supports a configurable API base URI, externally supplied authentication, safe diagnostics, and cancellation.
- Introduces no mutation, process, shell, CLI, polling, or retry behavior.

## Files Created

- `src/TrailTrainer.Developer.Core/IPullRequestStatusGate.cs`
- `src/TrailTrainer.Developer.Core/PullRequestCheck.cs`
- `src/TrailTrainer.Developer.Core/PullRequestCheckState.cs`
- `src/TrailTrainer.Developer.Core/PullRequestGateState.cs`
- `src/TrailTrainer.Developer.Core/PullRequestStatusGateResult.cs`
- `src/TrailTrainer.Developer.GitHub/GitHubPullRequestStatusGate.cs`
- `tests/TrailTrainer.Developer.Tests/GitHubPullRequestStatusGateTests.cs`
- `docs/developer-reviews/REVIEW-0014.md`

## Files Modified

None.

## Files Deleted

None.

## Architecture / Refactoring Notes

Provider-neutral models and the abstraction are in Core. GitHub DTOs, HTTP requests, pagination, and provider-specific normalization remain internal to the GitHub project. No Tasks component was needed because evaluation is a small part of the concrete read-only provider operation.

## Tests Added

- Input validation before HTTP.
- Pull Request head parsing, missing SHA rejection, request ordering, and exact SHA propagation.
- Every specified Check Run status/conclusion normalization case.
- Every specified commit-status normalization case.
- Name and details URL mapping.
- Failed, Pending, Successful, empty, mixed, combined, and duplicate-name gate scenarios.
- Check Run and commit-status pagination, including termination without a next link.
- Non-success responses and malformed JSON at all three request stages.
- Configurable API base URI and required GitHub REST headers.
- Authorization secrecy in exception diagnostics.
- Cancellation propagation.
- Core model validation and defensive collection copying.
- The full existing regression suite remains passing.

## Verification

### dotnet build

Successful. 0 warnings, 0 errors.

### dotnet test

Successful. 242 passed, 0 failed, 0 skipped.

### git diff --check

Successful. No whitespace errors.

## Deviations from DEV-0014

None.

## Open Issues / Known Limitations

None.

## Commit and Push
No commit created.
No push performed.
