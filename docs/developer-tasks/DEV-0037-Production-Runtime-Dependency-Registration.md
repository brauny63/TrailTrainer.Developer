# DEV-0037 – Production Runtime Dependency Registration

## Metadata

- Task ID: `DEV-0037`
- Repository: `TrailTrainer.Developer`
- Expected branch: `feature/dev-0037-production-runtime-dependency-registration`
- Review report: `docs/developer-reviews/REVIEW-0037.md`
- Depends on: `DEV-0036`

## Goal

Close the production runtime dependency boundaries that were intentionally left explicit by DEV-0034 and DEV-0035.

The executable host must be able to resolve the complete automatic-resume service graph using existing production implementations for lifecycle discovery/persistence and their required runtime configuration, without test doubles.

DEV-0037 is dependency registration and configuration only. It must reuse existing production adapters and must not introduce new lifecycle semantics, orchestration rules, polling, retry, Git/GitHub workflow behavior, or automatic Developer Task execution.

## Codex Execution Instructions

Work this Developer Task completely.

- Follow `docs/architecture/README.md`.
- Inspect the existing Core, Tasks, Git, GitHub, persistence/runtime projects before implementing registrations.
- Reuse existing production implementations wherever they already exist.
- Do not invent replacement implementations merely to make DI resolve.
- Keep runtime-specific options and registrations outside Core.
- Extend the existing host/composition registration cleanly.
- Preserve DEV-0036 Windows Service hosting unchanged.
- Do not duplicate DEV-0025 through DEV-0036 orchestration logic.
- Do not add timers, polling, retry, cron, process/shell execution, notifications, or automatic Developer Task execution.
- Do not modify this Developer Task or architecture documentation.
- Do not create a Git commit or push.
- After implementation and verification create `docs/developer-reviews/REVIEW-0037.md`.

If an abstraction required by the production graph has no legitimate production implementation yet, do not create a fake/no-op implementation. Document the exact missing boundary and set the review status to `BLOCKED`.

## Scope

Conceptually:

```text
TrailTrainer.Developer.Host
          |
          v
production runtime registrations
          |
          +--> lifecycle discovery
          +--> lifecycle persistence
          +--> existing required adapters
          |
          v
AddAutomaticResumePipeline()
          |
          v
HostedAutomaticResumeService
```

The target is a DI graph that is production-resolvable using real implementations and valid configuration.

## Discovery First

Before changing code, identify:

1. every unresolved service required to resolve `HostedAutomaticResumeService`;
2. the existing production implementation, if any;
3. the project/assembly containing it;
4. its constructor dependencies;
5. the configuration values it requires.

Record the discovered production dependency chain in `REVIEW-0037.md`.

Do not assume interface or implementation names from this task when the repository uses different established names.

## Production Runtime Registration API

Add or extend a host/runtime registration extension with a clear API, conceptually:

```text
IServiceCollection AddDeveloperProductionRuntime(
    this IServiceCollection services,
    IConfiguration configuration)
```

Exact naming may follow existing repository conventions.

Requirements:

- reject null arguments;
- return the same `IServiceCollection`;
- register real existing production implementations;
- bind only configuration needed by those implementations;
- do not execute workflow behavior during registration;
- be idempotent where duplicate registration would be harmful.

## Lifecycle Discovery

Register the existing production implementation for the lifecycle-discovery abstraction required by the automatic-resume graph.

Requirements:

- use the existing implementation;
- preserve its established behavior;
- do not add discovery logic to the host;
- do not create a fake, in-memory, or no-op production discovery service.

If multiple legitimate production implementations exist, choose the one consistent with the current architecture and document the choice.

## Lifecycle Persistence

Register the existing production implementation for the lifecycle-persistence abstraction required by the automatic-resume graph.

Requirements:

- use the existing implementation;
- preserve its established persistence format and behavior;
- configure only values it actually requires;
- do not add persistence logic to Program.cs;
- do not create a fake, in-memory, or no-op production persistence service.

## Other Required Runtime Boundaries

Resolve any additional production dependencies transitively required by the automatic-resume pipeline only when:

- an established production implementation already exists, and
- registration is required for the host graph to resolve.

Examples may include existing filesystem, repository, Git, or GitHub abstractions already implemented by earlier Developer Tasks.

Do not broaden DEV-0037 into new feature development.

If a required production implementation does not exist, stop at that boundary and report `BLOCKED`.

## Configuration

Use standard .NET Generic Host configuration and options binding.

Requirements:

- configuration names follow existing repository conventions where available;
- no custom JSON parser;
- no custom environment-variable parser;
- no embedded secrets;
- no hard-coded GitHub tokens;
- no hard-coded machine-specific paths unless the repository already defines a deliberate portable default;
- validate required configuration clearly.

An optional non-sensitive `appsettings.json` may be added only if needed and architecturally appropriate.

## Program.cs

Keep `Program.cs` thin.

It may:

1. create the builder;
2. enable Windows Service integration from DEV-0036;
3. register production runtime dependencies;
4. register `AddAutomaticResumePipeline()`;
5. register the DEV-0035 request provider/options;
6. build and run the host.

It must not construct persistence/discovery implementations directly.

## DI Validation

Add tests that build a service provider with validation enabled.

With valid test configuration representing production configuration:

```text
ValidateOnBuild = true
ValidateScopes = true
```

the complete service graph required for the automatic-resume hosted service must resolve without replacing production runtime services with test doubles.

External systems must not actually be contacted merely by DI resolution.

## Side-Effect Boundary

Registration and DI resolution must not:

- read/write lifecycle state as a workflow action;
- invoke Git commands;
- call GitHub;
- start child processes;
- execute the automatic-resume worker;
- perform network calls.

Constructors used by the graph should remain side-effect-free. If an existing production implementation violates this and prevents safe DI validation, document it rather than masking the issue.

## Failure Behavior

- Missing required configuration must fail clearly.
- Invalid configuration must fail clearly.
- Missing production implementations must not be hidden.
- DI validation errors must surface normally.
- Do not catch and suppress configuration or resolution errors.

## Tests

Use temporary/inert configuration values where required, but use the actual production registrations and implementations.

Cover at least:

### Discovery

1. Required unresolved runtime boundaries are identified.
2. Existing lifecycle discovery production implementation is registered.
3. Correct interface resolves to the production discovery implementation.
4. No fake/no-op discovery implementation is introduced.

### Persistence

5. Existing lifecycle persistence production implementation is registered.
6. Correct interface resolves to the production persistence implementation.
7. Required persistence configuration is bound.
8. No fake/no-op persistence implementation is introduced.

### Runtime registration API

9. Null IServiceCollection rejected.
10. Null IConfiguration rejected.
11. Same IServiceCollection returned.
12. Registration is idempotent where required.
13. Registration performs no workflow execution.

### Complete graph

14. Production runtime registration plus `AddAutomaticResumePipeline()` can build with DI validation enabled.
15. `IAutomaticResumeWorker` resolves.
16. `HostedAutomaticResumeService` resolves through `IHostedService`.
17. `IAutomaticResumeWorkerRequestProvider` resolves using DEV-0035 host registration.
18. No runtime test doubles are required for complete graph resolution.
19. Exactly one automatic-resume hosted adapter is registered.

### Configuration

20. Missing required configuration fails clearly.
21. Invalid required configuration fails clearly.
22. Valid configuration is preserved into the appropriate options/implementation.
23. No secrets are committed.
24. No custom JSON/environment parser is introduced.

### Side effects

25. Registration performs no persistence workflow operation.
26. DI resolution performs no Git command.
27. DI resolution performs no GitHub call.
28. DI resolution performs no network request.
29. DI resolution does not execute the worker.

### Architecture

30. Program.cs remains thin.
31. Program does not instantiate discovery/persistence implementations directly.
32. Existing automatic-resume orchestration is unchanged.
33. DEV-0036 Windows Service integration remains intact.
34. No timer/polling/retry/cron is introduced.
35. No automatic Developer Task execution is introduced.
36. No fake/no-op production adapter is introduced.

### Regression

37. Existing DEV-0002 through DEV-0036 tests continue to pass.

Avoid unrelated refactoring.

## Out of Scope

Do not implement:

- new lifecycle persistence semantics;
- new lifecycle discovery semantics;
- new Git adapter behavior;
- new GitHub adapter behavior;
- GitHub credentials or token acquisition;
- automatic PR creation;
- automatic merge;
- automatic next Developer Task selection;
- Codex execution;
- timers;
- polling;
- cron;
- retry/backoff;
- notifications;
- Windows Service installation scripts;
- systemd;
- distributed locking;
- self-update.

These belong to later Developer Tasks.

## Verification

Run:

```text
dotnet build
dotnet test
git diff --check
```

Required:

- build succeeds with 0 errors and no new DEV-0037 warnings;
- all tests pass;
- no whitespace errors;
- executable host project builds;
- DI validation tests pass using production registrations.

## Acceptance Criteria

DEV-0037 is complete when:

1. The production runtime dependency chain required by the automatic-resume host is identified.
2. Existing production lifecycle discovery is registered.
3. Existing production lifecycle persistence is registered.
4. Other required existing production runtime adapters are registered only as necessary.
5. No fake/no-op production implementations are introduced.
6. Runtime registration has a clear reusable extension API.
7. Standard Generic Host configuration/options are used.
8. Missing/invalid required configuration fails clearly.
9. Program.cs remains thin.
10. DEV-0036 Windows Service integration remains unchanged.
11. Production registrations plus the automatic-resume pipeline pass DI validation.
12. The automatic-resume hosted service resolves without runtime test doubles.
13. Registration/resolution performs no workflow, Git, GitHub, process, or network side effects.
14. Existing orchestration behavior is unchanged.
15. No timer, polling, retry, cron, or automatic Developer Task execution is introduced.
16. Existing tests continue to pass.
17. `dotnet build` succeeds.
18. `dotnet test` succeeds.
19. `git diff --check` succeeds.
20. `docs/developer-reviews/REVIEW-0037.md` is created.

If criteria 2, 3, or 12 cannot be met because a genuine production implementation is absent, status must be `BLOCKED` and the missing implementation must be named precisely.

## Codex Completion Protocol

After implementation and verification:

1. Do not create a Git commit.
2. Do not push changes.
3. Do not modify this Developer Task.
4. Create `docs/developer-reviews/REVIEW-0037.md`.
5. Use:

```text
# REVIEW-0037 – Production Runtime Dependency Registration

## Status
READY FOR REVIEW | BLOCKED

## Summary

## Production Dependency Chain Discovered

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

## Deviations from DEV-0037

## Open Issues / Known Limitations

## Commit and Push
No commit created.
No push performed.
```

6. Use `READY FOR REVIEW` only if all acceptance criteria and verification succeed; otherwise `BLOCKED`.
7. Record build warning/error counts, test passed/failed/skipped counts, and diff-check result.
8. List every created/modified/deleted file.
9. Explicitly list the discovered production dependency chain.
10. Explicitly name any unresolved production boundary.
11. Write `None` when there are no deviations or open issues.
