# DEV-0045 – Operational Health Diagnostics

## Goal

Add a small, read-only operational diagnostics command for `TrailTrainer.Developer` that answers whether the installed runtime is ready for normal operation without changing system state.

This is a deliberate v1 hardening step after the Windows Service lifecycle commands. It must reuse existing boundaries and avoid becoming a general monitoring framework.

## Scope

Add:

`TrailTrainer.Developer.Host health`

The command reports a deterministic health result based on immediately available runtime/service information.

At minimum check:

1. Windows Service installation/status through the existing service-management abstraction.
2. Whether the service is in a stable operational state.
3. Whether required production runtime composition can be created without starting the automatic-resume worker.
4. Whether required lifecycle/persistence dependencies can be resolved according to existing production registration.

The command is read-only.

## Normalized Result

Use a small result such as:

```text
Healthy
Degraded
Unhealthy
```

and provide concise diagnostic reasons.

Suggested semantics:

- `Healthy`: service installed and in a stable expected state, required production dependencies resolve.
- `Degraded`: runtime dependencies resolve but service is installed in a non-running stable state when running is expected.
- `Unhealthy`: service absent, unsupported/transitional state, status failure, or required production composition cannot be resolved.

If repository architecture indicates more appropriate semantics, preserve the same small deterministic intent and document the choice.

## Requirements

- Extend the existing management-command dispatcher.
- `health` must not start the Generic Host or automatic-resume pipeline.
- Do not start, stop, restart, install, uninstall, provision, deprovision, or reconfigure the service.
- Reuse existing service status abstraction.
- Reuse existing production DI registration/composition rather than duplicating dependency construction.
- If validating DI, create/dispose only the minimum safe service provider/scope needed; do not execute hosted workers.
- Do not execute Developer Tasks.
- Do not access Git/GitHub.
- Do not add network probing unless an existing production dependency already requires it merely to resolve; prefer resolution-only checks.
- Do not add retry, polling, timer, background monitoring, watchdog, or health server.
- Preserve management exit codes. Recommended:
  - 0 = Healthy
  - 1 = Degraded or Unhealthy / diagnostic failure
  - 2 = invalid command/arguments
- Non-Windows service diagnostics fail or report unsupported deterministically according to the existing platform boundary.
- Output must be concise and deterministic enough for scripting.

## Tests

Cover at least:

1. `health` dispatches as a management command.
2. Generic Host is not started.
3. Automatic-resume worker is not executed.
4. Service status is queried exactly once.
5. Healthy service/runtime maps to `Healthy`.
6. Stopped stable service maps deterministically according to documented semantics.
7. NotInstalled maps to `Unhealthy`.
8. Pending states map to `Unhealthy`.
9. Unknown state maps to `Unhealthy`.
10. Status failure maps to failure/unhealthy.
11. Required production registrations are validated without running hosted work.
12. DI/composition failure is surfaced as `Unhealthy`.
13. Diagnostic operation performs no SCM mutation.
14. No Git/GitHub operation occurs.
15. No Developer Task execution occurs.
16. No retry/polling/timer/background monitor is introduced.
17. Invalid arguments return exit code 2.
18. Existing management commands remain unchanged.
19. Existing tests continue to pass.

## Architecture

DEV-0045 is a read-only operational diagnostic boundary.

Do not introduce a new general-purpose health framework unless the repository already contains one that naturally fits. Prefer the smallest implementation needed for v1 operational confidence.

Do not change Core workflow semantics, lifecycle persistence semantics, automatic-resume orchestration, or Windows Service lifecycle behavior.

## Out of Scope

- HTTP health endpoint
- Prometheus/OpenTelemetry metrics
- Windows Event Log redesign
- dashboards
- continuous monitoring
- watchdog/restart logic
- network reachability tests
- GitHub availability tests
- automatic remediation
- notifications
- installer packaging
- systemd
- automatic Developer Task execution
- Codex execution

## Verification

Run:

```text
dotnet build
dotnet test
git diff --check
```

Required: 0 errors, no new warnings, all tests pass, no whitespace errors, and no real Windows Service is modified by tests.

## Acceptance Criteria

DEV-0045 is complete when an operator can run `health` and receive a deterministic, script-friendly assessment of Windows Service/runtime readiness without mutating service state or executing the automatic-resume workflow.

## Codex Completion Protocol

Create `docs/developer-reviews/REVIEW-0045.md` with:

```text
# REVIEW-0045 – Operational Health Diagnostics
## Status
READY FOR REVIEW | BLOCKED
## Summary
## Health Semantics Implemented
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
## Deviations from DEV-0045
## Open Issues / Known Limitations
## Commit and Push
No commit created.
No push performed.
```

Do not modify this task. Do not commit or push.
