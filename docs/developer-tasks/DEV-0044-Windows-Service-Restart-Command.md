# DEV-0044 – Windows Service Restart Command

## Goal

Add an explicit, deterministic `restart` management command for the existing `TrailTrainer Developer` Windows Service.

DEV-0038 already provides status/start/stop operations and DEV-0043 completes the provisioning/deprovisioning lifecycle. DEV-0044 composes the existing service operations into a safe restart workflow without introducing polling, retry logic, or automatic-resume changes.

## Scope

Add:

`TrailTrainer.Developer.Host restart`

The command must inspect the existing normalized service state and use the existing service-management operations.

Required behavior:

- `Running` -> stop, then start.
- `Stopped` -> start only.
- `NotInstalled` -> deterministic operation failure.
- transitional/unsupported states -> deterministic safe failure unless an existing operation already defines a safe immediate behavior.

No polling or waiting loop may be added.

## Requirements

- Extend the existing management-command dispatcher.
- Reuse the exact stable service identity.
- Reuse existing `GetStatusAsync`, `StopAsync`, and `StartAsync` operations.
- Do not duplicate SCM command construction.
- Do not invoke `sc.exe` directly from orchestration when existing manager operations encapsulate it.
- Do not start the Generic Host or automatic-resume pipeline.
- For a running service, stop must complete successfully before start is invoked.
- Stop failure prevents start.
- Start failure is surfaced.
- For a stopped service, do not issue a redundant stop.
- For an absent service, issue neither stop nor start.
- Do not install, uninstall, provision, deprovision, or reconfigure the service.
- Preserve exit codes: 0 success, 1 operation failure, 2 invalid command/arguments.
- Non-Windows execution fails clearly and safely.
- No retry, polling, timer, rollback, PowerShell, Git/GitHub behavior, or Developer Task execution.

## State Handling

Use the established normalized service-state model.

At minimum:

```text
Running      -> Stop -> Start
Stopped      -> Start
NotInstalled -> Failure
```

For `StartPending`, `StopPending`, `Paused`, and `Unknown`, choose conservative deterministic behavior consistent with existing abstractions. Do not wait or poll for a transition. Document the chosen behavior in REVIEW-0044.

## Failure Semantics

Restart is fail-fast and non-transactional.

- status failure -> return failure;
- stop failure -> return failure, no start;
- start failure after successful stop -> return failure;
- no rollback or second start attempt.

## Tests

Cover at least:

1. `restart` dispatches as a management command.
2. Generic Host is not started.
3. Exact service identity is used.
4. Running service performs status, stop, start in order.
5. Running service stops exactly once.
6. Running service starts exactly once.
7. Stop failure prevents start.
8. Start failure is surfaced.
9. Stopped service starts exactly once.
10. Stopped service does not stop.
11. NotInstalled fails.
12. NotInstalled performs no stop/start.
13. Transitional states have deterministic safe behavior.
14. Unknown state has deterministic safe behavior.
15. No install/uninstall/provision/deprovision occurs.
16. Non-Windows fails safely.
17. Invalid arguments return exit code 2.
18. No retry/polling/timer/rollback loop.
19. Existing management commands remain unchanged.
20. Existing tests continue to pass.

## Architecture

DEV-0044 is operational orchestration at the Host boundary only. It must compose existing service-management operations and must not change:

- Core;
- lifecycle persistence/discovery;
- automatic-resume orchestration;
- Generic Host worker behavior;
- production runtime registration;
- provisioning/deprovisioning semantics.

## Out of Scope

- polling for SCM state transitions
- retry/backoff
- timeout/wait loops
- service installation/removal
- recovery-policy changes
- delayed-start changes
- application-level watchdog
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

DEV-0044 is complete when `restart` safely composes the existing status/stop/start operations with deterministic state handling and fail-fast behavior, without polling, retry, SCM duplication, or changes to automatic-resume behavior.

## Codex Completion Protocol

Create `docs/developer-reviews/REVIEW-0044.md` with:

```text
# REVIEW-0044 – Windows Service Restart Command
## Status
READY FOR REVIEW | BLOCKED
## Summary
## State Handling Implemented
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
## Deviations from DEV-0044
## Open Issues / Known Limitations
## Commit and Push
No commit created.
No push performed.
```

Do not modify this task. Do not commit or push.
