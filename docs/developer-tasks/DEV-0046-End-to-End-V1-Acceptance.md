# DEV-0046 – End-to-End Developer v1 Acceptance

## Goal

Prove that the existing TrailTrainer.Developer v1 building blocks work together as one deterministic end-to-end workflow, without adding new product capabilities.

This is an acceptance/integration task, not a feature-expansion task.

## Scope

Add an automated v1 acceptance test suite that exercises the existing production orchestration boundaries using fakes/test doubles for external effects.

The acceptance scenarios must cover the lifecycle from discovery of a Developer Task through the established workflow outcomes, including interruption/resume behavior and the Windows Host composition where applicable.

## Required Acceptance Scenarios

At minimum prove:

1. A valid pending Developer Task can be discovered and selected.
2. The established workflow executes the task through its existing lifecycle states.
3. Existing Git branch/commit/push/PR abstractions are invoked in the expected order for a successful workflow.
4. Existing PR status/merge gates are honored.
5. Successful merge reaches the established post-merge cleanup behavior.
6. An interrupted resumable workflow can be rediscovered and resumed through the existing automatic-resume path.
7. Bounded automatic resume does not exceed its configured step/run limits.
8. Terminal/non-resumable states are not incorrectly resumed.
9. Failure at a major external boundary is surfaced without silently advancing lifecycle state.
10. Production DI composition resolves the complete v1 workflow graph without executing real Git, GitHub, Codex, or Windows SCM effects.

## Requirements

- Prefer integration-style tests over adding production code.
- Reuse existing abstractions and test doubles.
- Do not redesign lifecycle states or workflow semantics.
- Do not add new retries, polling, background loops, commands, or service-management features.
- Do not perform real GitHub operations.
- Do not invoke real Codex.
- Do not modify a real Windows Service.
- Do not require network access.
- Do not weaken existing unit tests.
- Tests must be deterministic and isolated.
- If a minimal production refactoring is required solely to make existing composition testable, keep it behavior-preserving and document it explicitly.
- Preserve all DEV-0001 through DEV-0045 behavior.

## Test Organization

Create a clearly identifiable v1 acceptance test area/class, for example:

`DeveloperV1AcceptanceTests`

Avoid duplicating every lower-level unit test. The acceptance suite should prove composition and cross-component behavior.

## Acceptance Evidence

The tests should make it easy to see that v1 supports the intended chain:

```text
Developer Task
  -> discovery
  -> workflow execution
  -> Git boundary
  -> GitHub PR boundary
  -> status/merge gate
  -> merge
  -> cleanup
  -> terminal lifecycle state
```

and the recovery chain:

```text
persisted resumable state
  -> discovery
  -> automatic resume
  -> bounded execution
  -> terminal / blocked / still-resumable outcome
```

## Out of Scope

- new Developer workflow features
- new Windows Service commands
- new health checks
- installer/MSI
- UI
- telemetry platform
- notifications
- new GitHub capabilities
- new Codex capabilities
- real external-system acceptance tests

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
- no network dependency;
- no real GitHub/Codex/Windows SCM side effects.

## Acceptance Criteria

DEV-0046 is complete when the automated acceptance suite demonstrates that the existing TrailTrainer.Developer v1 components compose into the intended successful and resumable workflows and that major boundary failures do not corrupt lifecycle progression.

No new product capability should be introduced merely to satisfy this task.

## Codex Completion Protocol

Create `docs/developer-reviews/REVIEW-0046.md` with:

```text
# REVIEW-0046 – End-to-End Developer v1 Acceptance
## Status
READY FOR REVIEW | BLOCKED
## Summary
## Acceptance Scenarios Implemented
## Files Created
## Files Modified
## Files Deleted
## Architecture / Refactoring Notes
## Tests Added
## Verification
### dotnet build
### dotnet test
### git diff --check
## Deviations from DEV-0046
## Open Issues / Known Limitations
## V1 Acceptance Assessment
PASS | FAIL
## Commit and Push
No commit created.
No push performed.
```

Do not modify this task. Do not commit or push.
