# DEV-0042 – Windows Service Provisioning Command

## Goal

Add one explicit provisioning command that combines the existing Windows Service installation and operational setup capabilities into a deterministic, fail-fast workflow for the `TrailTrainer Developer` service.

DEV-0038 provides service installation and management commands.
DEV-0039 provides recovery policy.
DEV-0040 provides delayed automatic start.
DEV-0041 provides the `setup` command for applying delayed-start and recovery configuration to an already-installed service.

DEV-0042 adds a convenience command that provisions a not-yet-installed service by installing it first and then applying the existing DEV-0041 setup behavior.

The service must not be started automatically.

## Scope

Add a management command:

`TrailTrainer.Developer.Host provision`

The command must:

1. verify the service is not already installed;
2. install the service using the existing DEV-0038 installation behavior;
3. apply the existing DEV-0041 operational setup behavior;
4. stop and surface failure immediately if any step fails;
5. leave the service installed but stopped after successful provisioning.

Do not duplicate SCM command construction already encapsulated by existing service-management operations.

## Requirements

- Extend the existing command dispatcher.
- Reuse the exact stable service identity `TrailTrainer Developer`.
- Reuse the existing current-executable-path handling from DEV-0038.
- Reuse the existing install operation rather than reimplementing installation.
- Reuse the existing DEV-0041 setup sequencing rather than duplicating delayed-start/recovery command logic.
- Execute in deterministic order:

```text
check absence
install
setup
return
```

- If the service already exists, fail deterministically and do not alter it.
- If installation fails, do not run setup.
- If setup fails after successful installation, surface the setup failure.
- Do not automatically uninstall on setup failure.
- Do not start or restart the service.
- Do not start the Generic Host or automatic-resume pipeline.
- Preserve established exit codes:
  - 0 success
  - 1 operation failure
  - 2 invalid command/arguments
- Non-Windows execution fails clearly and safely.
- No retry, polling, timers, rollback loop, PowerShell, Git/GitHub behavior, or Developer Task execution.
- Preserve all existing service-management commands unchanged.

## Failure Semantics

Provisioning is intentionally fail-fast but not transactional.

If installation succeeds and setup fails:

- return failure;
- leave the service installed;
- do not uninstall it automatically;
- do not attempt rollback;
- do not start the service.

This behavior must be clearly tested and documented.

## Tests

Cover at least:

1. `provision` dispatches as a management command.
2. Generic Host is not started.
3. Exact service identity is used.
4. Existing service causes deterministic failure.
5. Existing service prevents install.
6. Existing service prevents setup.
7. Install executes exactly once when service is absent.
8. Setup executes exactly once after successful install.
9. Install occurs before setup.
10. Install failure prevents setup.
11. Setup failure is surfaced.
12. Setup failure does not uninstall service.
13. Successful provision returns exit code 0.
14. Operation failure returns exit code 1.
15. Invalid arguments return exit code 2.
16. Service is not started or restarted.
17. Non-Windows fails safely.
18. No retry/polling/rollback loop.
19. Existing `install`, `uninstall`, `start`, `stop`, `status`, `recovery`, `delayed-start`, and `setup` commands remain unchanged.
20. Existing tests continue to pass.

## Architecture

DEV-0042 is orchestration at the Windows operational boundary only.

It must compose existing management operations and must not:

- add SCM command construction to the dispatcher;
- change Core;
- change automatic-resume orchestration;
- change lifecycle persistence/discovery;
- change Generic Host worker behavior;
- change production runtime registration.

## Out of Scope

Do not implement:

- automatic service start after provisioning;
- automatic rollback/uninstall on setup failure;
- service account configuration;
- credential handling;
- recovery-policy redesign;
- delayed-start redesign;
- installer packaging;
- MSI/WiX;
- systemd;
- notifications;
- Git/GitHub automation;
- automatic Developer Task execution;
- Codex execution.

## Verification

Run:

```text
dotnet build
dotnet test
git diff --check
```

Required:

- 0 build errors;
- no new warnings;
- all tests pass;
- no whitespace errors;
- no real Windows Service is installed or modified during tests.

## Acceptance Criteria

DEV-0042 is complete when:

1. `provision` exists as a management command.
2. It does not start the Generic Host.
3. It fails if the service already exists.
4. It reuses the existing install operation.
5. It reuses DEV-0041 setup behavior.
6. Install occurs before setup.
7. Install failure prevents setup.
8. Setup failure is surfaced without rollback.
9. Successful provisioning leaves the service installed and stopped.
10. No existing service-management behavior changes.
11. No new SCM command construction is duplicated in the dispatcher.
12. No retry, polling, rollback loop, Git/GitHub, or automatic Developer Task behavior is introduced.
13. Existing tests continue to pass.
14. `dotnet build`, `dotnet test`, and `git diff --check` succeed.
15. `docs/developer-reviews/REVIEW-0042.md` is created.

## Codex Completion Protocol

Create `docs/developer-reviews/REVIEW-0042.md` with:

```text
# REVIEW-0042 – Windows Service Provisioning Command

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

## Deviations from DEV-0042

## Open Issues / Known Limitations

## Commit and Push
No commit created.
No push performed.
```

Do not modify this task. Do not create a commit. Do not push.
