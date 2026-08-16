# REVIEW-0058 - Authenticate Private GitHub Repositories and Preserve PR Recovery

## Status

READY FOR REVIEW

## Summary

Production GitHub API access now requires an explicitly injected bearer token and shares that authenticated client across Pull Request operations and a read-only repository probe. GitHub failures provide credential, permission, not-found/private-access, rate-limit, and general HTTP diagnostics, remain controlled at the hosted workflow boundary, and preserve a durable post-commit recovery stage so retries do not rerun Codex, commit, or push.

## Requirements Implemented

- Traced the former production credential path to a singleton unauthenticated HttpClient with no configured Authorization header.
- Added startup-validated GitHub:Token configuration supporting environment and secret-store configuration providers without source-controlled secrets.
- Applied the token only as an in-memory Bearer Authorization header and preserved existing Git remote authentication behavior.
- Preserved GitHubRepositoryIdentity validation and request escaping.
- Added explicit diagnostics for missing credentials, rejected credentials, insufficient permission, repository-not-found-or-private-access-denied responses, rate limiting, and other HTTP failures.
- Prevented response content and credential values from entering GitHub exceptions or diagnostics.
- Added the non-mutating github-probe command using the same authenticated GitHub service path with optional open-Pull-Request permission checking.
- Kept the normal health command free of GitHub network calls.
- Wrapped GitHub HTTP failures as controlled, retryable Developer Task failures so they do not terminate the Windows Service.
- Persisted the secret-free completion result before PR lookup or creation, allowing exact post-push recovery without rerunning Codex, committing again, or pushing again.
- Preserved EnsureOpenAsync lookup-before-create idempotency.

## Files Created

- `src/TrailTrainer.Developer.GitHub/GitHubApiException.cs`
- `src/TrailTrainer.Developer.GitHub/IGitHubRepositoryProbe.cs`
- `src/TrailTrainer.Developer.Host/GitHubApiOptions.cs`
- `src/TrailTrainer.Developer.Host/GitHubRepositoryProbeCommand.cs`
- `docs/developer-reviews/REVIEW-0058.md`

## Files Modified

- `src/TrailTrainer.Developer.Core/CodexExecutionPhase.cs`
- `src/TrailTrainer.Developer.Core/CodexExecutionState.cs`
- `src/TrailTrainer.Developer.GitHub/GitHubPullRequestService.cs`
- `src/TrailTrainer.Developer.Host/DeveloperProductionRuntimeServiceCollectionExtensions.cs`
- `src/TrailTrainer.Developer.Host/Program.cs`
- `src/TrailTrainer.Developer.Tasks/DeveloperTaskWorkflow.cs`
- `src/TrailTrainer.Developer.Tasks/HostedAutomaticResumeService.cs`
- `tests/TrailTrainer.Developer.Tests/CodexTaskExecutionIntegrationTests.cs`
- `tests/TrailTrainer.Developer.Tests/DeveloperTaskWorkflowTests.cs`
- `tests/TrailTrainer.Developer.Tests/DeveloperV1AcceptanceTests.cs`
- `tests/TrailTrainer.Developer.Tests/GitHubPullRequestServiceTests.cs`
- `tests/TrailTrainer.Developer.Tests/HostedAutomaticResumeServiceTests.cs`
- `tests/TrailTrainer.Developer.Tests/InitialDeveloperTaskIntakeTests.cs`
- `tests/TrailTrainer.Developer.Tests/OperationalHealthDiagnosticsTests.cs`
- `tests/TrailTrainer.Developer.Tests/ProductionRuntimeDependencyRegistrationTests.cs`

## Files Deleted

None

## Architecture / Refactoring Notes

GitHubApiOptions is the single explicit production credential source and is consumed only while constructing the shared GitHub HttpClient. GitHubPullRequestService owns authenticated request creation, response classification, idempotent PR ensure behavior, and the read-only diagnostic probe. GitHub deliberately uses 404 for both absent repositories and private repositories hidden from the authenticated identity, so that response is represented honestly as one actionable not-found-or-private-access-denied classification rather than claiming knowledge the API does not provide. DeveloperTaskWorkflow converts transport failures into the existing controlled host failure type and durably records CompletionSucceeded with the already produced commit and push metadata before crossing the GitHub boundary. No token is stored in Codex or lifecycle persistence.

## Tests Added

- Added authenticated private-style GitHub request success and same-client read-only repository probe coverage.
- Added missing credential, rejected credential, insufficient permission, authenticated 404/private-access diagnostic, rate-limit, and general HTTP failure classification coverage.
- Added credential non-disclosure coverage for exception text and response bodies.
- Added PR lookup and creation idempotency coverage.
- Added a DEV-0007 post-completion retry regression proving Codex and the commit/push completion stage each execute once while PR ensure retries.
- Updated hosted workflow boundary regressions to require controlled DeveloperTaskExecutionException handling for GitHub HTTP failures.
- Updated production composition and health tests for startup-validated secret injection without network or Windows SCM calls.

## Verification

### dotnet build

Successful. 0 warnings, 0 errors.

### dotnet test

Successful. 854 passed, 0 failed, 0 skipped.

### git diff --check

Successful. No whitespace errors.

## Deviations from DEV-0058

None

## Open Issues / Known Limitations

GitHub intentionally does not reveal whether an authenticated 404 denotes a nonexistent repository or a private repository hidden from that identity; the diagnostic reports both actionable possibilities. The production token itself must be provisioned externally through GitHub:Token, for example GitHub__Token in the Windows Service environment or another configured secret provider.

## Commit and Push

No commit created.
No push performed.
