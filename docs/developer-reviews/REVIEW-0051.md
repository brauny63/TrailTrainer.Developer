# REVIEW-0051 – Codex Service Execution Diagnostics and User Environment

## Status
READY FOR REVIEW

## Summary
Codex process execution now uses an explicit effective-user profile environment and emits bounded, secret-safe start and completion diagnostics. Exit code 0 followed by failed review validation remains retryable at `BranchCreated` and now includes the captured Codex output needed to diagnose incomplete execution.

## Production Failure Reproduced
The DEV-0007 failure mode is covered by an automated regression: Codex exits 0, the expected review is missing, the state remains `BranchCreated`, and completion, push, and pull-request activity remain blocked. No real Codex or external service was invoked.

## Root Cause
The service process relied on its inherited environment and provided no Codex stdout/stderr logging. A named-user Windows Service can therefore resolve profile-dependent Codex configuration differently from an interactive invocation, while an exit-zero incomplete run leaves insufficient evidence for diagnosis.

## Codex Process Diagnostics
Each invocation logs task/repository context, executable, working directory, effective domain/user identity, whitelisted profile variables, process start, process finish, exit code, timeout, cancellation, and bounded stdout/stderr.

## User/Profile Environment Resolution
The executor resolves the effective account profile with `Environment.SpecialFolder.UserProfile`, then explicitly supplies `USERPROFILE`, `HOME`, `HOMEDRIVE`, `HOMEPATH`, `APPDATA`, and `LOCALAPPDATA`. Inherited `TEMP`, `TMP`, and `PATH` are preserved. Optional `CodexExecution:UserProfileDirectory` supports deterministic deployment override without a checked-in machine-specific path.

## ProcessStartInfo Changes
`UseShellExecute = false`, the explicit repository working directory, redirected stdout/stderr, `ArgumentList`, cancellation/timeout behavior, and process-tree termination are preserved. No shell wrapper was introduced.

## Exit-Zero Incomplete Execution Handling
Failed post-execution review validation includes bounded Codex stdout/stderr in the controlled `DeveloperTaskExecutionException`. `CodexSucceeded` is not persisted, and Git/GitHub completion is not reached.

## Secret-Safe Logging
Environment diagnostics enumerate only the required non-secret variable whitelist. Arbitrary environment variables are never enumerated. Output and environment diagnostics are capped by `MaximumDiagnosticCharacters`; credential files and contents are never read or logged.

## Hosted-Service Failure Handling
Expected process failures, timeouts, cancellation, and postcondition failures remain task-level controlled outcomes. Existing hosted-service recovery behavior is unchanged.

## Requirements Implemented
- Bounded process and output diagnostics.
- Deterministic effective-user profile environment.
- Strict exit-zero postcondition handling with useful diagnostics.
- Existing timeout, cancellation, branch safety, recovery, and DEV-0050 validation preserved.
- Fake executable-host integration coverage without external effects.

## Files Created
- `tests/TrailTrainer.Developer.FakeCodexCli/TrailTrainer.Developer.FakeCodexCli.csproj`
- `tests/TrailTrainer.Developer.FakeCodexCli/Program.cs`
- `docs/developer-reviews/REVIEW-0051.md`

## Files Modified
- `src/TrailTrainer.Developer.Host/CodexCliTaskExecutor.cs`
- `src/TrailTrainer.Developer.Host/CodexExecutionOptions.cs`
- `src/TrailTrainer.Developer.Tasks/DeveloperTaskWorkflow.cs`
- `tests/TrailTrainer.Developer.Tests/CodexTaskExecutionIntegrationTests.cs`
- `tests/TrailTrainer.Developer.Tests/TrailTrainer.Developer.Tests.csproj`

## Files Deleted
None.

## Tests Added
- Fake executable host verifies executable invocation, repository working directory, single instruction argument, profile environment, stdout, stderr, and exit code.
- Logging test verifies bounded diagnostics and exclusion of secret-like environment values.
- Real-process timeout test verifies process-tree termination; real-process cancellation test verifies cancellation propagation and termination.
- DEV-0007 regression now verifies useful stdout/stderr on exit-zero missing-review failure.

## Executable Host Integration Test
The harmless .NET fake CLI runs as a real child process through `CodexCliTaskExecutor`. It proves working-directory and environment propagation plus stdout, stderr, and nonzero exit-code capture. The real Codex CLI is never used.

## DEV-0007 Retry Readiness
The next pilot will expose the exact executable, repository directory, service identity, effective profile/config paths, exit code, and bounded Codex output. This is sufficient to explain any remaining difference from the successful manual invocation.

## Verification
### dotnet build
Passed: 0 errors, 0 warnings (NuGet audit disabled only for sandbox-local restore because external audit access is unavailable).

### dotnet test
Passed: 821 tests, 0 failed, 0 skipped.

### git diff --check
Passed: no whitespace errors.

## Deviations from DEV-0051
None.

## Open Issues / Known Limitations
The optional profile override must identify the profile of the same named service account; arbitrary impersonation is intentionally unsupported. Production DEV-0007 itself was not rerun because real Codex/SCM effects are outside this task's automated verification.

## TerrainEngine DEV-0007 Retry Readiness
READY

## Commit and Push
No commit created.
No push performed.
