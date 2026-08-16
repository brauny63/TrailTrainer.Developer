# DEV-0036 – Windows Service Hosting Integration

## Metadata

- Task ID: `DEV-0036`
- Repository: `TrailTrainer.Developer`
- Expected branch: `feature/dev-0036-windows-service-hosting-integration`
- Review report: `docs/developer-reviews/REVIEW-0036.md`
- Depends on: `DEV-0035`

## Goal

Add Windows Service hosting support to the executable automatic-resume host introduced in DEV-0035.

DEV-0035 provides the .NET Generic Host executable. DEV-0036 must allow that same executable to run under the Windows Service Control Manager using the standard .NET hosting integration, while preserving the existing automatic-resume pipeline unchanged.

DEV-0036 is an operating-system hosting adapter only. It must not introduce new workflow logic, polling, retry, scheduling, service installation scripts, Git/GitHub behavior, or automatic Developer Task execution.

## Codex Execution Instructions

Work this Developer Task completely.

- Follow `docs/architecture/README.md`.
- Implement only the scope defined here.
- Reuse the executable host from DEV-0035.
- Reuse `AddAutomaticResumePipeline()` from DEV-0034.
- Reuse `HostedAutomaticResumeService` from DEV-0033.
- Use the standard Microsoft.Extensions.Hosting Windows Service integration.
- Add only the minimum package dependency required.
- Keep `Program.cs` thin.
- Do not duplicate automatic-resume orchestration.
- Do not add timers, polling, retry, cron, custom service loops, Git, GitHub REST, process/shell behavior, or automatic Developer Task execution.
- Do not implement Windows service installation/uninstallation scripts.
- Do not modify this Developer Task or architecture documentation.
- Do not create a Git commit or push.
- After implementation and verification create `docs/developer-reviews/REVIEW-0036.md`.

If ambiguity prevents correct completion, document it and set the review status to `BLOCKED`.

## Scope

Conceptually:

```text
Windows Service Control Manager
             |
             v
+------------------------------+
| .NET Generic Host           |
| UseWindowsService()         |
+------------------------------+
             |
             v
HostedAutomaticResumeService
             |
             v
existing DEV-0032/0031 pipeline
```

No workflow behavior changes below the hosting boundary.

## Windows Service Integration

Configure the DEV-0035 executable host to use standard .NET Windows Service lifetime integration.

Use the established API for the target framework, conceptually:

```text
builder.Services.AddWindowsService(...)
```

or the equivalent supported hosting API.

Requirements:

- use official Microsoft.Extensions.Hosting.WindowsServices integration;
- configure the host to cooperate with Windows Service Control Manager;
- preserve normal console/debug execution where supported by the standard API;
- do not implement a custom `ServiceBase`;
- do not implement custom SCM interop;
- do not create a second executable.

## Service Name

Define one stable service name:

```text
TrailTrainer Developer
```

Keep the service name at the host boundary.

Do not add service-name concepts to Core or Tasks.

If the standard API supports a service-name option, configure it there.

## Program.cs

`Program.cs` must remain composition-only.

It may:

- create the application builder;
- enable Windows Service integration;
- call `AddAutomaticResumePipeline()`;
- register the DEV-0035 request provider/options;
- build and run the host.

It must not:

- inspect automatic-resume results;
- invoke worker/orchestrator methods directly;
- contain workflow branching;
- contain a custom loop;
- call `Task.Delay`;
- access persistence directly;
- invoke Git/GitHub.

## Package Dependency

Add the minimum compatible Windows Services hosting package to the executable host project.

Preferred package:

```text
Microsoft.Extensions.Hosting.WindowsServices
```

Use a version compatible with the solution target framework and existing Microsoft.Extensions packages.

Do not add unrelated packages.

## Host Lifetime and Cancellation

Windows Service stop/shutdown requests must flow through the standard Generic Host lifetime.

DEV-0036 must not create its own cancellation source or shutdown protocol.

The existing chain remains:

```text
SCM stop
  -> Generic Host cancellation
  -> HostedAutomaticResumeService
  -> IAutomaticResumeWorker
  -> existing pipeline
```

Do not intercept or reinterpret cancellation.

## Failure Behavior

- Startup failures must surface through standard Generic Host / Windows Service behavior.
- Do not swallow startup exceptions.
- Do not retry startup.
- Do not restart the process programmatically.
- Do not convert workflow failures into successful service startup.

Windows Service recovery policy is outside DEV-0036.

## Tests

Tests must not install or start a real Windows Service.

Use host/service collection inspection and test doubles.

Cover at least:

### Package / host integration

1. Executable host references the Windows Services hosting package.
2. Windows Service integration is enabled during host composition.
3. Stable service name is `TrailTrainer Developer`.
4. No custom `ServiceBase` implementation exists.
5. No custom SCM interop exists.
6. No second executable project is introduced.

### Program composition

7. Existing `AddAutomaticResumePipeline()` remains used.
8. Existing DEV-0035 request provider remains registered.
9. Program remains thin.
10. Program contains no orchestration logic.
11. Program does not invoke worker directly.
12. Program does not invoke DEV-0031/0029/0028/0027 directly.

### Pipeline preservation

13. Existing hosted adapter remains `HostedAutomaticResumeService`.
14. Existing worker remains DEV-0032 worker.
15. Windows Service integration does not duplicate hosted-service registration.
16. One pipeline registration still yields one automatic-resume hosted adapter.
17. Existing request/options behavior remains unchanged.

### Lifetime / cancellation

18. No custom cancellation-token source is introduced for service lifetime.
19. No detached worker task is introduced.
20. No custom stop loop is introduced.
21. Existing hosted-service cancellation behavior remains covered by regression tests.

### Architecture

22. No `Task.Delay`.
23. No timer.
24. No polling.
25. No retry/backoff.
26. No cron.
27. No service-installation code.
28. No filesystem/JSON parsing beyond standard host configuration already allowed by DEV-0035.
29. No Git/GitHub/network/process/shell behavior.
30. No automatic Developer Task selection/execution.
31. No workflow/business logic added to the host.

### Regression

32. Existing DEV-0002 through DEV-0035 tests continue to pass.

Avoid unrelated refactoring.

## Out of Scope

Do not implement:

- `sc.exe` invocation;
- PowerShell installation scripts;
- service creation/deletion;
- service account configuration;
- Windows Service recovery policy;
- automatic restart;
- delayed automatic service start configuration;
- event-log customization beyond standard hosting defaults;
- systemd integration;
- Linux daemon installation;
- timer;
- cron;
- polling;
- retry/backoff;
- persistence changes;
- Git/GitHub operations;
- notifications;
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

- build succeeds with 0 errors and no new DEV-0036 warnings;
- all tests pass;
- no whitespace errors;
- executable host project builds successfully.

No real Windows Service installation is required for DEV-0036 verification.

## Acceptance Criteria

DEV-0036 is complete when:

1. DEV-0035 executable host supports standard .NET Windows Service hosting.
2. Minimum Windows Services hosting package is referenced.
3. Stable service name is `TrailTrainer Developer`.
4. Windows Service integration is configured at the host boundary only.
5. No custom `ServiceBase` or SCM interop is introduced.
6. Existing automatic-resume composition remains unchanged.
7. Existing `HostedAutomaticResumeService` remains the hosted execution adapter.
8. Existing DEV-0032 worker remains the worker boundary.
9. Program remains thin and contains no orchestration logic.
10. Standard host lifetime handles service cancellation/shutdown.
11. No custom loop, timer, polling, retry, cron, or cancellation protocol is introduced.
12. No service installation/uninstallation behavior is introduced.
13. No Git, GitHub, network, process, shell, or automatic Developer Task behavior is introduced.
14. Tests do not install/start a real Windows Service.
15. Existing tests continue to pass.
16. `dotnet build` succeeds.
17. `dotnet test` succeeds.
18. `git diff --check` succeeds.
19. `docs/developer-reviews/REVIEW-0036.md` is created.

## Codex Completion Protocol

After implementation and verification:

1. Do not create a Git commit.
2. Do not push changes.
3. Do not modify this Developer Task.
4. Create `docs/developer-reviews/REVIEW-0036.md`.
5. Use:

```text
# REVIEW-0036 – Windows Service Hosting Integration

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

## Deviations from DEV-0036

## Open Issues / Known Limitations

## Commit and Push
No commit created.
No push performed.
```

6. Use `READY FOR REVIEW` only if all acceptance criteria and verification succeed; otherwise `BLOCKED`.
7. Record build warning/error counts, test passed/failed/skipped counts, and diff-check result.
8. List every created/modified/deleted file.
9. Write `None` when there are no deviations or open issues.

The review report is part of DEV-0036 and must be included in the later Pull Request.
