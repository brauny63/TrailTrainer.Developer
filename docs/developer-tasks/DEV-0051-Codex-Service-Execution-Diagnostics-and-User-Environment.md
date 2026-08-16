# DEV-0051 – Codex Service Execution Diagnostics and User Environment

## Goal

Fix the remaining production gap discovered during the real `TrailTrainer.TerrainEngine` DEV-0007 pilot.

The Windows Service successfully discovers DEV-0007, creates the expected feature branch, persists `BranchCreated`, launches Codex, receives exit code 0, and then correctly refuses to persist `CodexSucceeded` because no implementation or review report was produced.

The same `codex exec` instruction run manually under the same Windows user account successfully edits the repository, creates tests, and writes the review report.

Make Codex execution from the Windows Service observable and make its effective user/environment context equivalent enough to the successful manual invocation for supported production use.

## Production Evidence

Observed service behavior:

```text
Windows Service account: .\braujoh
Repository: TrailTrainer.TerrainEngine
Expected branch: feature/dev-0007-implement-valueobject
BranchCreated persisted
Codex process returns exit code 0
Repository remains unchanged
Expected review does not exist
DEV-0050 validation rejects success
State remains BranchCreated
Service remains Running
```

The same task and instruction manually executed with the configured Codex CLI under `HANS-PC\braujoh` produced the implementation, tests, and review.

## Scope

Correct only the remaining host/process-execution gap. Preserve DEV-0048, DEV-0049 and DEV-0050 behavior.

## Codex Execution Diagnostics

Enhance `CodexCliTaskExecutor` and/or its host logging boundary so every invocation records bounded, secret-safe diagnostics:

- task/repository context;
- executable path;
- working directory;
- process start/finish;
- exit code;
- timeout/cancellation;
- bounded stdout;
- bounded stderr;
- effective process/service user identity where practical;
- presence/effective values of relevant non-secret profile variables:
  - `USERPROFILE`
  - `HOME`
  - `HOMEDRIVE`
  - `HOMEPATH`
  - `APPDATA`
  - `LOCALAPPDATA`
  - `TEMP`
  - `TMP`
  - `PATH`

Do not dump the complete environment.

Never log secrets, tokens, credentials, cookies, authorization headers, or Codex credential file contents.

## Exit-Code-Zero Incomplete Execution

Exit code 0 plus failed post-execution validation is a first-class diagnostic case.

When Codex exits 0 but no valid review/output exists:

- include bounded stdout/stderr in logs or controlled failure diagnostics;
- keep state at `BranchCreated`;
- do not mark success;
- do not commit, push, create PRs, or merge;
- keep the service running.

Do not weaken DEV-0050 validation.

## Service User Environment

The production service runs under a named user account. Codex must resolve the same user profile/configuration locations as the successful manual invocation.

Do not hard-code `C:\Users\braujoh` or any machine-specific path.

Use deterministic environment construction or supported Windows account/profile APIs where required.

Relevant locations include:

```text
%USERPROFILE%\.codex
%APPDATA%
%LOCALAPPDATA%
```

Do not copy credential files into TrailTrainer.Developer configuration.

Do not require tokens or credentials in `appsettings.json`.

## ProcessStartInfo

Preserve:

- `UseShellExecute = false`;
- explicit repository working directory;
- redirected stdout/stderr;
- timeout/cancellation handling;
- process-tree termination;
- safe argument passing via `ArgumentList` or equivalent.

Do not build a shell command string.

Do not introduce PowerShell/cmd as a wrapper unless a proven Windows requirement makes it necessary.

## Production Configuration

Keep `CodexExecution:ExecutablePath`.

Add only the smallest optional configuration needed for environment/profile handling. Checked-in configuration must not contain machine-specific paths.

## Tests

Add tests for at least:

1. configured executable is used;
2. repository is the process working directory;
3. task instruction is passed as one argument;
4. profile/user environment variables are resolved/preserved;
5. stdout is captured;
6. stderr is captured;
7. exit code is captured;
8. timeout still kills the process tree;
9. cancellation still works;
10. diagnostics remain bounded;
11. secret-like environment values are not logged;
12. exit 0 + missing review includes useful Codex diagnostics;
13. state remains `BranchCreated` after incomplete execution;
14. commit/push/PR remain blocked;
15. valid Codex output still proceeds;
16. DEV-0049 recovery remains intact;
17. DEV-0050 validation remains intact.

No real Codex, GitHub, network, or Windows SCM effects in tests.

## Executable Host Integration Test

Add or extend an executable-host test using a harmless fake CLI/helper process and prove:

- correct working directory;
- expected user/profile environment;
- stdout capture;
- stderr capture;
- exit code capture.

Do not use the real Codex CLI in tests.

## Hosted-Service Behavior

Task-level process/environment failures must remain controlled and diagnosable, not create SCM crash loops.

Unrelated fatal configuration or programming errors may still propagate.

## Safety Requirements

- Never log secrets.
- Never expose Codex credentials.
- Never store credentials in appsettings.
- Never weaken repository safety.
- Never reset, clean, stash, force-checkout, or delete user work.
- Never mark success from exit code alone.
- Never proceed to Git/GitHub completion after failed postconditions.
- Never invoke Codex on `main`.
- No real external effects in tests.

## Architecture

Preferred ownership:

- Host: process creation, environment resolution, diagnostics;
- Tasks: controlled workflow failure and post-execution validation;
- Core: only minimal contracts if needed;
- Persistence: unchanged.

Do not put Windows profile logic into the workflow/domain layer.

## Out of Scope

- Developer Task format changes;
- review format changes;
- merge behavior changes;
- replacing Codex CLI;
- database persistence;
- multi-repository scheduling;
- parallel execution;
- arbitrary impersonation;
- installer redesign;
- copying credential files;
- interactive desktop automation.

## Verification

Run:

```text
dotnet build
dotnet test
git diff --check
```

Required:

- 0 errors;
- no new warnings;
- all tests pass;
- no whitespace errors;
- no real Codex/GitHub/network/Windows SCM effects.

## Acceptance Criteria

DEV-0051 is complete when:

1. production logs include bounded Codex stdout/stderr;
2. logs identify executable, working directory, exit code, and relevant non-secret profile context;
3. Windows Service Codex execution uses a deterministic named-user profile environment;
4. no secrets are logged;
5. exit 0 + missing review remains controlled and retryable;
6. DEV-0050 validation remains equally strict;
7. DEV-0048/0049/0050 regressions remain green;
8. fake executable-host integration tests prove environment/output behavior;
9. all tests pass;
10. the next real DEV-0007 pilot can explain any remaining difference from the successful manual invocation.

## Codex Completion Protocol

Create `docs/developer-reviews/REVIEW-0051.md` with:

```text
# REVIEW-0051 – Codex Service Execution Diagnostics and User Environment

## Status
READY FOR REVIEW | BLOCKED

## Summary
## Production Failure Reproduced
## Root Cause
## Codex Process Diagnostics
## User/Profile Environment Resolution
## ProcessStartInfo Changes
## Exit-Zero Incomplete Execution Handling
## Secret-Safe Logging
## Hosted-Service Failure Handling
## Requirements Implemented
## Files Created
## Files Modified
## Files Deleted
## Tests Added
## Executable Host Integration Test
## DEV-0007 Retry Readiness
## Verification
### dotnet build
### dotnet test
### git diff --check
## Deviations from DEV-0051
## Open Issues / Known Limitations
## TerrainEngine DEV-0007 Retry Readiness
READY | NOT READY
## Commit and Push
No commit created.
No push performed.
```

Do not modify this Developer Task.
Do not create a commit.
Do not push.
