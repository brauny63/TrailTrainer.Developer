# DEV-0035 – Automatic Resume Executable Host

## Metadata

- Task ID: `DEV-0035`
- Repository: `TrailTrainer.Developer`
- Expected branch: `feature/dev-0035-automatic-resume-executable-host`
- Review report: `docs/developer-reviews/REVIEW-0035.md`
- Depends on: `DEV-0034`

## Goal

Add the first executable .NET Generic Host for the automatic-resume pipeline.

DEV-0033 introduced the hosted adapter and DEV-0034 introduced the DI composition root. DEV-0035 now provides the executable application boundary that creates a Generic Host, registers the existing automatic-resume pipeline, supplies the required worker request provider, and runs the host.

DEV-0035 must remain a thin hosting layer. It must not duplicate orchestration logic or introduce polling, retry, recurring scheduling, Windows Service/systemd integration, Git/GitHub behavior, or automatic Developer Task execution.

## Codex Execution Instructions

Work this Developer Task completely.

- Follow `docs/architecture/README.md`.
- Implement only the scope defined here.
- Reuse `AddAutomaticResumePipeline()` from DEV-0034.
- Reuse `HostedAutomaticResumeService` from DEV-0033.
- Reuse `IAutomaticResumeWorkerRequestProvider` and existing request models.
- Add an executable host project only if no existing project is architecturally suitable.
- Use `Host.CreateApplicationBuilder(...)` or the solution's established equivalent.
- Keep `Program.cs` minimal.
- Keep request construction isolated behind `IAutomaticResumeWorkerRequestProvider`.
- Do not duplicate DEV-0031/0032/0033 orchestration logic.
- Do not add timers, polling, retry, cron, Windows Service, systemd, Git, GitHub REST, HTTP, process, shell, or automatic Developer Task execution.
- Do not modify this Developer Task or architecture documentation.
- Do not create a Git commit or push.
- After implementation and verification create `docs/developer-reviews/REVIEW-0035.md`.

If ambiguity prevents correct completion, document it and set the review status to `BLOCKED`.

## Scope

Conceptually:

```text
Program.cs
   |
   v
Host.CreateApplicationBuilder
   |
   +--> AddAutomaticResumePipeline()
   |
   +--> register IAutomaticResumeWorkerRequestProvider
   |
   v
Build
   |
   v
RunAsync
   |
   v
HostedAutomaticResumeService
```

The executable host owns composition/startup only.

## Executable Project

Create or use an executable project named consistently with the existing solution architecture.

Preferred name if a new project is required:

```text
TrailTrainer.Developer.Host
```

Requirements:

- executable .NET project;
- reference only projects required for hosting/composition;
- include the minimum Microsoft.Extensions.Hosting dependency;
- add the project to the solution;
- no business logic in the executable project.

## Program Entry Point

Add a minimal `Program.cs`.

It must:

1. create an application builder;
2. call `AddAutomaticResumePipeline()`;
3. register one concrete `IAutomaticResumeWorkerRequestProvider`;
4. build the host;
5. run the host asynchronously.

`Program.cs` must not:

- instantiate orchestration classes directly;
- inspect workflow results;
- contain retry or loop logic;
- call `Task.Delay`;
- invoke Git/GitHub;
- access lifecycle persistence directly.

## Runtime Request Provider

Add a concrete request provider for `IAutomaticResumeWorkerRequestProvider`.

Preferred name:

```text
ConfiguredAutomaticResumeWorkerRequestProvider
```

The provider belongs in the executable/hosting boundary, not Core.

Its only responsibility is to construct one valid `AutomaticResumeWorkerRequest` from explicitly supplied options.

## Options

Add a host-specific options model sufficient to construct the existing nested automatic-resume request graph.

Preferred name:

```text
AutomaticResumeHostOptions
```

The model should expose only values actually required by the existing request constructors.

Requirements:

- validate invalid/non-positive bounds and delays according to existing request invariants;
- do not duplicate domain validation unnecessarily;
- no secrets or GitHub credentials;
- no filesystem paths unless already required by an existing request model;
- no retry/polling/schedule fields beyond values already required by the existing automatic-resume request graph.

Use standard .NET configuration binding where practical.

Preferred configuration section:

```text
AutomaticResume
```

## Configuration

DEV-0035 may use normal Generic Host configuration sources already provided by .NET, including:

- command-line arguments;
- environment variables;
- optional appsettings configuration if automatically supported by the chosen Generic Host builder.

Do not implement custom JSON parsing or custom environment parsing.

Do not add secrets.

If an `appsettings.json` example/default is necessary for the executable to start, keep it minimal and non-sensitive.

## Runtime Dependency Boundary

DEV-0034 intentionally leaves externally configured lower-level runtime boundaries explicit.

DEV-0035 must not invent fake production implementations for missing Git/GitHub/persistence/runtime abstractions.

If the complete executable graph cannot yet resolve because a required production runtime adapter has not been implemented, DEV-0035 must:

- keep that dependency explicit;
- test host construction using injected test doubles where appropriate;
- document the unresolved production boundary in `REVIEW-0035.md`;
- set `BLOCKED` only if the executable host cannot be correctly implemented within the existing architecture.

Do not hide missing dependencies with no-op production implementations.

## Request Provider Behavior

`ConfiguredAutomaticResumeWorkerRequestProvider.GetRequest()` must:

- construct a valid nested request graph using the configured options;
- return a non-null `AutomaticResumeWorkerRequest`;
- create no workflow side effects;
- perform no Git/GitHub/persistence calls;
- perform no delays;
- perform no I/O beyond normal options access.

## Host Lifetime

The host uses the standard .NET host lifetime.

Cancellation from the host must flow naturally through `HostedAutomaticResumeService.StartAsync` into DEV-0032.

Do not create custom cancellation-token sources unless required by the hosting framework.

Do not create detached tasks.

## Failure Behavior

- Invalid host options must fail clearly during startup/request construction.
- DI resolution failures must surface normally.
- Worker/hosted-service exceptions must propagate through normal host startup behavior.
- Do not catch and suppress startup exceptions.
- Do not retry failed startup.

## Tests

Use host/service collections and test doubles only.

No test may require GitHub, network, real persistence, process execution, real delayed waiting, Windows Service, or systemd.

Cover at least:

### Executable project

1. Host project is executable.
2. Host project is included in the solution.
3. Host project references required composition/hosting projects only.
4. `Program.cs` exists and remains thin.

### Options

5. Options model exposes only required automatic-resume values.
6. Valid options can construct a valid request graph.
7. Invalid zero/negative delay rejected.
8. Invalid zero/negative run bounds rejected.
9. Configured values are preserved into the request graph.

### Request provider

10. Concrete provider implements `IAutomaticResumeWorkerRequestProvider`.
11. `GetRequest()` returns non-null request.
12. Provider creates valid nested request graph.
13. Provider performs no workflow execution.
14. Provider performs no delay.
15. Provider performs no persistence/Git/GitHub operations.

### Host composition

16. Host builder registers `AddAutomaticResumePipeline()`.
17. Concrete request provider is registered.
18. `IHostedService` resolves to `HostedAutomaticResumeService` when required runtime test doubles are supplied.
19. Worker pipeline resolves when required runtime test doubles are supplied.
20. No duplicate hosted service is registered.

### Startup/cancellation

21. Host startup invokes the existing hosted adapter.
22. Exact host cancellation propagates through the existing adapter boundary.
23. Startup awaits the hosted worker.
24. Worker exception surfaces through startup.
25. Cancellation is not converted into successful completion.
26. No startup retry occurs.

### Architecture

27. Program contains no orchestration/business logic.
28. Program does not instantiate worker/orchestrator classes directly.
29. No direct DEV-0031/DEV-0029/DEV-0028/DEV-0027 calls from Program.
30. No direct `Task.Delay`.
31. No timer/polling/retry/cron.
32. No Windows Service integration.
33. No systemd integration.
34. No Git/GitHub/network/process behavior added by the host.
35. No automatic Developer Task selection/execution.
36. No fake production implementation is introduced for unresolved runtime boundaries.

### Regression

37. Existing DEV-0002 through DEV-0034 tests continue to pass.

Avoid unrelated refactoring.

## Out of Scope

Do not implement:

- Windows Service integration;
- systemd integration;
- service installation;
- automatic OS startup;
- recurring external scheduling;
- timer;
- cron;
- polling;
- retry/backoff;
- GitHub credentials;
- GitHub REST operations;
- Git operations;
- persistence adapters not already implemented;
- notifications;
- automatic next Developer Task selection;
- Codex execution;
- self-update;
- distributed locking;
- multi-instance coordination.

These belong to later Developer Tasks.

## Verification

Run:

```text
dotnet build
dotnet test
git diff --check
```

Required:

- build succeeds with 0 errors and no new DEV-0035 warnings;
- all tests pass;
- no whitespace errors.

If a new executable project is added, also verify it can be built explicitly.

## Acceptance Criteria

DEV-0035 is complete when:

1. An executable automatic-resume host project exists or an existing suitable executable project is used.
2. The executable project is included in the solution.
3. Minimal `Program.cs` creates, configures, builds, and runs a Generic Host.
4. `AddAutomaticResumePipeline()` is used.
5. A concrete `IAutomaticResumeWorkerRequestProvider` exists at the host boundary.
6. Host-specific options can construct the existing automatic-resume request graph.
7. Invalid option values fail clearly.
8. Program contains no orchestration logic.
9. Existing DEV-0033 hosted adapter remains the hosting execution boundary.
10. Existing DEV-0032 worker remains the worker boundary.
11. No lower orchestration logic is duplicated.
12. Cancellation and startup exceptions flow through standard host behavior.
13. No retry, timer, polling, cron, Windows Service, systemd, Git, GitHub, network, process, or automatic Developer Task behavior is introduced.
14. No fake production runtime dependency is introduced.
15. Tests use test doubles for unresolved runtime boundaries.
16. Existing tests continue to pass.
17. `dotnet build` succeeds.
18. `dotnet test` succeeds.
19. `git diff --check` succeeds.
20. `docs/developer-reviews/REVIEW-0035.md` is created.

## Codex Completion Protocol

After implementation and verification:

1. Do not create a Git commit.
2. Do not push changes.
3. Do not modify this Developer Task.
4. Create `docs/developer-reviews/REVIEW-0035.md`.
5. Use:

```text
# REVIEW-0035 – Automatic Resume Executable Host

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

## Deviations from DEV-0035

## Open Issues / Known Limitations

## Commit and Push
No commit created.
No push performed.
```

6. Use `READY FOR REVIEW` only if all acceptance criteria and verification succeed; otherwise `BLOCKED`.
7. Record build warning/error counts, test passed/failed/skipped counts, and diff-check result.
8. List every created/modified/deleted file.
9. Explicitly document any unresolved production runtime dependencies.
10. Write `None` when there are no deviations or open issues.
