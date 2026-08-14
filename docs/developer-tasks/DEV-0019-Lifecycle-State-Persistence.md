# DEV-0019 – Lifecycle State Persistence

## Metadata

- Task ID: `DEV-0019`
- Repository: `TrailTrainer.Developer`
- Expected branch: `feature/dev-0019-lifecycle-state-persistence`
- Review report: `docs/developer-reviews/REVIEW-0019.md`
- Depends on: `DEV-0018`

## Goal

Add provider-neutral persistence for a resumable Developer lifecycle state.

DEV-0017 may stop while CI is `Pending`, and DEV-0018 can resume an already existing Pull Request when supplied with a `DeveloperLifecycleResumeContext`. DEV-0019 provides the persistence building block needed to store that resume context across process executions.

This task implements persistence only. It must not automatically resume a lifecycle, poll CI, schedule work, invoke DEV-0017/DEV-0018, or perform Git/GitHub operations.

## Codex Execution Instructions

Work this Developer Task completely.

- Follow `docs/architecture/README.md`.
- Implement only the scope defined here.
- Reuse `DeveloperLifecycleResumeContext` from DEV-0018.
- Keep persistence contracts/models in `TrailTrainer.Developer.Core`.
- Put local JSON persistence in an appropriate infrastructure project consistent with the existing architecture. If no suitable persistence project exists, prefer `TrailTrainer.Developer.Tasks` only if architecture rules permit infrastructure there; otherwise make the smallest architecture-consistent project change and document it in the review.
- Do not introduce Git, GitHub REST, HTTP, shell, process, polling, scheduling, or lifecycle orchestration logic.
- Use `System.Text.Json`.
- Persist no credentials, tokens, authorization headers, or secrets.
- Writes must be atomic from the caller's perspective.
- Do not modify this Developer Task or architecture documentation.
- Do not implement anything under **Out of Scope**.
- Do not create a Git commit for DEV-0019.
- Do not push the DEV-0019 implementation branch.
- After implementation and verification create `docs/developer-reviews/REVIEW-0019.md`.

If an ambiguity prevents correct completion, do not invent behavior. Document it and set the review status to `BLOCKED`.

## Scope

Implement:

1. an immutable persisted lifecycle-state model,
2. an asynchronous mockable state-store abstraction,
3. a local JSON-file implementation,
4. save/upsert,
5. load,
6. delete,
7. safe/atomic replacement,
8. cancellation,
9. deterministic state-file identification.

No lifecycle phase is executed by DEV-0019.

## Persisted State Model

### DeveloperLifecyclePersistedState

Add an immutable provider-neutral Core model exposing at least:

- `TaskId`
- optional `TaskFilePath`
- `ResumeContext`
- `SavedAtUtc`

Where:

- `TaskId` is a stable non-empty identifier such as `DEV-0018`.
- `TaskFilePath` may be null when unavailable.
- `ResumeContext` is the exact logical `DeveloperLifecycleResumeContext` required by DEV-0018.
- `SavedAtUtc` is a UTC timestamp.

Validation:

- `TaskId` must not be null/empty/whitespace.
- `ResumeContext` must not be null.
- `SavedAtUtc` must represent UTC (`DateTimeOffset.Offset == TimeSpan.Zero`).
- if `TaskFilePath` is supplied, whitespace-only is invalid.

Do not persist derived CI head SHA or merge expected SHA.

## Store Abstraction

### IDeveloperLifecycleStateStore

Add a mockable asynchronous Core abstraction with operations equivalent to:

- `SaveAsync(DeveloperLifecyclePersistedState state, CancellationToken cancellationToken = default)`
- `LoadAsync(string taskId, CancellationToken cancellationToken = default)`
- `DeleteAsync(string taskId, CancellationToken cancellationToken = default)`

`LoadAsync` returns `DeveloperLifecyclePersistedState?`.

Semantics:

### Save

- creates a state when none exists,
- replaces the state for the same TaskId when one already exists,
- must not leave a partially written target file visible after successful/failed replacement handling.

### Load

- returns the persisted state for the TaskId,
- returns `null` when no state exists,
- malformed persisted data fails clearly.

### Delete

- deletes the persisted state when present,
- missing state is tolerated,
- repeated delete is idempotent.

## Local JSON Store

### LocalJsonDeveloperLifecycleStateStore

Implement a local filesystem-backed JSON store.

Constructor/configuration must accept an explicit storage directory.

Do not hard-code a user profile, repository path, current directory, temp directory, or OS-specific application-data directory as the only storage location.

The storage directory may be created when required by `SaveAsync`.

## File Naming

Each TaskId maps deterministically to exactly one state file.

Do not use raw TaskId text directly as an unchecked path segment.

The mapping must prevent:

- path traversal,
- directory separators escaping the storage directory,
- invalid filename behavior,
- collisions caused by naive sanitization where reasonably avoidable.

A deterministic cryptographic hash of the exact TaskId plus a fixed state-file extension is acceptable and preferred.

Example conceptual layout:

```text
<storage>/
  <deterministic-task-id>.lifecycle.json
```

The exact filename format is an implementation detail.

## JSON Format

Use `System.Text.Json`.

Persist all information necessary to reconstruct:

- `TaskId`,
- `TaskFilePath`,
- `SavedAtUtc`,
- `DeveloperLifecycleResumeContext`,
- `GitHubRepositoryIdentity`,
- Pull Request number,
- repository directory,
- feature branch,
- base branch,
- Git remote name.

Do not persist:

- authentication tokens,
- passwords,
- authorization headers,
- HTTP clients,
- cancellation tokens,
- CI head SHA,
- expected merge SHA,
- merge response payloads.

Use an explicit persistence DTO if that avoids coupling serialization to constructor behavior.

## Round Trip

Saving and then loading must preserve the logical values exactly, including:

- TaskId casing,
- TaskFilePath,
- repository directory,
- GitHub owner/repository identity,
- Pull Request number,
- feature branch,
- base branch,
- Git remote name,
- SavedAtUtc.

Do not normalize branch names, paths, owner names, repository names, remote names, or TaskId during persistence.

## Atomic Write Behavior

Saving/replacing state must use a temporary file in the same target directory followed by an atomic or replace-style filesystem operation appropriate for the platform.

Requirements:

1. serialize/write the complete new state to a temporary file,
2. close/flush it before replacement,
3. replace/move into the final target path,
4. clean up the temporary file on failure when possible,
5. never intentionally delete the existing good state before the replacement is ready.

Do not implement Save as:

```text
delete old
write new
```

A failed save must not intentionally destroy an already valid persisted state.

## Concurrency

DEV-0019 does not need distributed locking or cross-process transactional coordination.

However:

- use unique temporary filenames,
- avoid a single fixed `.tmp` filename shared by all saves,
- do not expose partially written target JSON.

Document any same-TaskId concurrent-writer limitation in code/review if applicable.

## Storage Directory Safety

All final and temporary state files must remain inside the configured storage directory.

TaskId must never allow access to arbitrary filesystem paths.

`LoadAsync` and `DeleteAsync` must derive the path only through the same deterministic internal mapping used by `SaveAsync`.

## Cancellation

All asynchronous filesystem operations that support cancellation must receive the caller's `CancellationToken`.

Cancellation must propagate as cancellation.

Do not convert cancellation into `null`, successful deletion, or another normal result.

A cancellation/failure before final replacement must not intentionally corrupt an existing valid state.

## Error Handling

Fail clearly for:

- invalid constructor storage directory,
- null state,
- invalid TaskId,
- malformed JSON,
- missing required persisted fields,
- invalid persisted resume context,
- invalid UTC timestamp,
- filesystem access failures.

Do not include secret values in diagnostics.

Missing state on Load is not an error.
Missing state on Delete is not an error.

## Tests

Use isolated temporary directories.

Tests must not require:

- Git,
- GitHub,
- network access,
- global machine configuration,
- a specific user profile.

Cover at least:

### Model

1. Valid persisted state preserves exact values.
2. Empty TaskId rejected.
3. Whitespace TaskId rejected.
4. Null resume context rejected.
5. Non-UTC SavedAtUtc rejected.
6. Whitespace-only optional TaskFilePath rejected.
7. Null TaskFilePath accepted.

### Store configuration

8. Empty storage directory rejected.
9. Whitespace storage directory rejected.
10. Storage directory can be created by Save.

### Save/load

11. Save creates state.
12. Load returns saved state.
13. Round trip preserves TaskId exactly.
14. Round trip preserves TaskFilePath exactly.
15. Round trip preserves repository directory exactly.
16. Round trip preserves repository identity exactly.
17. Round trip preserves PR number exactly.
18. Round trip preserves feature branch exactly.
19. Round trip preserves base branch exactly.
20. Round trip preserves Git remote exactly.
21. Round trip preserves SavedAtUtc exactly.
22. Save replaces existing state for same TaskId.
23. Replacement returns latest values.
24. Different TaskIds persist independently.

### Missing/delete

25. Load missing TaskId returns null.
26. Delete existing state removes it.
27. Load after delete returns null.
28. Delete missing state succeeds.
29. Repeated delete is idempotent.

### Path safety

30. TaskId containing `../` cannot escape storage directory.
31. TaskId containing backslashes cannot escape storage directory.
32. TaskId containing slashes cannot create nested directories.
33. Unusual valid TaskId characters round-trip unchanged.
34. File mapping is deterministic for same exact TaskId.
35. Distinct representative TaskIds do not collide.

### Malformed state

36. Malformed JSON fails clearly.
37. Missing required persisted field fails clearly.
38. Invalid persisted PR number fails clearly.
39. Invalid persisted equal feature/base branches fail clearly.
40. Invalid/non-UTC persisted timestamp fails clearly.

### Atomicity / temporary files

41. Save uses a distinct temporary file before final placement where observable/testable.
42. Successful Save leaves a valid final state file.
43. Successful Save does not leave its temporary file behind.
44. Replacement does not use delete-old-before-write-new semantics.
45. Failure before replacement does not intentionally remove an existing valid state where testable.

### Cancellation

46. Pre-cancelled Save propagates cancellation.
47. Pre-cancelled Load propagates cancellation.
48. Pre-cancelled Delete propagates cancellation.

### Secrets / schema

49. Persisted JSON contains required resume values.
50. Persisted JSON contains no authorization/token/password fields introduced by this implementation.

### Regression

51. Existing DEV-0002 through DEV-0018 tests continue to pass.

Avoid unrelated refactoring.

## Out of Scope

Do not implement:

- automatic saving from DEV-0017,
- automatic loading into DEV-0018,
- lifecycle orchestration changes,
- CI polling,
- timers,
- scheduling,
- background services,
- automatic resume,
- retries,
- database persistence,
- cloud persistence,
- encryption/key management,
- credentials/tokens persistence,
- Git operations,
- GitHub REST operations,
- Pull Request discovery,
- automatic next Developer Task selection,
- Codex execution,
- CLI commands,
- retention/cleanup policies for old state files,
- multi-process locking,
- distributed transactions.

These belong to later Developer Tasks.

## Verification

Run:

```text
dotnet build
```

Required:

- 0 errors,
- no new warnings caused by DEV-0019.

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

DEV-0019 is complete when:

1. `DeveloperLifecyclePersistedState` exists as an immutable validated Core model.
2. `IDeveloperLifecycleStateStore` exists as a mockable asynchronous Core abstraction.
3. A local JSON implementation exists.
4. Storage directory is explicitly configurable.
5. Save creates and replaces state.
6. Load returns state or null when missing.
7. Delete is idempotent.
8. TaskId maps deterministically and safely to a state file.
9. TaskId cannot escape the storage directory.
10. JSON round-trip preserves all required logical values.
11. No credentials/tokens/secrets are persisted.
12. Save uses temporary-write-then-replace/move semantics.
13. Existing valid state is not intentionally deleted before replacement is ready.
14. Unique temporary files are used.
15. Malformed persisted data fails clearly.
16. Cancellation is propagated.
17. Tests use isolated temporary directories and no network/GitHub/Git.
18. Existing tests continue to pass.
19. `dotnet build` succeeds.
20. `dotnet test` succeeds.
21. `git diff --check` succeeds.
22. No out-of-scope functionality is implemented.
23. `docs/developer-reviews/REVIEW-0019.md` is created.

## Codex Completion Protocol

After implementation and verification:

1. Do **not** create a Git commit.
2. Do **not** push changes.
3. Do **not** modify this Developer Task.
4. Create:

   `docs/developer-reviews/REVIEW-0019.md`

5. The review report must contain:

```text
# REVIEW-0019 – Lifecycle State Persistence

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

## Deviations from DEV-0019

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

The review report is part of DEV-0019 and must be included in the later Pull Request.
