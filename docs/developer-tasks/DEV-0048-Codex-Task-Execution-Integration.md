# DEV-0048 – Codex Task Execution Integration

## Goal

Close the real pilot gap discovered with `TrailTrainer.TerrainEngine` DEV-0007:

`TrailTrainer.Developer` can discover, parse, start, persist, complete, push, create PRs, gate, merge, and clean up Developer Tasks, but the production workflow currently never invokes Codex to perform the actual implementation work.

Add a production Codex execution boundary between task start/branch creation and review/completion processing.

This task must integrate Codex into the existing lifecycle without redesigning Git, GitHub, persistence, merge gates, or Windows Service management.

## Background / Pilot Evidence

The real pilot proved:

- initial task intake can locate the target repository and task;
- the task parser can validate the machine-readable Developer Task format;
- production workflow then reaches completion/review logic immediately;
- no feature branch implementation, code changes, or review report are produced;
- the workflow fails because the expected `REVIEW-xxxx.md` does not yet exist.

Therefore the missing production step is:

```text
Task discovered
-> task parsed
-> task started / branch created
-> Codex executes the task in the repository
-> Codex creates the review report
-> existing completion/review gate runs
-> existing push / PR / merge / cleanup continues
```

## Scope

Add a small production abstraction for Codex task execution and integrate it into the existing Developer Task lifecycle.

The implementation must:

1. invoke Codex only after the existing start preconditions succeed and the expected feature branch has been created;
2. run Codex with the target repository as working directory;
3. provide Codex the selected Developer Task path and a deterministic instruction to execute it completely;
4. wait for the Codex process to finish;
5. capture exit code and useful stdout/stderr diagnostics;
6. require a successful Codex execution before entering existing review/completion logic;
7. allow the existing review parser/gate to validate the generated review report afterward;
8. preserve lifecycle persistence/resume semantics around this new execution phase.

## Codex Invocation Contract

Create a mockable Core abstraction, for example:

- `ICodexTaskExecutor`
- `CodexTaskExecutionRequest`
- `CodexTaskExecutionResult`

Names may differ if repository conventions suggest better names.

The request should contain only what execution needs, such as:

- repository path;
- Developer Task file path;
- optional executable/configuration values supplied by Host.

The result should expose at least:

- exit code;
- success/failure;
- bounded/captured stdout;
- bounded/captured stderr.

Do not expose `Process` directly outside the concrete adapter.

## Production Adapter

Add a concrete process-based implementation in an infrastructure-appropriate project.

Preferred behavior:

- use the installed Codex CLI executable;
- invoke non-interactively if the installed CLI supports it;
- set `WorkingDirectory` to the target repository;
- pass the task instruction deterministically;
- redirect stdout/stderr;
- do not launch a shell window;
- support cancellation by terminating the child process safely;
- do not use `cmd.exe` or PowerShell unless the repository's process abstraction already requires it.

The Codex executable path/name must be configurable from Host configuration.

Recommended configuration section:

```text
CodexExecution:
  ExecutablePath
  AdditionalArguments
  Timeout
```

If the existing installed Codex CLI has a canonical executable/argument pattern already documented in the repository, use it. Otherwise make the executable path explicit and keep additional arguments small and optional.

Do not put API keys, secrets, or credentials into task files or source code.

## Execution Prompt

The production prompt must be deterministic and equivalent in intent to:

```text
Work the Developer Task at <task-path> completely.
Follow its scope, requirements, architecture constraints, verification steps,
and Codex Completion Protocol.
Do not modify the Developer Task.
Create the required review report.
Do not commit and do not push.
```

Do not duplicate the complete task contents into a second template if Codex can read the task file directly from the repository.

## Lifecycle Integration

Integrate the new execution phase into the existing workflow so that:

1. repository/task validation remains first;
2. branch creation remains owned by existing starter logic;
3. Codex executes exactly once for a fresh task start;
4. review/completion logic runs only after Codex succeeds;
5. Codex failure does not proceed to stage/commit/push/PR;
6. missing review after successful Codex execution is surfaced as a task/workflow failure, not as proof that Codex was skipped;
7. lifecycle state is persisted sufficiently to avoid accidental duplicate Codex execution during resume;
8. resume behavior is deterministic if the host/process stops after branch creation, during/after Codex execution, or before completion.

If the current lifecycle state model cannot distinguish these phases safely, add the smallest explicit state needed. Do not redesign the entire lifecycle.

## Host / Service Behavior

- Production DI must register the Codex executor.
- Windows Service must run Codex under the configured service account.
- Intake/worker exceptions caused by task execution must be logged with task/repository context.
- A task/Codex failure must not crash-loop the entire Windows Service.
- The hosted service should remain alive or terminate gracefully according to existing bounded-worker semantics, but must not throw an unhandled exception that triggers the SCM recovery loop for a normal task failure.
- Preserve health and all service-management commands.

## Review Gate Behavior

The existing review gate remains authoritative.

After Codex returns success:

- the configured `Review report` file must exist;
- it must parse successfully;
- it must satisfy the existing ready/completion gate;
- only then may existing stage/commit/push/PR behavior continue.

Do not auto-create or fabricate a review report in production code.

## Safety Requirements

- Never invoke Codex before repository cleanliness and branch preconditions are satisfied.
- Never invoke Codex on `main`.
- Never invoke Codex for a task that already has terminal completed lifecycle state.
- Never commit/push if Codex execution failed.
- Never suppress or overwrite unrelated local changes.
- Do not `git reset`, `clean`, `stash`, or force checkout to recover from Codex failure.
- Do not allow concurrent Codex execution for the same repository/task.
- No secrets in logs.
- Bound stdout/stderr retained in memory/logs.
- Bound or configure execution timeout.
- Cancellation must not leave an orphaned Codex process.

## Tests

Cover at least:

1. fresh task start invokes Codex after branch creation;
2. Codex receives correct repository working directory;
3. Codex receives the selected task path;
4. Codex is invoked exactly once for a successful fresh execution;
5. review/completion is not attempted before Codex succeeds;
6. successful Codex execution followed by valid review proceeds into existing completion flow;
7. non-zero Codex exit code blocks stage/commit/push/PR;
8. Codex startup failure is surfaced deterministically;
9. Codex cancellation terminates execution and blocks completion;
10. Codex timeout blocks completion and does not orphan the process;
11. missing review after successful Codex execution fails at the review gate;
12. invalid review after successful Codex execution fails at the existing review gate;
13. no Codex execution occurs when repository is dirty/unsafe;
14. no Codex execution occurs when starter preconditions fail;
15. no Codex execution occurs for terminal completed lifecycle state;
16. persisted/resume state prevents duplicate Codex execution after Codex already succeeded;
17. persisted/resume state can safely retry/continue when Codex never completed;
18. a normal Codex/task failure is logged and does not create an unhandled hosted-service crash loop;
19. no real Codex process is launched in tests;
20. no real GitHub call occurs in tests;
21. no Windows SCM mutation occurs in tests;
22. existing automatic-resume bounds remain effective;
23. DEV-0045 health remains unchanged;
24. all existing management commands remain unchanged;
25. production DI resolves with valid Codex configuration;
26. invalid/missing required Codex configuration fails deterministically before task execution;
27. all existing tests continue to pass.

## Architecture

Keep responsibilities separated:

- Core: execution contract/result/request;
- process adapter/infrastructure: concrete Codex CLI execution;
- Tasks: orchestration only;
- Host: configuration and DI.

Do not put raw process launching into Core or the task orchestration layer if an infrastructure boundary is available.

Reuse existing process-runner conventions where practical.

## Out of Scope

- changing the Developer Task markdown format;
- generating tasks automatically;
- multi-repository scheduling;
- parallel task execution;
- interactive Codex UI;
- ChatGPT/OpenAI API integration;
- new GitHub authentication;
- new credential storage;
- installer/MSI changes;
- service-account provisioning redesign;
- automatic task repair;
- auto-generation of review files;
- replacing existing review gates;
- replacing existing lifecycle persistence.

## Verification

Run:

```text
dotnet build
dotnet test
git diff --check
```

Required:

- 0 errors;
- no new warnings;
- all tests pass;
- no whitespace errors;
- no real Codex execution in automated tests;
- no real GitHub/network/Windows SCM side effects.

## Acceptance Criteria

DEV-0048 is complete when a production Developer Task lifecycle can:

1. discover/select a valid task;
2. create/use its expected feature branch through existing starter logic;
3. invoke configured Codex in the target repository;
4. wait for successful Codex completion;
5. consume the Codex-created review report through the existing review gate;
6. continue through the existing completion/Git/GitHub lifecycle;
7. persist enough state to avoid unsafe duplicate Codex execution;
8. surface Codex/task failures without causing an SCM crash/restart loop.

The real TerrainEngine DEV-0007 pilot should then be ready to retry without any manual Codex invocation.

## Codex Completion Protocol

Create:

`docs/developer-reviews/REVIEW-0048.md`

with:

```text
# REVIEW-0048 – Codex Task Execution Integration

## Status
READY FOR REVIEW | BLOCKED

## Summary

## Codex Execution Semantics Implemented

## Lifecycle Integration

## Failure / Resume Semantics

## Configuration Added

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

## Deviations from DEV-0048

## Open Issues / Known Limitations

## TerrainEngine Pilot Readiness
READY | NOT READY

## Commit and Push
No commit created.
No push performed.
```

Do not modify this Developer Task.

Do not create a commit.

Do not push.
