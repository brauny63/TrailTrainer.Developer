# DEV-0039 – Windows Service Recovery Policy

## Goal
Extend the existing Windows Service operational boundary from DEV-0038 with explicit, testable configuration of Windows Service recovery behavior.

Configure the `TrailTrainer Developer` service so operational failures can be handled by Windows SCM without adding retry/restart logic to the application.

## Scope
Add a management command:

`TrailTrainer.Developer.Host recovery`

It configures SCM recovery policy for the already-installed service.

Required policy:
- first failure: restart service
- second failure: restart service
- subsequent failure: restart service
- reset failure count after 1 day
- restart delay: 60 seconds
- enable recovery actions for non-crash failures where supported

Use the existing DEV-0038 Windows service manager/process abstractions. Keep all SCM-specific behavior in the Host boundary.

## Requirements
- Extend the existing mockable service-management abstraction rather than bypassing it.
- Use the stable existing service identity.
- Use structured `sc.exe` arguments; no shell interpolation or PowerShell dependency.
- `recovery` must not start the Generic Host or automatic-resume pipeline.
- Fail clearly when the service is not installed.
- Fail safely on non-Windows platforms.
- Return the established exit codes: 0 success, 1 operation failure, 2 invalid command/arguments.
- No retry, polling, timers, application-level restart loop, Git/GitHub behavior, or Developer Task execution.
- Do not change DEV-0025 through DEV-0038 workflow semantics.

## Tests
Cover at least:
1. `recovery` dispatches exactly once.
2. Management command does not start Generic Host.
3. Exact service identity is used.
4. Missing service fails deterministically.
5. Non-Windows fails safely.
6. First/second/subsequent actions are restart.
7. Reset period is one day.
8. Restart delay is 60 seconds.
9. Non-crash recovery flag is configured where supported.
10. Structured `sc.exe` arguments are used.
11. No shell/PowerShell invocation.
12. SCM failure is surfaced.
13. No retry/polling loop.
14. Existing DEV-0038 commands remain unchanged.
15. Existing tests continue to pass.

## Out of Scope
- service installation changes
- service account management
- delayed automatic start
- custom watchdog
- application restart logic
- systemd
- notifications
- Git/GitHub automation
- automatic next Developer Task execution
- Codex execution

## Verification
Run:

```text
dotnet build
dotnet test
git diff --check
```

Required: 0 errors, no new warnings, all tests pass, no whitespace errors.

## Acceptance Criteria
DEV-0039 is complete when the installed `TrailTrainer Developer` service can have the defined SCM recovery policy configured through the new command, entirely through the existing safe service-management boundary, with tests and no changes to automatic-resume orchestration.

## Codex Completion Protocol
Create `docs/developer-reviews/REVIEW-0039.md` with:

```text
# REVIEW-0039 – Windows Service Recovery Policy
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
## Deviations from DEV-0039
## Open Issues / Known Limitations
## Commit and Push
No commit created.
No push performed.
```

Do not modify this task. Do not commit or push.
