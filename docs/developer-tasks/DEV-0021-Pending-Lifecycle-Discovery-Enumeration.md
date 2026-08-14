# DEV-0021 – Pending Lifecycle Discovery / Enumeration

## Metadata

- Task ID: `DEV-0021`
- Repository: `TrailTrainer.Developer`
- Expected branch: `feature/dev-0021-pending-lifecycle-discovery`
- Review report: `docs/developer-reviews/REVIEW-0021.md`
- Depends on: `DEV-0019`, `DEV-0020`

## Goal

Add a provider-neutral way to discover and enumerate persisted Developer lifecycle states that are currently available for later resume.

DEV-0019 can persist individual lifecycle states by TaskId.
DEV-0020 can resume one known TaskId.
DEV-0021 adds read-only discovery so callers can list the persisted states without already knowing every TaskId.

This task is discovery only.

It must not resume lifecycles, poll CI, merge Pull Requests, mutate Git repositories, delete persisted states, or schedule work.

## Codex Execution Instructions

Work this Developer Task completely.

- Follow `docs/architecture/README.md`.
- Implement only the scope defined here.
- Reuse `DeveloperLifecyclePersistedState` from DEV-0019.
- Reuse the existing persistence project for filesystem-backed discovery.
- Keep provider-neutral contracts/models in `TrailTrainer.Developer.Core`.
- Keep concrete JSON/file discovery in `TrailTrainer.Developer.Persistence`.
- Do not add orchestration logic to discovery.
- Do not add HTTP, Git, GitHub REST, process, shell, polling, delay, retry, scheduling, or background execution.
- Do not modify this Developer Task or architecture documentation.
- Do not implement anything under **Out of Scope**.
- Do not create a Git commit for DEV-0021.
- Do not push the DEV-0021 implementation branch.
- After implementation and verification create `docs/developer-reviews/REVIEW-0021.md`.

If an ambiguity prevents correct completion, do not invent behavior. Document it and set the review status to `BLOCKED`.

## Scope

Implement read-only lifecycle-state discovery with:

1. asynchronous enumeration of all persisted lifecycle states,
2. deterministic ordering,
3. strict validation of discovered files,
4. isolation from unrelated files in the storage directory,
5. cancellation support,
6. no mutation.

## Core Abstraction

### IDeveloperLifecycleStateDiscovery

Add a mockable asynchronous provider-neutral abstraction with an operation equivalent to:

`ListAsync(CancellationToken cancellationToken = default)`

Return a read-only collection of `DeveloperLifecyclePersistedState`.

The abstraction must not expose:

- filesystem paths,
- JSON DTOs,
- file handles,
- directory entries,
- persistence implementation details.

## Concrete Implementation

### LocalJsonDeveloperLifecycleStateDiscovery

Implement in `TrailTrainer.Developer.Persistence`.

The implementation must accept the same configured storage directory semantics used by `LocalJsonDeveloperLifecycleStateStore`.

Do not hard-code:

- repository paths,
- current directory,
- user profile,
- temp directory,
- OS-specific data directories.

The discovery implementation may be constructed separately from the store or share an internal path/serialization helper if that reduces duplication without broad refactoring.

## File Selection

Only lifecycle-state files owned by DEV-0019 must be considered.

Use the same final file extension and deterministic naming convention used by the JSON state store.

Ignore:

- temporary files,
- unrelated JSON files,
- unrelated files with other extensions,
- directories,
- backup/test artifacts not matching the lifecycle-state file pattern.

Do not delete or rename ignored files.

## Storage Directory Missing

If the configured storage directory does not exist:

- return an empty collection,
- do not create the directory.

Discovery is read-only.

## State Loading

For each discovered final lifecycle-state file:

1. open/read asynchronously,
2. deserialize using the same logical schema as DEV-0019,
3. validate the reconstructed domain model,
4. include the valid `DeveloperLifecyclePersistedState` in the result.

Do not duplicate domain validation rules unnecessarily.

If a discovered lifecycle-state file is malformed or invalid:

- fail the enumeration clearly,
- do not silently skip it,
- do not return a partial successful result,
- do not modify the invalid file.

This prevents hidden corruption.

## Filename / TaskId Integrity

The deterministic filename must correspond to the TaskId contained in the loaded state.

Recompute the expected filename mapping for the loaded `TaskId`.

If the current discovered filename does not match the expected filename exactly:

- treat the state as invalid/corrupt,
- fail clearly,
- do not silently accept or rename it.

This prevents a valid JSON payload from being placed under another TaskId's deterministic filename.

## Ordering

Return states in deterministic order by:

1. `SavedAtUtc` ascending,
2. then `TaskId` using ordinal comparison.

This order is intended to make oldest pending work visible first.

Do not depend on filesystem enumeration order.

## Duplicate TaskIds

Under the DEV-0019 deterministic mapping there should be only one final state file per TaskId.

If discovery nevertheless reconstructs duplicate TaskIds from multiple candidate files:

- fail clearly,
- do not silently choose one.

## Read-only Semantics

Discovery must not:

- create storage directory,
- write files,
- delete files,
- rename files,
- replace files,
- clean temp files,
- normalize persisted content.

## Cancellation

Propagate the caller's `CancellationToken` to asynchronous filesystem reads/deserialization where supported.

Cancellation must propagate as cancellation.

Do not convert cancellation into an empty or partial collection.

## Error Handling

Fail clearly for:

- invalid constructor storage directory,
- malformed lifecycle JSON,
- missing required fields,
- invalid persisted state,
- filename/TaskId mismatch,
- duplicate TaskId,
- filesystem access failures.

Missing storage directory is not an error.

Unrelated files are not errors.

## Optional Internal Refactoring

DEV-0019 may currently contain private helpers for:

- TaskId -> deterministic filename mapping,
- JSON DTO serialization/deserialization,
- persisted-state reconstruction.

DEV-0021 may extract a small internal shared helper inside `TrailTrainer.Developer.Persistence` if needed to guarantee identical behavior between store and discovery.

Requirements:

- behavior of existing DEV-0019 store must remain unchanged,
- no public API expansion unless required,
- no unrelated refactoring,
- existing DEV-0019 tests must remain passing.

## Tests

Use isolated temporary directories.

Tests must not require:

- Git,
- GitHub,
- network access,
- current repository state,
- global machine configuration.

Cover at least:

### Configuration / empty discovery

1. Empty storage directory argument rejected.
2. Whitespace storage directory argument rejected.
3. Missing storage directory returns empty collection.
4. Missing storage directory is not created.
5. Existing empty storage directory returns empty collection.

### Normal enumeration

6. One persisted state is returned.
7. Multiple persisted states are returned.
8. Exact TaskId values are preserved.
9. Exact TaskFilePath values are preserved.
10. Exact ResumeContext values are preserved.
11. Exact SavedAtUtc values are preserved.
12. Result collection is read-only or externally non-mutable according to project conventions.

### Ordering

13. States ordered by SavedAtUtc ascending.
14. Equal timestamps ordered by TaskId ordinal.
15. Filesystem creation/enumeration order does not affect result order.

### File filtering

16. Unrelated file is ignored.
17. Unrelated `.json` file is ignored.
18. Temporary lifecycle file is ignored.
19. Nested directory is ignored.
20. Non-lifecycle extension is ignored.
21. Discovery does not delete ignored files.

### Validation / corruption

22. Malformed lifecycle JSON fails clearly.
23. Missing required field fails clearly.
24. Invalid PR number fails clearly.
25. Equal feature/base branches fail clearly.
26. Non-UTC timestamp fails clearly.
27. Filename/TaskId mismatch fails clearly.
28. Invalid discovered state is not deleted or rewritten.
29. Enumeration does not return a partial success before corruption failure.

### Duplicate / integrity

30. Duplicate reconstructed TaskId fails clearly where constructible.
31. Deterministic filename integrity uses exact TaskId casing.
32. Distinct TaskIds remain distinct.

### Read-only behavior

33. Discovery does not create files.
34. Discovery does not modify final state files.
35. Discovery does not remove temporary files.
36. Discovery does not create the storage directory when absent.

### Cancellation

37. Pre-cancelled enumeration propagates cancellation.
38. Cancellation does not return a partial collection.

### Compatibility

39. States saved by `LocalJsonDeveloperLifecycleStateStore` are discoverable.
40. Replaced state from DEV-0019 appears once with latest values.
41. Deleted state from DEV-0019 is no longer discovered.

### Regression

42. Existing DEV-0002 through DEV-0020 tests continue to pass.

Avoid unrelated refactoring.

## Out of Scope

Do not implement:

- automatic Resume,
- invocation of DEV-0020,
- CI polling,
- wait loops,
- timers,
- scheduler,
- background service/worker,
- automatic state deletion,
- state repair,
- state migration,
- retention/expiration,
- state filtering by GitHub status,
- GitHub API calls,
- Git operations,
- merge behavior,
- cleanup behavior,
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
- no new warnings caused by DEV-0021.

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

DEV-0021 is complete when:

1. `IDeveloperLifecycleStateDiscovery` exists as a mockable asynchronous Core abstraction.
2. Concrete local JSON discovery exists in `TrailTrainer.Developer.Persistence`.
3. Discovery uses the DEV-0019 storage schema and deterministic filename mapping.
4. Missing storage directory returns empty and is not created.
5. Only final lifecycle-state files are considered.
6. Temporary and unrelated files are ignored.
7. Valid states are reconstructed exactly.
8. Filename/TaskId integrity is validated.
9. Malformed/invalid state causes clear failure rather than silent skipping.
10. No partial success is returned on corrupt lifecycle files.
11. Duplicate TaskIds fail clearly.
12. Results are deterministically ordered by SavedAtUtc then ordinal TaskId.
13. Discovery performs no filesystem mutation.
14. Cancellation is propagated.
15. States created/replaced/deleted by DEV-0019 behave correctly under discovery.
16. Existing DEV-0019 store behavior remains unchanged.
17. Tests use isolated directories and no network/Git/GitHub.
18. Existing tests continue to pass.
19. `dotnet build` succeeds.
20. `dotnet test` succeeds.
21. `git diff --check` succeeds.
22. No out-of-scope functionality is implemented.
23. `docs/developer-reviews/REVIEW-0021.md` is created.

## Codex Completion Protocol

After implementation and verification:

1. Do **not** create a Git commit.
2. Do **not** push changes.
3. Do **not** modify this Developer Task.
4. Create:

   `docs/developer-reviews/REVIEW-0021.md`

5. The review report must contain:

```text
# REVIEW-0021 – Pending Lifecycle Discovery / Enumeration

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

## Deviations from DEV-0021

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

The review report is part of DEV-0021 and must be included in the later Pull Request.
