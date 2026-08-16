# DEV-0043 – Windows Service Deprovisioning Command

## Goal

Add a complementary, explicit deprovisioning command for the `TrailTrainer Developer` Windows Service.

DEV-0042 provisions the service without starting it. DEV-0043 provides the inverse operational workflow: safely stop the service when necessary and uninstall it, while reusing the existing DEV-0038 service-management boundary.

## Scope

Add:

`TrailTrainer.Developer.Host deprovision`

Required sequence:

1. determine whether the service exists;
2. if absent, return deterministic success;
3. if running or otherwise requiring stop, stop it using the existing operation;
4. uninstall it using the existing operation;
5. return without deleting application files or lifecycle state.

## Requirements

- Extend the existing management-command dispatcher.
- Reuse the stable service identity.
- Reuse existing status, stop, and uninstall operations.
- Do not duplicate SCM command construction.
- Do not start the Generic Host or automatic-resume pipeline.
- Missing service is an idempotent successful outcome.
- Stop only when required by the current normalized service state.
- If stop fails, do not uninstall.
- If uninstall fails after a successful stop, surface failure.
- Do not restart the service.
- Do not delete binaries, configuration, logs, lifecycle persistence, repositories, or user data.
- Do not undo recovery/delayed-start settings separately; service deletion owns removal of SCM configuration.
- Preserve exit codes: 0 success, 1 operation failure, 2 invalid command/arguments.
- Non-Windows execution fails clearly and safely.
- No retry, polling, rollback loop, PowerShell, Git/GitHub behavior, or Developer Task execution.

## State Handling

Use the existing normalized service-state model.

At minimum:
- `NotInstalled` -> success, no stop/uninstall call.
- `Stopped` -> uninstall directly.
- `Running` -> stop, then uninstall.

For pending/paused/unknown states, follow existing Windows-service semantics conservatively. Do not invent polling. If safe deterministic deprovisioning cannot proceed without waiting/polling, fail clearly instead.

## Failure Semantics

The workflow is fail-fast and non-transactional.

- Stop failure -> return failure; do not uninstall.
- Successful stop followed by uninstall failure -> return failure; leave resulting SCM state as-is.
- No automatic rollback or restart.

## Tests

Cover at least:
1. `deprovision` dispatches as management command.
2. Generic Host is not started.
3. Exact service identity is used.
4. NotInstalled returns success.
5. NotInstalled performs no stop.
6. NotInstalled performs no uninstall.
7. Stopped service uninstalls without stop.
8. Running service stops before uninstall.
9. Stop executes exactly once when required.
10. Uninstall executes exactly once.
11. Stop failure prevents uninstall.
12. Uninstall failure is surfaced.
13. No restart occurs.
14. No application/lifecycle files are deleted.
15. Pending/paused/unknown states have deterministic safe behavior.
16. Non-Windows fails safely.
17. Invalid arguments return exit code 2.
18. No retry/polling/rollback loop.
19. Existing management commands including `provision` remain unchanged.
20. Existing tests continue to pass.

## Architecture

DEV-0043 is Windows operational orchestration only. It composes existing service-management operations and must not change Core, automatic-resume orchestration, persistence/discovery, Generic Host behavior, or production runtime registration.

## Out of Scope

- deleting application files
- deleting lifecycle state
- deleting repositories/logs
- rollback/restart after failure
- service account management
- installer/MSI
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

Required: 0 errors, no new warnings, all tests pass, no whitespace errors, and tests modify no real Windows Service.

## Acceptance Criteria

DEV-0043 is complete when `deprovision` safely and idempotently removes the Windows service by composing existing status/stop/uninstall behavior, with fail-fast semantics and no deletion of application or lifecycle data.

## Codex Completion Protocol

Create `docs/developer-reviews/REVIEW-0043.md` with:

```text
# REVIEW-0043 – Windows Service Deprovisioning Command
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
## Deviations from DEV-0043
## Open Issues / Known Limitations
## Commit and Push
No commit created.
No push performed.
```

Do not modify this task. Do not commit or push.
