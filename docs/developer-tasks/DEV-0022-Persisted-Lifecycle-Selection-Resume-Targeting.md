# DEV-0022 – Persisted Lifecycle Selection / Resume Targeting

## Metadata

- Task ID: `DEV-0022`
- Repository: `TrailTrainer.Developer`
- Expected branch: `feature/dev-0022-persisted-lifecycle-selection`
- Review report: `docs/developer-reviews/REVIEW-0022.md`
- Depends on: `DEV-0019`, `DEV-0020`, `DEV-0021`

## Goal

Add a provider-neutral selection capability for persisted Developer lifecycle states.

DEV-0021 can enumerate persisted lifecycle states, while DEV-0020 can resume a known TaskId. DEV-0022 connects those capabilities by selecting one persisted lifecycle state according to explicit caller criteria and producing a clear resume target.

This task is selection and targeting only. It must not resume a lifecycle, poll CI, merge Pull Requests, mutate Git repositories, delete persisted state, or schedule work.

## Codex Execution Instructions

Work this Developer Task completely.

- Follow `docs/architecture/README.md`.
- Implement only the scope defined here.
- Reuse `IDeveloperLifecycleStateDiscovery` from DEV-0021.
- Reuse existing persisted lifecycle state models from DEV-0019/DEV-0020.
- Keep provider-neutral contracts/models in `TrailTrainer.Developer.Core`.
- Put selection/orchestration logic in `TrailTrainer.Developer.Tasks`.
- Do not add filesystem, JSON, Git, GitHub REST, HTTP, process, shell, polling, delay, retry, scheduling, or background behavior.
- Do not modify this Developer Task or architecture documentation.
- Do not implement anything under **Out of Scope**.
- Do not create a Git commit for DEV-0022.
- Do not push the DEV-0022 implementation branch.
- After implementation and verification create `docs/developer-reviews/REVIEW-0022.md`.

If ambiguity prevents correct completion, do not invent behavior. Document it and set the review status to `BLOCKED`.

## Scope

Implement explicit selection of one persisted lifecycle state from DEV-0021 discovery results.

Supported selection modes:

1. exact TaskId,
2. oldest persisted state,
3. newest persisted state.

The result must either identify one exact persisted lifecycle state or return a clear not-found outcome.

Do not silently choose an arbitrary state.

## Selection Mode

### PersistedLifecycleSelectionMode

Add a strongly typed enum with exactly:

- `ExactTaskId`
- `Oldest`
- `Newest`

## Selection Request

### PersistedLifecycleSelectionRequest

Add an immutable provider-neutral request exposing:

- `Mode`
- optional `TaskId`

Validation:

### ExactTaskId

- TaskId is required,
- TaskId must not be empty or whitespace.

### Oldest / Newest

- TaskId must be null.

Reject unsupported enum values.

Do not accept Pull Request number, branch names, repository identity, or filesystem paths.

## Selection State

### PersistedLifecycleSelectionState

Add a strongly typed enum with exactly:

- `Found`
- `NotFound`

## Selection Result

### PersistedLifecycleSelectionResult

Add an immutable provider-neutral result exposing:

- `State`
- optional `DeveloperLifecyclePersistedState PersistedState`

Invariants:

- `Found` requires `PersistedState`.
- `NotFound` requires `PersistedState == null`.

Do not duplicate nested state fields.

## Resume Target

### PersistedLifecycleResumeTarget

Add an immutable provider-neutral model exposing:

- `TaskId`
- `DeveloperLifecyclePersistedState PersistedState`

Validation:

- TaskId must be non-empty,
- PersistedState must not be null,
- TaskId must equal `PersistedState.TaskId` using ordinal comparison.

Do not add merge method or delete-remote settings here; those remain caller inputs to DEV-0020 Resume.

## Core Abstraction

### IPersistedLifecycleSelector

Add a mockable asynchronous provider-neutral abstraction equivalent to:

`SelectAsync(PersistedLifecycleSelectionRequest request, CancellationToken cancellationToken = default)`

It returns `PersistedLifecycleSelectionResult`.

## Concrete Selection

### PersistedLifecycleSelector

Implement in `TrailTrainer.Developer.Tasks`.

Inject only:

- `IDeveloperLifecycleStateDiscovery`

Do not instantiate concrete discovery or persistence implementations.

## Exact TaskId Selection

Required behavior:

1. call discovery exactly once,
2. compare `state.TaskId` to requested TaskId using ordinal comparison,
3. if exactly one match exists, return Found with that exact state,
4. if no match exists, return NotFound,
5. if more than one match exists, fail clearly as inconsistent discovery data.

Do not normalize TaskId casing.

## Oldest Selection

Select the state with the earliest `SavedAtUtc`.

Tie-break:

- lowest TaskId by ordinal comparison.

If discovery returns no states, return NotFound.

The result must not depend on discovery ordering.

## Newest Selection

Select the state with the latest `SavedAtUtc`.

Tie-break:

- highest TaskId by ordinal comparison.

If discovery returns no states, return NotFound.

The result must not depend on discovery ordering.

## No Mutation

DEV-0022 must not save, delete, rewrite, resume, start, merge, or otherwise mutate lifecycle/Git/GitHub state.

## Cancellation

Pass the caller's `CancellationToken` to `IDeveloperLifecycleStateDiscovery.ListAsync`.

Cancellation must propagate and must not be converted into NotFound.

## Error Handling

Fail clearly for:

- null request,
- invalid mode/TaskId combination,
- unsupported enum value,
- discovery exception,
- duplicate exact TaskId matches,
- inconsistent resume-target construction.

NotFound is not an exception.

## Tests

Use injected fakes/stubs only. No test may require filesystem, JSON, Git, GitHub, network, or child processes.

Cover at least:

1. ExactTaskId requires TaskId.
2. ExactTaskId rejects empty TaskId.
3. ExactTaskId rejects whitespace TaskId.
4. Oldest rejects non-null TaskId.
5. Newest rejects non-null TaskId.
6. Unsupported mode rejected.
7. Null request rejected before discovery.
8. Found requires persisted state.
9. NotFound rejects persisted state.
10. Resume target requires non-empty TaskId.
11. Resume target requires persisted state.
12. Resume target rejects ordinal TaskId mismatch.
13. Discovery called exactly once.
14. Cancellation token delegated exactly.
15. Discovery exception propagates.
16. Exact matching TaskId returns Found.
17. Exact match preserves exact state object identity.
18. Case-distinct TaskId does not match.
19. Missing TaskId returns NotFound.
20. Duplicate exact matches fail clearly.
21. Exact selection is independent of input ordering.
22. Empty discovery returns NotFound for Oldest.
23. Oldest timestamp is selected.
24. Oldest tie-break uses lowest ordinal TaskId.
25. Oldest selection is independent of input ordering.
26. Oldest Found preserves exact state identity.
27. Empty discovery returns NotFound for Newest.
28. Newest timestamp is selected.
29. Newest tie-break uses highest ordinal TaskId.
30. Newest selection is independent of input ordering.
31. Newest Found preserves exact state identity.
32. Selector performs no state-store mutation.
33. Selector does not invoke DEV-0020 Resume.
34. Selector does not invoke lifecycle Start.
35. No retry occurs.
36. Pre-cancelled selection propagates cancellation.
37. Cancellation does not return NotFound.
38. Existing DEV-0002 through DEV-0021 tests continue to pass.

Avoid unrelated refactoring.

## Out of Scope

Do not implement automatic Resume invocation, DEV-0020 orchestration calls, state deletion, state saving, filesystem discovery changes, JSON changes, repository/branch/PR filtering, interactive prompting, CLI/UI, fuzzy matching, regex selection, CI polling, timers, scheduling, background workers, automatic next Developer Task selection, or Codex execution.

## Verification

Run:

```text
dotnet build
```

Required:

- 0 errors,
- no new warnings caused by DEV-0022.

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

DEV-0022 is complete when:

1. `PersistedLifecycleSelectionMode` exists with exactly ExactTaskId, Oldest, and Newest.
2. `PersistedLifecycleSelectionRequest` exists with enforced invariants.
3. `PersistedLifecycleSelectionState` exists with Found and NotFound.
4. `PersistedLifecycleSelectionResult` exists with enforced invariants.
5. `PersistedLifecycleResumeTarget` exists and validates TaskId consistency.
6. `IPersistedLifecycleSelector` exists as a mockable asynchronous Core abstraction.
7. Concrete selector exists in Tasks.
8. Selector reuses DEV-0021 discovery.
9. Discovery is called exactly once per selection.
10. Exact TaskId selection uses ordinal comparison.
11. Missing exact TaskId returns NotFound.
12. Duplicate exact TaskId matches fail clearly.
13. Oldest uses earliest SavedAtUtc then lowest ordinal TaskId.
14. Newest uses latest SavedAtUtc then highest ordinal TaskId.
15. Selection correctness does not depend on discovery ordering.
16. Found results preserve exact selected state object identity.
17. Selector performs no mutation and invokes no lifecycle Resume/Start.
18. Cancellation is propagated.
19. Tests use injected fakes only.
20. Existing tests continue to pass.
21. `dotnet build` succeeds.
22. `dotnet test` succeeds.
23. `git diff --check` succeeds.
24. No out-of-scope functionality is implemented.
25. `docs/developer-reviews/REVIEW-0022.md` is created.

## Codex Completion Protocol

After implementation and verification:

1. Do **not** create a Git commit.
2. Do **not** push changes.
3. Do **not** modify this Developer Task.
4. Create `docs/developer-reviews/REVIEW-0022.md`.
5. The review report must contain:

```text
# REVIEW-0022 – Persisted Lifecycle Selection / Resume Targeting

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

## Deviations from DEV-0022

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

The review report is part of DEV-0022 and must be included in the later Pull Request.
