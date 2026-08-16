# DEV-0033 – Hosted Automatic Resume Service Adapter

## Metadata

- Task ID: `DEV-0033`
- Repository: `TrailTrainer.Developer`
- Expected branch: `feature/dev-0033-hosted-automatic-resume-service-adapter`
- Review report: `docs/developer-reviews/REVIEW-0033.md`
- Depends on: `DEV-0032`

## Goal

Add the first .NET hosting adapter for the automatic resume pipeline.

DEV-0032 introduced `IAutomaticResumeWorker` as a host-neutral invocation boundary. DEV-0033 connects that boundary to the .NET hosting model without moving orchestration logic into the host adapter.

The hosted adapter must execute `IAutomaticResumeWorker` exactly once when the host starts and then complete.

DEV-0033 must not introduce recurring scheduling, timers, polling, retry, configuration-file loading, Windows Service/systemd integration, CLI commands, persistence access, Git operations, or GitHub calls.

## Codex Execution Instructions

Work this Developer Task completely.

- Follow `docs/architecture/README.md`.
- Implement only the scope defined here.
- Reuse `IAutomaticResumeWorker`, `AutomaticResumeWorkerRequest`, and `AutomaticResumeWorkerResult` from DEV-0032.
- Add the minimum Microsoft.Extensions.Hosting dependency required by the existing solution structure.
- Keep provider-neutral contracts/models in `TrailTrainer.Developer.Core`.
- Put the host adapter in the appropriate Tasks/hosting project according to the existing architecture; do not create a new project unless required by established architecture.
- Do not duplicate DEV-0031 or DEV-0032 logic.
- Do not call DEV-0031, DEV-0029, DEV-0028, or DEV-0027 directly.
- Do not call `Task.Delay`.
- Do not add a timer or loop.
- Do not access lifecycle persistence/discovery directly.
- Do not add filesystem, JSON, Git, GitHub REST, HTTP, process, shell, cron, retry, polling, Windows-service, systemd, or CLI behavior.
- Do not modify this Developer Task or architecture documentation.
- Do not implement anything under **Out of Scope**.
- Do not create a Git commit for DEV-0033.
- Do not push the DEV-0033 implementation branch.
- After implementation and verification create `docs/developer-reviews/REVIEW-0033.md`.

If ambiguity prevents correct completion, document it and set the review status to `BLOCKED`.

## Scope

Conceptually:

```text
.NET Generic Host
       |
       v
+-------------------------------+
| HostedAutomaticResumeService |
+-------------------------------+
       |
       | exactly once
       v
+-------------------------------+
| IAutomaticResumeWorker       |
+-------------------------------+
       |
       v
      done
```

DEV-0033 is a host adapter only.

## Request Provider

### IAutomaticResumeWorkerRequestProvider

Add a provider-neutral Core abstraction equivalent to:

```text
AutomaticResumeWorkerRequest GetRequest()
```

Purpose:

- keep host adapter construction independent of configuration-file formats,
- allow tests and future composition roots to supply a request,
- avoid embedding concrete resume settings in the hosted service.

The provider must be synchronous and side-effect-free from the hosted adapter's perspective.

DEV-0033 must not implement JSON/appsettings/environment parsing.

## Hosted Adapter

### HostedAutomaticResumeService

Implement as a .NET hosted service using the simplest suitable hosting abstraction.

Prefer `IHostedService` over `BackgroundService` because DEV-0033 performs one bounded invocation and contains no long-running loop.

Inject exactly:

- `IAutomaticResumeWorker`
- `IAutomaticResumeWorkerRequestProvider`

### StartAsync

`StartAsync(CancellationToken cancellationToken)` must:

1. obtain the request exactly once from `IAutomaticResumeWorkerRequestProvider`,
2. reject a null provider result,
3. call `IAutomaticResumeWorker.RunAsync` exactly once,
4. pass the exact returned request,
5. pass the exact host cancellation token,
6. await worker completion,
7. return only after the worker has completed.

Do not:

- inspect `AutomaticResumeWorkerResult`,
- schedule another run,
- wait after completion,
- start detached/background work,
- swallow exceptions.

### StopAsync

`StopAsync(CancellationToken cancellationToken)` must:

- complete without invoking the worker,
- perform no scheduling,
- perform no delay,
- perform no persistence or cleanup orchestration.

No additional shutdown behavior is required in DEV-0033.

## Single Invocation Guarantee

For one hosted-service instance:

```text
one StartAsync call -> one request-provider call -> one worker call
```

DEV-0033 does not create a recurring worker.

If the host itself calls `StartAsync` again, each explicit host invocation may delegate once; DEV-0033 does not need to maintain cross-call state.

## Trust Boundary

DEV-0033 must treat DEV-0032 as authoritative.

It must not:

- inspect worker result state,
- inspect DEV-0031 result state,
- inspect nested runs,
- independently decide continuation,
- call lower orchestration layers directly.

## Failure Behavior

If the request provider throws:

- propagate the exact exception,
- do not invoke the worker.

If the provider returns null:

- reject it,
- do not invoke the worker.

If the worker throws:

- propagate the exact exception,
- do not retry,
- do not convert it into successful host startup.

## Cancellation

Pass the exact `StartAsync` cancellation token to `IAutomaticResumeWorker.RunAsync`.

Do not create a replacement or linked token.

Cancellation propagates unchanged.

`StopAsync` accepts its host token but does not need to delegate it anywhere in DEV-0033.

## Tests

Use injected fakes/stubs only.

No test may require real hosting infrastructure beyond constructing/calling the hosted adapter directly.

Cover at least:

### Request provider

1. Core abstraction exists.
2. Hosted service calls provider exactly once per `StartAsync`.
3. Exact provider request is passed to worker.
4. Null provider result rejected.
5. Provider exception propagates unchanged.
6. Provider failure prevents worker invocation.

### StartAsync

7. Worker invoked exactly once.
8. Exact `StartAsync` cancellation token passed to worker.
9. `StartAsync` does not complete before worker completes.
10. Worker result is not reinterpreted.
11. Worker result does not trigger another invocation.
12. No delay occurs before or after worker.
13. No detached task is created.

### Worker failures

14. Worker exception propagates unchanged.
15. Worker exception is not retried.
16. Worker cancellation propagates.
17. Cancellation is not converted into successful startup.

### StopAsync

18. `StopAsync` completes successfully.
19. `StopAsync` does not invoke worker.
20. `StopAsync` does not invoke request provider.
21. `StopAsync` performs no delay.

### Architecture

22. Hosted adapter implements `IHostedService`.
23. Hosted adapter has exactly two constructor dependencies.
24. Dependencies are exactly `IAutomaticResumeWorker` and `IAutomaticResumeWorkerRequestProvider`.
25. No direct DEV-0031 dependency.
26. No direct DEV-0029 dependency.
27. No direct DEV-0028 dependency.
28. No direct DEV-0027 dependency.
29. No `IAsyncDelay` dependency.
30. No direct `Task.Delay`.
31. No timer.
32. No polling.
33. No retry.
34. No internal loop.
35. No persistence/discovery dependency.
36. No filesystem/JSON/Git/GitHub/process behavior.
37. No Windows Service/systemd integration.
38. No CLI behavior.

### Regression

39. Existing DEV-0002 through DEV-0032 tests continue to pass.

Avoid unrelated refactoring.

## Out of Scope

Do not implement:

- recurring hosted execution,
- `BackgroundService` loop,
- periodic timer,
- cron,
- polling,
- retry/backoff,
- dynamic scheduling,
- appsettings parsing,
- environment-variable parsing,
- concrete request configuration,
- DI composition root/host builder,
- executable host application,
- Windows Service integration,
- systemd integration,
- service installation,
- automatic startup,
- persistence changes,
- filesystem/JSON changes,
- Git/GitHub operations,
- notifications,
- CLI command,
- distributed locking,
- automatic next Developer Task selection,
- Codex execution.

These belong to later Developer Tasks.

## Verification

Run:

```text
dotnet build
dotnet test
git diff --check
```

Required:

- build succeeds with 0 errors and no new DEV-0033 warnings,
- all tests pass,
- no whitespace errors.

## Acceptance Criteria

DEV-0033 is complete when:

1. `IAutomaticResumeWorkerRequestProvider` exists in Core.
2. `HostedAutomaticResumeService` implements `IHostedService`.
3. Hosted service depends exactly on `IAutomaticResumeWorker` and `IAutomaticResumeWorkerRequestProvider`.
4. `StartAsync` gets exactly one request.
5. Null provider result is rejected.
6. `StartAsync` delegates exactly once to DEV-0032.
7. Exact request and host cancellation token are delegated.
8. `StartAsync` awaits worker completion.
9. Worker result is not inspected or reinterpreted.
10. Provider and worker exceptions propagate unchanged.
11. No retry is introduced.
12. `StopAsync` performs no worker invocation or scheduling.
13. No loop, timer, polling, delay, recurring scheduling, detached task, persistence, filesystem, JSON, Git, GitHub, network, process, CLI, Windows-service, or systemd behavior is introduced.
14. No direct dependency on DEV-0031 or lower orchestration layers is introduced.
15. Tests use injected fakes/stubs.
16. Existing tests continue to pass.
17. `dotnet build` succeeds.
18. `dotnet test` succeeds.
19. `git diff --check` succeeds.
20. No out-of-scope functionality is implemented.
21. `docs/developer-reviews/REVIEW-0033.md` is created.

## Codex Completion Protocol

After implementation and verification:

1. Do **not** create a Git commit.
2. Do **not** push changes.
3. Do **not** modify this Developer Task.
4. Create `docs/developer-reviews/REVIEW-0033.md`.
5. Use:

```text
# REVIEW-0033 – Hosted Automatic Resume Service Adapter

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

## Deviations from DEV-0033

## Open Issues / Known Limitations

## Commit and Push
No commit created.
No push performed.
```

6. Use `READY FOR REVIEW` only if all acceptance criteria and verification succeed; otherwise `BLOCKED`.
7. Record build warning/error counts, test passed/failed/skipped counts, and diff-check result.
8. List every created/modified/deleted file.
9. Write `None` when there are no deviations or open issues.

The review report is part of DEV-0033 and must be included in the later Pull Request.
