# DEV-0024 – Automatic Resume Candidate Selection

## Metadata

- Task ID: `DEV-0024`
- Repository: `TrailTrainer.Developer`
- Expected branch: `feature/dev-0024-automatic-resume-candidate-selection`
- Review report: `docs/developer-reviews/REVIEW-0024.md`
- Depends on: `DEV-0021`, `DEV-0022`

## Goal

Add a provider-neutral policy layer that automatically selects one persisted lifecycle state as the next resume candidate.

DEV-0021 discovers all persisted lifecycle states.
DEV-0022 supports explicit selection by ExactTaskId, Oldest, or Newest.

DEV-0024 adds a small automatic-selection policy that chooses one resume candidate from the discovered states without requiring the caller to provide an explicit selection mode.

This task is candidate selection only.

It must not resume a lifecycle, poll CI, mutate persisted state, merge Pull Requests, mutate Git repositories, or schedule work.

## Codex Execution Instructions

Work this Developer Task completely.

- Follow `docs/architecture/README.md`.
- Implement only the scope defined here.
- Reuse `IDeveloperLifecycleStateDiscovery` from DEV-0021.
- Reuse persisted lifecycle state models from DEV-0019.
- Reuse `PersistedLifecycleResumeTarget` from DEV-0022 when appropriate.
- Keep provider-neutral contracts/models in `TrailTrainer.Developer.Core`.
- Put candidate-selection policy/orchestration in `TrailTrainer.Developer.Tasks`.
- Do not add filesystem, JSON, Git, GitHub REST, HTTP, process, shell, polling, delay, retry, scheduling, or background execution.
- Do not modify this Developer Task or architecture documentation.
- Do not implement anything under **Out of Scope**.
- Do not create a Git commit for DEV-0024.
- Do not push the DEV-0024 implementation branch.
- After implementation and verification create `docs/developer-reviews/REVIEW-0024.md`.

If ambiguity prevents correct completion, do not invent behavior. Document it and set the review status to `BLOCKED`.

## Scope

Implement one automatic candidate-selection policy:

- discover all persisted lifecycle states,
- choose the oldest persisted state,
- use ordinal TaskId as deterministic tie-break,
- return a clear NotFound result when there are no states,
- expose the exact selected persisted state and resume target when found.

The policy must be deterministic and independent of discovery ordering.

## Candidate Selection State

### AutomaticResumeCandidateState

Add a strongly typed enum with exactly:

- `Found`
- `NotFound`

## Candidate Result

### AutomaticResumeCandidateResult

Add an immutable provider-neutral result exposing at least:

- `State`
- optional `DeveloperLifecyclePersistedState PersistedState`
- optional `PersistedLifecycleResumeTarget ResumeTarget`

Invariants:

### Found

- PersistedState must be present.
- ResumeTarget must be present.
- ResumeTarget.PersistedState must be the exact same object as PersistedState.
- ResumeTarget.TaskId must ordinal-equal PersistedState.TaskId.

### NotFound

- PersistedState must be null.
- ResumeTarget must be null.

Reject unsupported result-state enum values.

## Core Abstraction

### IAutomaticResumeCandidateSelector

Add a mockable asynchronous provider-neutral abstraction equivalent to:

`SelectAsync(CancellationToken cancellationToken = default)`

Return `AutomaticResumeCandidateResult`.

## Concrete Policy

### AutomaticResumeCandidateSelector

Implement in `TrailTrainer.Developer.Tasks`.

Inject only:

- `IDeveloperLifecycleStateDiscovery`

Do not instantiate concrete discovery/persistence implementations.

## Selection Policy

Required behavior:

1. call discovery exactly once,
2. if no states are returned, return NotFound,
3. select the state with the earliest `SavedAtUtc`,
4. when timestamps are equal, select the lowest TaskId using ordinal comparison,
5. create a `PersistedLifecycleResumeTarget` from the selected state,
6. preserve exact selected-state object identity.

Do not rely on DEV-0021 ordering for correctness.

Do not call DEV-0022 selector; DEV-0024 is a standalone automatic policy over discovery.

## Why Oldest First

The automatic policy intentionally chooses the oldest persisted work item first to avoid starvation and provide deterministic FIFO-like behavior.

Do not add additional heuristics in DEV-0024.

Specifically do not consider:

- Pull Request number,
- repository name,
- branch name,
- TaskId numeric value,
- GitHub status,
- number of previous attempts,
- age thresholds.

## No Mutation

DEV-0024 must not:

- save state,
- delete state,
- rewrite state,
- invoke DEV-0020,
- invoke DEV-0023,
- invoke Git or GitHub,
- alter files.

## Cancellation

Pass the exact caller `CancellationToken` to discovery.

Cancellation must propagate and must not be converted into NotFound.

## Error Handling

Fail clearly for:

- discovery exception,
- null/invalid discovery output if the abstraction contract is violated,
- inconsistent resume-target construction.

Do not retry.

NotFound is a normal result.

## Tests

Use injected fakes/stubs only.

No test may require filesystem, JSON, Git, GitHub, network, or child processes.

Cover at least:

1. Found requires PersistedState.
2. Found requires ResumeTarget.
3. Found requires exact ResumeTarget/PersistedState identity.
4. Found requires matching TaskId.
5. NotFound rejects PersistedState.
6. NotFound rejects ResumeTarget.
7. Unsupported result state rejected.
8. Discovery called exactly once.
9. Exact cancellation token delegated.
10. Discovery exception propagates.
11. Empty discovery returns NotFound.
12. Empty discovery returns null state/target.
13. One state is selected.
14. One-state result preserves exact object identity.
15. Oldest timestamp selected.
16. Equal timestamp uses lowest ordinal TaskId.
17. Selection is independent of discovery ordering.
18. Case-distinct TaskIds use ordinal tie-break semantics.
19. Selected result creates correct ResumeTarget.
20. ResumeTarget contains exact selected state.
21. Pre-cancelled selection propagates cancellation.
22. Cancellation does not return NotFound.
23. No retry occurs.
24. Selector has only discovery dependency.
25. Selector does not call a state store.
26. Selector does not invoke DEV-0020.
27. Selector does not invoke DEV-0023.
28. Selector performs no filesystem/Git/GitHub/process work.
29. Existing DEV-0002 through DEV-0023 tests continue to pass.

Avoid unrelated refactoring.

## Out of Scope

Do not implement:

- lifecycle Resume execution,
- DEV-0020 calls,
- DEV-0023 calls,
- explicit selection modes,
- alternative policies,
- priority scores,
- retry counts,
- attempt history,
- filtering by repository/branch/PR,
- CI status lookups,
- state mutation,
- persistence changes,
- filesystem/JSON changes,
- polling,
- timers,
- scheduling,
- background workers,
- CLI command,
- UI,
- automatic next Developer Task selection,
- Codex execution.

These belong to later Developer Tasks.

## Verification

Run:

```text
dotnet build
```

Required:

- 0 errors,
- no new warnings caused by DEV-0024.

Then:

```text
dotnet test
```

All tests must pass.

Also run:

```text
git diff --check
```

There must be no whitespace errors. Platform line-ending notices alone are acceptable.

## Acceptance Criteria

DEV-0024 is complete when:

1. `AutomaticResumeCandidateState` exists with exactly Found and NotFound.
2. `AutomaticResumeCandidateResult` exists with enforced invariants.
3. `IAutomaticResumeCandidateSelector` exists as a mockable asynchronous Core abstraction.
4. `AutomaticResumeCandidateSelector` exists in Tasks.
5. It depends only on DEV-0021 discovery.
6. Discovery is called exactly once.
7. Empty discovery returns NotFound.
8. Oldest SavedAtUtc is selected.
9. Equal timestamps use lowest ordinal TaskId.
10. Selection correctness does not depend on discovery ordering.
11. Exact selected-state object identity is preserved.
12. A matching `PersistedLifecycleResumeTarget` is returned when Found.
13. Cancellation is propagated.
14. No retry or mutation is introduced.
15. No DEV-0020/DEV-0023/Git/GitHub/filesystem/JSON/process logic is introduced.
16. Tests use injected fakes only.
17. Existing tests continue to pass.
18. `dotnet build` succeeds.
19. `dotnet test` succeeds.
20. `git diff --check` succeeds.
21. No out-of-scope functionality is implemented.
22. `docs/developer-reviews/REVIEW-0024.md` is created.

## Codex Completion Protocol

After implementation and verification:

1. Do **not** create a Git commit.
2. Do **not** push changes.
3. Do **not** modify this Developer Task.
4. Create:

   `docs/developer-reviews/REVIEW-0024.md`

5. The review report must contain:

```text
# REVIEW-0024 – Automatic Resume Candidate Selection

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

## Deviations from DEV-0024

## Open Issues / Known Limitations

## Commit and Push
No commit created.
No push performed.
```

6. Use `READY FOR REVIEW` only when all acceptance criteria and verification succeed.
7. Otherwise use `BLOCKED` and document the reason.
8. Record build success/failure, warning/error counts, test passed/failed/skipped counts, and `git diff --check`.
9. List every created, modified, or deleted file.
10. Write `None` when there are no deviations or open issues.

The review report is part of DEV-0024 and must be included in the later Pull Request.
