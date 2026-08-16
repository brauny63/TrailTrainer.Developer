# DEV-0041 – Windows Service Operational Setup

## Goal
Add one explicit operational setup command that applies the already implemented Windows Service configuration steps for `TrailTrainer Developer` in a deterministic sequence.

DEV-0038 provides installation and service-management commands, DEV-0039 provides recovery policy, and DEV-0040 provides delayed automatic start. DEV-0041 composes these existing capabilities into a convenient setup operation without duplicating SCM logic.

## Scope
Add a management command:

`TrailTrainer.Developer.Host setup`

The command configures an already-installed service by applying:
1. delayed automatic start;
2. recovery policy.

It must reuse the existing service-management abstractions/operations from DEV-0039 and DEV-0040.

Installation itself remains explicit and separate.

## Requirements
- Extend the existing command dispatcher.
- `setup` must not install the service.
- Verify the service exists before applying configuration.
- Apply delayed-start configuration exactly once.
- Apply recovery configuration exactly once.
- Execute in deterministic order: delayed start, then recovery.
- Stop immediately if the first operation fails.
- Surface the original operation failure.
- Do not rollback successful SCM configuration.
- Do not start or restart the service.
- Do not start the Generic Host or automatic-resume pipeline.
- Preserve established exit codes: 0 success, 1 operation failure, 2 invalid command/arguments.
- Non-Windows execution fails safely.
- Reuse the exact stable service identity.
- Do not invoke `sc.exe` directly from the dispatcher when existing manager operations already encapsulate it.
- No retry, polling, timers, PowerShell, Git/GitHub behavior, or Developer Task execution.

## Tests
Cover at least:
1. `setup` dispatches as a management command.
2. Generic Host is not started.
3. Service existence is verified.
4. Missing service fails before configuration.
5. Delayed-start operation executes exactly once.
6. Recovery operation executes exactly once.
7. Delayed start occurs before recovery.
8. Delayed-start failure prevents recovery.
9. Recovery failure is surfaced.
10. Success returns exit code 0.
11. Failure returns exit code 1.
12. Invalid arguments return exit code 2.
13. Service is not installed by setup.
14. Service is not started/restarted.
15. Non-Windows fails safely.
16. Existing install/uninstall/start/stop/status/recovery/delayed-start commands remain unchanged.
17. No retry/polling/rollback loop.
18. Existing tests continue to pass.

## Architecture
The setup command is orchestration only at the Windows operational boundary. It may sequence existing service-management operations but must not contain or duplicate SCM command construction.

No changes to Core, automatic-resume workflow semantics, lifecycle persistence/discovery, Generic Host worker behavior, or production pipeline orchestration.

## Out of Scope
- implicit installation
- implicit service start
- rollback/transaction semantics
- service-account management
- recovery-policy redesign
- delayed-start redesign
- systemd
- MSI/installer packaging
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
DEV-0041 is complete when the installed `TrailTrainer Developer` service can be operationally configured with one `setup` command that reuses DEV-0039 and DEV-0040 behavior in deterministic fail-fast order without installing, starting, or restarting the service.

## Codex Completion Protocol
Create `docs/developer-reviews/REVIEW-0041.md` with:

```text
# REVIEW-0041 – Windows Service Operational Setup
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
## Deviations from DEV-0041
## Open Issues / Known Limitations
## Commit and Push
No commit created.
No push performed.
```

Do not modify this task. Do not commit or push.
