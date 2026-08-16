# DEV-0034 – Automatic Resume Host Composition Root

## Metadata

- Task ID: `DEV-0034`
- Repository: `TrailTrainer.Developer`
- Expected branch: `feature/dev-0034-automatic-resume-host-composition-root`
- Review report: `docs/developer-reviews/REVIEW-0034.md`
- Depends on: `DEV-0033`

## Goal

Add the first .NET Generic Host composition root for the automatic-resume pipeline.

DEV-0033 introduced `HostedAutomaticResumeService` and intentionally left host-builder/DI registration out of scope. DEV-0034 now wires the existing provider-neutral abstractions and concrete implementations into `Microsoft.Extensions.DependencyInjection` so a later executable host can start the complete automatic-resume pipeline without hand-constructing the graph.

DEV-0034 is composition only. It must not add new orchestration rules, polling, retry, configuration-file parsing, Windows Service/systemd integration, CLI commands, or persistence behavior.

## Codex Execution Instructions

Work this Developer Task completely.

- Follow `docs/architecture/README.md`.
- Implement only the scope defined here.
- Reuse all existing abstractions and implementations from DEV-0025 through DEV-0033.
- Add only the minimum Microsoft.Extensions.DependencyInjection/Hosting abstractions required.
- Keep provider-neutral contracts in `TrailTrainer.Developer.Core`.
- Put DI/host registration code in `TrailTrainer.Developer.Tasks` unless the existing architecture clearly provides a better location.
- Do not create a new executable project.
- Do not add `Program.cs`.
- Do not add appsettings/environment parsing.
- Do not create concrete persistence/Git/GitHub configuration values inside the composition root.
- Do not add timers, polling, retry, cron, Windows Service, systemd, CLI, process, shell, filesystem, JSON, HTTP, or network behavior.
- Do not modify this Developer Task or architecture documentation.
- Do not create a Git commit or push.
- After implementation and verification create `docs/developer-reviews/REVIEW-0034.md`.

If ambiguity prevents correct completion, document it and set the review status to `BLOCKED`.

## Scope

Conceptually:

```text
Host builder / IServiceCollection
              |
              v
+----------------------------------+
| AddAutomaticResumePipeline(...) |
+----------------------------------+
              |
              v
 register existing abstractions
 and concrete implementations
              |
              v
 HostedAutomaticResumeService
```

The composition root must assemble existing components; it must not implement workflow logic.

## Registration API

Add an extension class and method equivalent to:

```text
IServiceCollection AddAutomaticResumePipeline(
    this IServiceCollection services)
```

Requirements:

- reject null `services`;
- return the same `IServiceCollection` instance;
- register the DEV-0030 delay implementation;
- register the DEV-0031 repeated delayed executor;
- register the DEV-0032 worker;
- register the DEV-0033 hosted service;
- register all directly required DEV-0027/DEV-0028/DEV-0029 orchestration services needed for the complete in-memory orchestration chain;
- use interface-to-concrete registrations;
- do not register provider-specific Git/GitHub/persistence implementations that require runtime configuration not available in DEV-0034;
- do not manufacture request/configuration values.

## Request Provider Boundary

`IAutomaticResumeWorkerRequestProvider` is intentionally runtime-specific.

DEV-0034 must NOT invent a concrete production request provider.

The composition root may require callers to register `IAutomaticResumeWorkerRequestProvider` separately.

Tests must verify that:

- the automatic-resume graph registers successfully,
- resolving `HostedAutomaticResumeService` fails or remains incomplete if the caller has not supplied required runtime/provider registrations,
- after test doubles for unresolved runtime/provider boundaries are supplied, the hosted service and worker pipeline can be resolved.

Do not hide missing runtime dependencies with placeholder production implementations.

## Required Registrations

Register the existing orchestration components using their abstractions where available, including at least:

- `IAsyncDelay` -> `SystemAsyncDelay`
- `IAutomaticResumeBatchStep` -> `AutomaticResumeBatchStep`
- `IAutomaticResumeBatchRunner` -> `AutomaticResumeBatchRunner`
- `IAutomaticResumeSchedulingDecision` -> `AutomaticResumeSchedulingDecisionService`
- `IAutomaticResumeRunOrchestrator` -> `AutomaticResumeRunOrchestrator`
- `IRepeatedDelayedAutomaticResumeExecutor` -> `RepeatedDelayedAutomaticResumeExecutor`
- `IAutomaticResumeWorker` -> `AutomaticResumeWorker`
- `HostedAutomaticResumeService` as hosted service

Where lower-level interfaces depend on externally configured implementations already introduced in earlier tasks, do not invent replacements. Leave those boundaries explicit.

## Hosted Service Registration

Register `HostedAutomaticResumeService` with the .NET hosting abstractions so that it is available as `IHostedService`.

Requirements:

- only one hosted-service registration is added by one call;
- its concrete implementation is `HostedAutomaticResumeService`;
- no hosted service other than the existing DEV-0033 adapter is introduced;
- no background loop is introduced.

## Lifetime Rules

Use lifetimes appropriate for stateless orchestration services.

Prefer singleton registrations for stateless orchestration components unless an existing implementation clearly requires another lifetime.

Tests must verify the selected lifetime behavior and ensure repeated resolutions do not unexpectedly create duplicate hosted-service registrations.

Do not redesign existing classes merely to support a preferred lifetime.

## Idempotency

Calling `AddAutomaticResumePipeline` twice on the same `IServiceCollection` must not create duplicate registrations for the same pipeline service where duplication would cause multiple worker/hosted-service executions.

Use `TryAdd*`, `TryAddEnumerable`, or equivalent registration techniques as appropriate.

Critical invariant:

```text
Two registration calls must not produce two HostedAutomaticResumeService instances in IEnumerable<IHostedService>.
```

## Trust Boundary

The composition root must not:

- execute any workflow;
- construct requests;
- inspect results;
- invoke Git/GitHub;
- access persistence;
- read files/environment variables;
- run commands;
- start a host.

It registers services only.

## Failure Behavior

- Null service collection -> reject.
- Missing caller-provided runtime dependencies must surface through normal DI resolution/validation; do not swallow them.
- Do not catch or transform DI exceptions.
- Do not silently register fake/default runtime dependencies.

## Tests

Use `ServiceCollection` and test doubles only.

No test may require Git, GitHub, network, filesystem, JSON, process execution, real delays, or an actual long-running host.

Cover at least:

### Extension API

1. Null IServiceCollection rejected.
2. Same IServiceCollection instance returned.
3. One call registers the automatic-resume pipeline.
4. Extension method performs no workflow execution.

### Core registrations

5. `IAsyncDelay` resolves to `SystemAsyncDelay`.
6. `IAutomaticResumeBatchStep` registration exists.
7. `IAutomaticResumeBatchRunner` resolves to `AutomaticResumeBatchRunner`.
8. `IAutomaticResumeSchedulingDecision` resolves to `AutomaticResumeSchedulingDecisionService`.
9. `IAutomaticResumeRunOrchestrator` resolves to `AutomaticResumeRunOrchestrator`.
10. `IRepeatedDelayedAutomaticResumeExecutor` resolves to `RepeatedDelayedAutomaticResumeExecutor`.
11. `IAutomaticResumeWorker` resolves to `AutomaticResumeWorker`.
12. `IHostedService` includes `HostedAutomaticResumeService`.

### Runtime boundaries

13. No concrete `IAutomaticResumeWorkerRequestProvider` is invented.
14. No fake/default persistence implementation is introduced.
15. No fake/default Git/GitHub implementation is introduced.
16. Missing runtime boundaries surface during resolution/validation.
17. Supplying test doubles for missing runtime boundaries allows graph resolution.

### Idempotency

18. Calling registration twice does not duplicate `IAsyncDelay`.
19. Calling registration twice does not duplicate worker registration.
20. Calling registration twice does not duplicate `HostedAutomaticResumeService`.
21. `IEnumerable<IHostedService>` contains exactly one DEV-0033 hosted adapter after two calls.

### Lifetimes

22. Stateless orchestration registrations use the documented selected lifetime.
23. Repeated singleton resolution returns the same instance where singleton is selected.
24. No scoped service is accidentally captured by a singleton registration.

### Architecture

25. Registration code contains no business/orchestration logic.
26. No workflow method is invoked during registration.
27. No direct Task.Delay call.
28. No timer/polling/retry.
29. No filesystem/JSON/environment parsing.
30. No Git/GitHub/network/process behavior.
31. No CLI.
32. No Windows Service/systemd integration.
33. No executable host/Program.cs introduced.
34. No new project introduced unless strictly required by existing architecture.

### Regression

35. Existing DEV-0002 through DEV-0033 tests continue to pass.

Avoid unrelated refactoring.

## Out of Scope

Do not implement:

- executable host application;
- `Program.cs`;
- `Host.CreateApplicationBuilder` entry point;
- appsettings parsing;
- environment-variable configuration;
- concrete production `IAutomaticResumeWorkerRequestProvider`;
- Windows Service integration;
- systemd integration;
- service installation;
- automatic startup;
- recurring schedule;
- timer;
- cron;
- polling;
- retry/backoff;
- persistence configuration;
- Git/GitHub credential configuration;
- CLI commands;
- notifications;
- distributed locking;
- automatic next Developer Task selection;
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

- build succeeds with 0 errors and no new DEV-0034 warnings;
- all tests pass;
- no whitespace errors.

## Acceptance Criteria

DEV-0034 is complete when:

1. `AddAutomaticResumePipeline(IServiceCollection)` exists.
2. Null collections are rejected.
3. Same collection instance is returned.
4. Existing orchestration abstractions are wired to existing concrete implementations.
5. `SystemAsyncDelay` is registered for `IAsyncDelay`.
6. DEV-0027/0028/0029 orchestration services needed by the pipeline are registered.
7. DEV-0031 executor is registered.
8. DEV-0032 worker is registered.
9. DEV-0033 adapter is registered as `IHostedService`.
10. No concrete production request provider is invented.
11. Externally configured runtime boundaries remain explicit.
12. Duplicate registration calls do not create duplicate hosted-service execution.
13. Registration has no side effects or workflow execution.
14. No new orchestration/business logic is introduced.
15. No configuration parsing, persistence, filesystem, JSON, Git, GitHub, network, process, CLI, timer, polling, retry, Windows-service, or systemd behavior is introduced.
16. Tests use `ServiceCollection` and test doubles only.
17. Existing tests continue to pass.
18. `dotnet build` succeeds.
19. `dotnet test` succeeds.
20. `git diff --check` succeeds.
21. `docs/developer-reviews/REVIEW-0034.md` is created.

## Codex Completion Protocol

After implementation and verification:

1. Do not create a Git commit.
2. Do not push changes.
3. Do not modify this Developer Task.
4. Create `docs/developer-reviews/REVIEW-0034.md`.
5. Use:

```text
# REVIEW-0034 – Automatic Resume Host Composition Root

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

## Deviations from DEV-0034

## Open Issues / Known Limitations

## Commit and Push
No commit created.
No push performed.
```

6. Use `READY FOR REVIEW` only if all criteria and verification succeed; otherwise `BLOCKED`.
7. Record build warning/error counts, test passed/failed/skipped counts, and diff-check result.
8. List every created/modified/deleted file.
9. Write `None` when there are no deviations or open issues.
