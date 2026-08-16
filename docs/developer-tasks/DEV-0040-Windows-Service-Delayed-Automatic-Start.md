# DEV-0040 – Windows Service Delayed Automatic Start

## Goal
Extend the Windows Service operational management boundary with explicit configuration of delayed automatic startup for the existing `TrailTrainer Developer` service.

DEV-0038 owns service-management commands and DEV-0039 owns SCM recovery policy. DEV-0040 adds only delayed-auto-start configuration and must not alter automatic-resume orchestration.

## Scope
Add a management command:

`TrailTrainer.Developer.Host delayed-start`

The command configures the already-installed Windows service for automatic startup with delayed automatic start enabled.

Use the existing Windows service manager and safe process-runner abstractions.

## Requirements
- Extend the existing mockable service-management abstraction.
- Use exactly the existing service identity.
- Verify the service exists before configuration.
- Configure start mode as automatic.
- Enable delayed automatic start using the supported Windows SCM mechanism.
- Use structured process arguments if `sc.exe` is used.
- No PowerShell or shell interpolation.
- Do not start/restart the service as part of configuration.
- Do not start the Generic Host for this management command.
- Non-Windows execution fails clearly and safely.
- Missing service fails deterministically.
- Preserve established exit codes: 0 success, 1 operation failure, 2 invalid command/arguments.
- No retry, polling, timers, custom restart logic, Git/GitHub behavior, or Developer Task execution.
- Preserve DEV-0039 recovery behavior and all existing management commands unchanged.

## Tests
Cover at least:
1. `delayed-start` dispatches exactly once.
2. Generic Host is not started.
3. Exact service identity is used.
4. Service existence is checked.
5. Missing service fails deterministically.
6. Automatic start mode is configured.
7. Delayed automatic start is enabled.
8. Service is not started or restarted.
9. Non-Windows fails safely.
10. Structured SCM/process arguments are used.
11. No shell/PowerShell invocation.
12. SCM failure is surfaced.
13. No retry/polling.
14. DEV-0038 commands remain unchanged.
15. DEV-0039 recovery command remains unchanged.
16. Existing tests continue to pass.

## Out of Scope
- service installation redesign
- recovery-policy changes
- service account configuration
- custom watchdog
- application-level restart logic
- systemd
- notifications
- Git/GitHub automation
- automatic Developer Task execution
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
DEV-0040 is complete when `TrailTrainer Developer` can be configured for Windows delayed automatic startup through the existing safe service-management boundary, without starting the service or changing automatic-resume behavior.

## Codex Completion Protocol
Create `docs/developer-reviews/REVIEW-0040.md` with:

```text
# REVIEW-0040 – Windows Service Delayed Automatic Start
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
## Deviations from DEV-0040
## Open Issues / Known Limitations
## Commit and Push
No commit created.
No push performed.
```

Do not modify this task. Do not commit or push.
