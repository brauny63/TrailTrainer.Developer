# DEV-0038 – Windows Service Installation Command

## Metadata

- Task ID: `DEV-0038`
- Repository: `TrailTrainer.Developer`
- Expected branch: `feature/dev-0038-windows-service-installation-command`
- Review report: `docs/developer-reviews/REVIEW-0038.md`
- Depends on: `DEV-0037`

## Goal

Add a controlled Windows Service installation/uninstallation command for the executable host.

DEV-0036 made the host Windows-Service-aware and DEV-0037 completed the production runtime DI graph. DEV-0038 adds the operational boundary needed to install, inspect, start, stop, and uninstall the `TrailTrainer Developer` Windows Service without changing the automatic-resume workflow itself.

The implementation must isolate Windows Service Control Manager/process interaction behind a mockable abstraction so command behavior can be unit tested without modifying the developer machine.

## Codex Execution Instructions

Work this Developer Task completely.

- Follow `docs/architecture/README.md`.
- Implement only the scope defined here.
- Reuse the executable host from DEV-0035 through DEV-0037.
- Reuse the stable Windows service name `TrailTrainer Developer`.
- Keep service-management abstractions mockable and separate from automatic-resume orchestration.
- Prefer standard Windows tooling/API already available on supported Windows installations.
- Do not invoke service-management commands from constructors or DI registration.
- Do not alter DEV-0025 through DEV-0037 workflow semantics.
- Do not add polling, retry, cron, automatic Developer Task execution, Git, or GitHub workflow behavior.
- Do not modify this Developer Task or architecture documentation.
- Do not create a Git commit or push.
- After implementation and verification create `docs/developer-reviews/REVIEW-0038.md`.

If the repository already has an established command-line architecture, integrate with it. Otherwise implement the smallest host-boundary command parser necessary for this task without introducing a general CLI framework.

## Scope

Support explicit administrative commands conceptually equivalent to:

```text
TrailTrainer.Developer.Host install
TrailTrainer.Developer.Host uninstall
TrailTrainer.Developer.Host start
TrailTrainer.Developer.Host stop
TrailTrainer.Developer.Host status
```

Normal execution without one of these management commands must continue to run the DEV-0037 Generic Host.

## Service Identity

Use exactly:

```text
Service name: TrailTrainer Developer
```

If Windows requires a machine-oriented service key distinct from the display name, define one stable host-boundary constant and document it.

Do not move service identity into Core.

## Service Management Abstraction

Add a host/runtime abstraction representing Windows service management.

It must support the minimum operations required for:

- install;
- uninstall;
- start;
- stop;
- status.

The abstraction must make command behavior testable without accessing the real Service Control Manager.

Do not expose automatic-resume concepts through this abstraction.

## Production Windows Implementation

Add a Windows-specific production implementation.

It may use an established Windows service-management mechanism such as `sc.exe` through an injected process runner, or an appropriate supported .NET API.

If process execution is used:

- isolate it behind an existing process abstraction if one exists;
- otherwise add the smallest mockable process-runner abstraction required;
- never use shell string concatenation for untrusted arguments;
- pass executable and arguments structurally;
- capture exit code/stdout/stderr;
- propagate failures clearly.

Do not use PowerShell as an internal implementation dependency unless the repository already standardizes on it.

## Install Command

`install` must install the current executable as the Windows service.

Requirements:

- use the current executable path safely;
- configure the service for the existing DEV-0036 Windows Service host;
- use the stable service identity;
- reject unsupported/non-Windows execution clearly;
- do not silently overwrite an existing conflicting service;
- report success/failure through a deterministic result/exit code.

Do not start the service implicitly unless explicitly required by an existing repository convention. Prefer install-only behavior.

## Uninstall Command

`uninstall` must remove the service.

Requirements:

- deterministic behavior when service is absent;
- do not delete application files;
- do not alter persistence state;
- do not alter Git/GitHub state.

If Windows requires the service to be stopped first, handle that only as required for correct removal and document the behavior.

## Start / Stop Commands

`start` and `stop` operate only on the registered Windows service.

Requirements:

- no automatic retry loop;
- no polling loop;
- surface SCM/tool failure;
- do not directly execute the automatic-resume pipeline from the management command.

## Status Command

`status` reports a small deterministic service state.

Preferred normalized states:

```text
NotInstalled
Stopped
StartPending
StopPending
Running
Paused
Unknown
```

Map platform-specific output to the normalized state at the Windows boundary.

Do not expose raw localized command output as the domain result.

## Command Dispatch

The executable host must distinguish management commands before normal host startup.

Conceptually:

```text
if management command:
    execute service-management operation
    return exit code
else:
    run normal Generic Host
```

Requirements:

- management commands must not build/start the automatic-resume hosted pipeline unnecessarily;
- normal host startup remains unchanged;
- unknown management commands fail clearly;
- help/usage text may be minimal.

## Exit Codes

Use deterministic process exit codes.

At minimum:

```text
0 = success
1 = command/operation failure
2 = invalid command or arguments
```

If existing repository conventions define exit codes, use those instead and document them.

## Platform Boundary

On non-Windows platforms:

- normal host behavior may remain available if supported;
- Windows service-management commands must fail clearly and safely;
- tests must not depend on the actual operating system.

## Security

- No elevation bypass.
- No credential storage.
- No service-account password handling.
- No arbitrary command execution.
- No user-provided executable name.
- No command-line interpolation into a shell.
- Do not log secrets.

Administrator privileges may be required by Windows; surface the resulting failure normally.

## Tests

Use fakes/stubs for Windows service management and process execution.

No test may install, start, stop, query, or delete a real Windows service.

Cover at least:

### Command dispatch

1. No management command follows normal host path.
2. `install` dispatches exactly once to install.
3. `uninstall` dispatches exactly once to uninstall.
4. `start` dispatches exactly once to start.
5. `stop` dispatches exactly once to stop.
6. `status` dispatches exactly once to status.
7. Management command does not start automatic-resume host.
8. Unknown command returns invalid-command exit code.
9. Extra invalid arguments fail clearly.

### Install

10. Exact service identity is used.
11. Current executable path is used.
12. Install does not start service implicitly.
13. Existing conflicting service is not silently overwritten.
14. Install failure produces failure exit code.

### Uninstall

15. Uninstall targets exact service identity.
16. Absent service behavior is deterministic.
17. Uninstall does not delete application files.
18. Uninstall failure produces failure exit code.

### Start / stop

19. Start targets exact service identity.
20. Stop targets exact service identity.
21. Start failure is surfaced.
22. Stop failure is surfaced.
23. No retry/polling loop exists.

### Status

24. Installed/running maps to `Running`.
25. Installed/stopped maps to `Stopped`.
26. Absent service maps to `NotInstalled`.
27. Pending/paused states map correctly when supported.
28. Unknown platform state maps to `Unknown`.

### Platform/security

29. Non-Windows management operation fails safely.
30. No elevation bypass exists.
31. No credential/password handling exists.
32. No arbitrary shell command construction exists.
33. Process arguments are passed structurally when process execution is used.

### Architecture

34. Service-management abstraction contains no automatic-resume concepts.
35. Automatic-resume orchestration remains unchanged.
36. DEV-0037 production DI registration remains unchanged for normal host startup.
37. DEV-0036 Windows Service hosting remains enabled.
38. No Git/GitHub behavior is introduced.
39. No automatic Developer Task execution is introduced.
40. No timer/cron/retry/polling is introduced.

### Regression

41. Existing DEV-0002 through DEV-0037 tests continue to pass.

Avoid unrelated refactoring.

## Out of Scope

Do not implement:

- graphical installer;
- MSI/WiX packaging;
- service-account credential management;
- automatic elevation/UAC bypass;
- Windows Service recovery policy;
- automatic restart policy;
- delayed-auto-start configuration;
- systemd installation;
- Linux daemon management;
- remote machine service management;
- notifications;
- automatic Git operations;
- automatic GitHub PR creation/merge;
- automatic next Developer Task selection;
- Codex execution;
- self-update;
- distributed locking.

These belong to later Developer Tasks.

## Verification

Run:

```text
dotnet build
dotnet test
git diff --check
```

Required:

- build succeeds with 0 errors and no new DEV-0038 warnings;
- all tests pass;
- no whitespace errors;
- executable host builds successfully.

Do not install or modify a real Windows Service during verification.

## Acceptance Criteria

DEV-0038 is complete when:

1. Explicit `install`, `uninstall`, `start`, `stop`, and `status` management commands exist.
2. Normal execution still starts the existing Generic Host.
3. Management commands do not start the automatic-resume host.
4. Windows service management is isolated behind a mockable abstraction.
5. Production Windows implementation uses a safe, established SCM mechanism.
6. Service identity remains stable and host-specific.
7. Install uses the current executable path.
8. Install does not silently overwrite a conflicting service.
9. Start/stop contain no retry or polling.
10. Status returns a normalized service state.
11. Non-Windows management commands fail clearly and safely.
12. Deterministic exit codes are used.
13. No real service is modified by tests.
14. No elevation bypass, credential handling, or arbitrary shell construction is introduced.
15. Existing automatic-resume orchestration and production DI behavior remain unchanged.
16. No Git/GitHub, timer, cron, retry, polling, or automatic Developer Task behavior is introduced.
17. Existing tests continue to pass.
18. `dotnet build` succeeds.
19. `dotnet test` succeeds.
20. `git diff --check` succeeds.
21. `docs/developer-reviews/REVIEW-0038.md` is created.

## Codex Completion Protocol

After implementation and verification:

1. Do not create a Git commit.
2. Do not push changes.
3. Do not modify this Developer Task.
4. Create `docs/developer-reviews/REVIEW-0038.md`.
5. Use:

```text
# REVIEW-0038 – Windows Service Installation Command

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

## Deviations from DEV-0038

## Open Issues / Known Limitations

## Commit and Push
No commit created.
No push performed.
```

6. Use `READY FOR REVIEW` only if all acceptance criteria and verification succeed; otherwise `BLOCKED`.
7. Record build warning/error counts, test passed/failed/skipped counts, and diff-check result.
8. List every created/modified/deleted file.
9. Write `None` when there are no deviations or open issues.
