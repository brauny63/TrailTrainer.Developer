# DEV-0049 – Codex State Persistence and Interrupted-Start Recovery

## Goal

Fix the production failure discovered during the real `TrailTrainer.TerrainEngine` DEV-0007 pilot.

The Developer created the expected feature branch, then failed while persisting the initial Codex execution state. The unhandled persistence exception terminated the Windows Service. On restart, no Codex state existed, so intake treated the task as fresh and failed because the repository was already on the task branch instead of `main`.

Make Codex state persistence robust and make this interrupted-start boundary safely recoverable without weakening repository safety.

## Pilot Failure

Observed sequence:

```text
DEV-0007 discovered
-> repository validated on main
-> feature/dev-0007-implement-valueobject created
-> initial CodexExecutionState save attempted
-> LocalJsonCodexExecutionStateStore.SaveAsync failed in File.Move
-> IOException escaped and terminated the host
-> SCM restarted the service
-> no persisted Codex state existed
-> intake treated DEV-0007 as fresh
-> starter required main
-> repository was already on feature/dev-0007-implement-valueobject
-> intake failed again
```

The failure occurred before Codex was launched. No implementation or review report was created.

## Scope

Correct two concrete gaps:

1. robust Codex execution state persistence;
2. deterministic recovery when start was interrupted after branch creation but before initial Codex state was durably recorded.

Preserve DEV-0048 architecture and existing Git/GitHub/lifecycle behavior.

## State Persistence Requirements

Harden `LocalJsonCodexExecutionStateStore`.

- Preserve JSON persistence and atomic replacement semantics.
- Never expose partially written JSON as valid state.
- Overlapping/rapid saves for the same task must not corrupt state.
- Do not use arbitrary sleeps or a global mutable static lock.
- Synchronization must be scoped appropriately to store/task/path.
- Honor cancellation.
- Existing valid state must remain intact until replacement state is durable.
- Clean temporary files after successful saves and where possible after failures.
- Remaining failures must include useful task/state-path diagnostics without secrets.
- Loading must never deserialize temporary/incomplete files.

The final path remains:

```text
codex-<TaskId>.json
```

After a save attempt, a reader must see either the previous complete state or the new complete state.

## Interrupted-Start Recovery

Handle exactly this boundary:

```text
branch creation succeeded
BUT
initial Codex execution state was not durably persisted
```

Recovery is allowed only when:

- the selected task is valid;
- its expected branch is deterministically known;
- repository identity matches;
- repository is currently on exactly that expected task branch;
- repository is otherwise safe/clean under existing status rules;
- no terminal completed lifecycle exists;
- no conflicting resumable task owns the repository;
- there is no evidence another task owns the branch/repository;
- Codex success has not already been durably recorded.

If these facts do not agree, fail visibly.

For the valid interrupted-start case, reconstruct/re-enter only the minimum state needed to continue immediately before Codex execution.

Do not recreate/delete/reset the branch, require checkout to `main`, stash, clean, overwrite work, fabricate Codex success, or fabricate a review.

After recovery, normal DEV-0048 behavior owns Codex execution and completion.

## Fresh Start

Fresh-start safety remains:

```text
clean main
-> starter validation
-> expected feature branch creation
-> durable initial Codex state
-> Codex execution
```

An arbitrary feature branch must still block fresh intake. Recovery is narrowly limited to the expected branch for the selected task.

## Ordering and Durability

Review ordering between branch creation, lifecycle persistence, Codex-state persistence, and Codex launch.

Make the smallest changes necessary for deterministic restart at these interruption points:

1. before branch creation;
2. after branch creation but before initial Codex-state persistence;
3. after initial Codex-state persistence but before Codex launch;
4. during Codex execution;
5. after Codex succeeds but before review/completion.

No interruption may cause unsafe duplicate branch creation or falsely record Codex success.

## Hosted-Service Failure Semantics

A normal task/state/recovery failure must not cause an uncontrolled Windows SCM crash/restart loop.

Log task ID, repository, phase, and useful exception context. Preserve bounded execution semantics. Do not indiscriminately swallow fatal configuration/programming errors.

## Concurrency Tests

Cover at least:

- two overlapping saves for the same task;
- repeated rapid saves for the same task;
- load after concurrent saves returns one complete valid state;
- no malformed/truncated JSON;
- no leftover temporary files after successful saves;
- independent task IDs remain correct.

Do not rely only on `Thread.Sleep` timing.

## DEV-0007 Regression Test

Reproduce the real sequence with fakes:

1. clean `main`;
2. task selected;
3. expected branch creation succeeds;
4. initial state persistence is interrupted/fails;
5. service/process is conceptually restarted;
6. repository is clean on expected feature branch;
7. no successful Codex state exists;
8. workflow recognizes interrupted-start recovery;
9. branch creation is not attempted again;
10. Codex is invoked exactly once;
11. after Codex success, normal review/completion can continue.

Also reject recovery when:

- current branch is different;
- expected branch is dirty/unsafe;
- unrelated local/untracked work exists;
- another resumable lifecycle owns execution;
- Codex success is already persisted;
- task metadata/expected branch mismatches;
- repository identity mismatches.

## Persistence Failure Tests

Verify:

- previous valid state remains readable if replacement fails;
- temporary artifacts are cleaned where possible;
- exception contains useful task/state context;
- no partial final JSON is produced;
- cancellation does not mark a transition successful.

## Production DI

Preserve DEV-0048 registrations.

`ICodexExecutionStateStore` must continue to use:

```text
DeveloperProductionRuntime:LifecycleStateStorageDirectory
```

Do not add another state directory.

## Safety Requirements

- Never reset, clean, stash, force-checkout, or delete a user branch for recovery.
- Never overwrite unrelated work.
- Never invoke Codex on `main` or an unexpected branch.
- Never fabricate persisted success.
- Never proceed to commit/push/PR after failed Codex execution.
- Never create a second task while resumable work owns the repository.
- Do not weaken existing repository-status checks.
- No real Codex, GitHub, network, or Windows SCM effects in tests.

## Architecture

Keep responsibilities separated:

- Persistence: robust JSON state storage/concurrency;
- Tasks: interrupted-start detection/orchestration;
- Core: only minimal contracts/state if required;
- Host: configuration, DI, logging.

Do not redesign the lifecycle engine.

## Out of Scope

- database persistence;
- filesystem watchers or queues;
- multi-repository scheduling;
- parallel Codex tasks;
- Developer Task format changes;
- GitHub authentication changes;
- merge behavior changes;
- installer/service-account redesign;
- automatic repository cleanup;
- automatic review generation.

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

DEV-0049 is complete when:

1. overlapping/rapid Codex-state saves cannot reproduce the observed `File.Move` race under the supported process model;
2. final JSON is always complete and parseable;
3. interruption after branch creation but before durable initial Codex state is recognized deterministically;
4. the expected clean task branch can resume without recreating it or requiring `main`;
5. arbitrary/dirty/unrelated branches remain blocked;
6. Codex executes exactly once after valid recovery;
7. persisted Codex success prevents duplicate execution;
8. normal task/recovery failures do not reproduce the uncontrolled SCM crash/restart loop;
9. DEV-0048 behavior remains valid;
10. all tests, including the DEV-0007 production regression, pass.

After deployment, the TerrainEngine DEV-0007 pilot must be retryable without manual Codex invocation.

## Codex Completion Protocol

Create `docs/developer-reviews/REVIEW-0049.md` containing:

```text
# REVIEW-0049 – Codex State Persistence and Interrupted-Start Recovery

## Status
READY FOR REVIEW | BLOCKED

## Summary
## Pilot Failure Reproduced
## Root Cause
## State Persistence Changes
## Concurrency Semantics
## Interrupted-Start Recovery
## Lifecycle / Ordering Changes
## Hosted-Service Failure Handling
## Requirements Implemented
## Files Created
## Files Modified
## Files Deleted
## Tests Added
## Regression Test for DEV-0007 Pilot
## Verification
### dotnet build
### dotnet test
### git diff --check
## Deviations from DEV-0049
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
