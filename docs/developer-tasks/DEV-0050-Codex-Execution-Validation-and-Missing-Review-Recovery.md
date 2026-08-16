# DEV-0050 – Codex Execution Validation and Missing Review Recovery

## Goal

Fix the production failure discovered during the real `TrailTrainer.TerrainEngine` DEV-0007 pilot after DEV-0049.

The Developer now successfully persists Codex execution state and reaches Codex execution, but the workflow records `CodexSucceeded` based on process success alone even when Codex produced no implementation and no review report. The subsequent review parser then throws `FileNotFoundException`, which escapes through host startup and terminates the Windows Service.

Make Codex success semantically meaningful, validate required postconditions before durable success, and convert missing/invalid review outcomes into controlled task failures rather than host crashes.

## Pilot Failure Evidence

Observed production sequence:

```text
DEV-0007 discovered
-> expected feature branch created
-> codex-DEV-0007.json persisted with BranchCreated
-> Codex process exits successfully
-> state persisted as CodexSucceeded
-> repository still clean
-> no ValueObject implementation exists
-> no REVIEW-0007.md exists
-> review parser attempts to open REVIEW-0007.md
-> FileNotFoundException escapes
-> host terminates
-> SCM restart loop begins
```

The key defect is that process exit code `0` is treated as sufficient proof of successful task completion.

## Scope

Correct these concrete gaps:

1. validate Codex execution output before persisting `CodexSucceeded`;
2. require the expected review report to exist before success is durable;
3. prevent missing/invalid review outcomes from terminating the Windows Service;
4. preserve safe retry semantics for incomplete Codex execution;
5. add a regression test matching the real DEV-0007 failure.

Preserve the DEV-0048/0049 architecture and existing Git/GitHub/lifecycle behavior.

## Codex Success Semantics

`CodexSucceeded` must mean that Codex completed with all required minimum postconditions satisfied.

A process exit code of `0` is necessary but not sufficient.

Before persisting `CodexSucceeded`, validate at least:

- process exit code indicates success;
- expected review report path from the parsed Developer Task exists;
- review report can be parsed using the existing review parser;
- review report identity matches the task/repository expectations;
- review status is not structurally invalid;
- repository remains on the expected task branch;
- repository safety checks still pass.

Do not fabricate or repair the review.

## Repository Change Validation

For a code-producing task, a successful Codex run should normally produce repository changes before completion.

Use existing task/review semantics to decide the smallest reliable validation. At minimum:

- if the repository remains completely unchanged from the post-branch baseline;
- and no review report exists;
- Codex must not be marked successful.

Do not hard-code `ValueObject` or DEV-0007-specific filenames into production logic.

If some legitimate tasks can produce only documentation/review changes, keep validation generic.

## Review Validation Boundary

The existing review parser and validator remain authoritative.

The workflow must validate the review report before durable Codex success or immediately as part of the same bounded success transition.

Missing review:

- is a controlled task failure;
- must include task ID and expected review path in diagnostics;
- must not propagate as an unhandled host-start exception;
- must not cause stage/commit/push/PR/merge.

Invalid review:

- remains a controlled workflow failure;
- must not be converted into success;
- must not crash the Windows Service.

## Durable State Semantics

Do not persist `CodexSucceeded` until the minimum success postconditions are validated.

If Codex exits `0` but validation fails:

- retain a retryable state such as `BranchCreated`, or the smallest explicit failure/retry state needed;
- do not falsely record success;
- do not proceed to completion;
- allow a later deterministic retry according to existing bounded execution/resume policy.

If the current state model needs one small additional phase/status, add only what is necessary.

## Retry Safety

A retry after incomplete Codex execution must:

- run only on the expected clean/safe task branch;
- never run on `main`;
- never recreate the branch;
- never delete/reset/stash/clean user work;
- never duplicate execution after a genuinely validated successful Codex run;
- preserve existing automatic-resume priority and bounds.

If Codex left repository changes but no valid review, do not silently overwrite them. Surface the repository state and block or resume only when safety rules allow.

## Hosted-Service Failure Handling

Normal task failures in Codex/review validation must not crash-loop the SCM service.

Specifically catch/translate expected workflow failures at the hosted intake/resume boundary while preserving useful diagnostics.

Do not indiscriminately swallow:

- configuration failures;
- programmer/invariant failures;
- cancellation;
- genuinely fatal host initialization errors unrelated to task execution.

The service must remain diagnosable and bounded.

## Diagnostics

Log enough context to distinguish:

- Codex process failure;
- Codex exit 0 but missing review;
- invalid review;
- repository unsafe after Codex;
- retryable incomplete execution;
- validated success.

Include:

- task ID;
- repository path/name;
- expected branch;
- expected review path;
- Codex exit code where available;
- validation phase.

Do not log secrets or full sensitive environment data.

## Tests

Add focused tests for at least:

1. exit code 0 + valid review + safe expected branch => `CodexSucceeded`;
2. exit code 0 + missing review => no `CodexSucceeded`;
3. exit code 0 + invalid review => no `CodexSucceeded`;
4. exit code non-zero => no review/completion;
5. missing review never reaches stage/commit/push/PR;
6. invalid review never reaches stage/commit/push/PR;
7. missing review is surfaced as controlled task failure;
8. hosted service does not terminate with unhandled `FileNotFoundException`;
9. retry after exit 0 + missing review remains deterministic;
10. validated `CodexSucceeded` still prevents duplicate Codex execution;
11. repository on unexpected branch blocks retry;
12. dirty/unsafe repository blocks retry without cleanup;
13. no real Codex/GitHub/network/SCM effects in tests;
14. existing DEV-0049 interrupted-start recovery still passes;
15. existing DEV-0048 execution tests still pass.

## DEV-0007 Regression

Reproduce the real production failure with fakes:

```text
clean main
-> feature branch created
-> BranchCreated persisted
-> fake Codex exits 0
-> fake Codex produces no review and no implementation
-> workflow does NOT persist CodexSucceeded
-> review completion is NOT entered
-> hosted boundary returns/logs controlled failure
-> no host crash
-> no commit/push/PR
```

Then add the success counterpart:

```text
clean main
-> feature branch created
-> BranchCreated persisted
-> fake Codex exits 0
-> valid review exists
-> success postconditions validated
-> CodexSucceeded persisted
-> existing completion flow continues
```

## Production DI

Preserve all existing DEV-0048/0049 registrations and configuration.

Do not add a second Codex execution subsystem.

## Safety Requirements

- Never mark Codex success from exit code alone.
- Never fabricate review files.
- Never auto-edit an invalid review.
- Never reset, clean, stash, force-checkout, or delete user branches.
- Never proceed to commit/push/PR after incomplete or invalid Codex output.
- Never invoke Codex on `main`.
- Never suppress unrelated local work.
- No secrets in logs.
- No real external side effects in tests.

## Architecture

Keep responsibilities separated:

- Core: minimal execution/result/state contracts;
- Tasks: post-execution validation and orchestration;
- Persistence: durable state only;
- Host: configuration, logging, process execution, hosted failure boundary.

Reuse existing review parser/validator instead of duplicating review semantics.

## Out of Scope

- changing Developer Task markdown format;
- auto-generating review reports;
- changing GitHub auth;
- changing merge behavior;
- installer/service-account redesign;
- database persistence;
- parallel Codex tasks;
- multi-repository scheduling;
- task-content heuristics specific to DEV-0007.

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
- no real Codex/GitHub/network/Windows SCM effects.

## Acceptance Criteria

DEV-0050 is complete when:

1. Codex exit code `0` alone cannot produce durable `CodexSucceeded`;
2. a missing expected review prevents success;
3. an invalid review prevents success;
4. valid review/postconditions allow success and existing completion flow;
5. missing/invalid review never reaches commit/push/PR;
6. missing/invalid review does not terminate the Windows Service with an unhandled task exception;
7. incomplete execution remains safely retryable;
8. validated success still prevents duplicate execution;
9. DEV-0049 recovery behavior remains intact;
10. all tests including the DEV-0007 regression pass.

After deployment, the real TerrainEngine DEV-0007 pilot must no longer produce `Phase: CodexSucceeded` without a valid review report.

## Codex Completion Protocol

Create `docs/developer-reviews/REVIEW-0050.md` containing:

```text
# REVIEW-0050 – Codex Execution Validation and Missing Review Recovery

## Status
READY FOR REVIEW | BLOCKED

## Summary
## Pilot Failure Reproduced
## Root Cause
## Codex Success Validation
## Review Validation Boundary
## Durable State Changes
## Retry Semantics
## Hosted-Service Failure Handling
## Diagnostics
## Requirements Implemented
## Files Created
## Files Modified
## Files Deleted
## Tests Added
## DEV-0007 Regression Test
## Verification
### dotnet build
### dotnet test
### git diff --check
## Deviations from DEV-0050
## Open Issues / Known Limitations
## TerrainEngine DEV-0007 Retry Readiness
READY | NOT READY
## Commit and Push
No commit created.
No push performed.
```

Do not modify this Developer Task.
Do not create a commit.
Do not push.
