# REVIEW-0021 – Pending Lifecycle Discovery / Enumeration

## Status
READY FOR REVIEW

## Summary

Implemented provider-neutral read-only discovery of DEV-0019 lifecycle-state files. Discovery uses the exact shared JSON schema and SHA-256 filename mapping, validates filename/TaskId integrity and domain data, ignores unrelated artifacts, and returns all valid states ordered by oldest timestamp then ordinal TaskId.

## Requirements Implemented

- Added mockable asynchronous `IDeveloperLifecycleStateDiscovery` returning a read-only state collection.
- Added concrete `LocalJsonDeveloperLifecycleStateDiscovery` in the Persistence project.
- Extracted DEV-0019's private JSON DTO, serialization, reconstruction, directory, and filename logic into one internal shared helper.
- Preserved the existing store's public API, atomic-write behavior, validation, and serialization format.
- Uses explicitly configured storage-directory semantics identical to the store.
- Missing storage directory returns empty without creating it.
- Considers only top-level files with an exact lowercase 64-hex SHA-256 plus `.lifecycle.json` shape.
- Ignores temporary, backup, unrelated JSON, other-extension, and nested artifacts without mutation.
- Reads/deserializes candidate files asynchronously with cancellation.
- Reconstructs and validates the exact DEV-0019 domain model rather than duplicating domain validation.
- Recomputes the deterministic exact-case TaskId filename and rejects mismatches.
- Detects duplicate reconstructed TaskIds defensively using ordinal comparison.
- Malformed or invalid candidate state fails the entire operation without partial successful return or file modification.
- Sorts by `SavedAtUtc` ascending, then TaskId with ordinal comparison.
- Returns a defensively read-only collection.
- Performs no create, write, delete, rename, repair, cleanup, orchestration, Git, GitHub, HTTP, process, polling, delay, retry, scheduling, or background behavior.

## Files Created

- `src/TrailTrainer.Developer.Core/IDeveloperLifecycleStateDiscovery.cs`
- `src/TrailTrainer.Developer.Persistence/LifecycleStateJsonFormat.cs`
- `src/TrailTrainer.Developer.Persistence/LocalJsonDeveloperLifecycleStateDiscovery.cs`
- `tests/TrailTrainer.Developer.Tests/LocalJsonDeveloperLifecycleStateDiscoveryTests.cs`
- `docs/developer-reviews/REVIEW-0021.md`

## Files Modified

- `src/TrailTrainer.Developer.Persistence/LocalJsonDeveloperLifecycleStateStore.cs`

## Files Deleted

None.

## Architecture / Refactoring Notes

The Core abstraction exposes only provider-neutral persisted states. Concrete filesystem enumeration remains in Persistence. A small internal `LifecycleStateJsonFormat` helper guarantees the store and discovery use exactly the same schema, reconstruction rules, extension, and deterministic hash mapping without expanding the public API. Duplicate TaskIds are checked defensively; with exact SHA-256 filename integrity they are only constructible through a hash collision or equivalent corruption, which would otherwise fail filename integrity.

## Tests Added

- Invalid and whitespace storage-directory configuration.
- Missing directory empty result without creation and existing empty directory behavior.
- One/multiple-state exact round-trip and externally read-only collection.
- Timestamp ascending and ordinal TaskId tie-break ordering independent of creation order.
- Unrelated files, unrelated JSON, temporary files, backups, nested directories, and non-lifecycle extensions ignored and retained.
- Malformed JSON, missing required field, invalid PR number, equal branches, and non-UTC timestamp failure without rewrite/delete.
- Exact-case filename/TaskId mismatch detection without rename/delete.
- Corruption preventing any partial successful result.
- Case-distinct TaskIds remaining distinct.
- Discovery not modifying final state bytes or creating files.
- Pre-cancelled enumeration propagation.
- DEV-0019 save discoverability, replacement appearing once with latest values, and deletion removing discovery results.
- All existing DEV-0019 store and full regression tests remain passing.

## Verification

### dotnet build

Successful. 0 warnings, 0 errors.

### dotnet test

Successful. 415 passed, 0 failed, 0 skipped.

### git diff --check

Successful. No whitespace errors. Platform line-ending notice only.

## Deviations from DEV-0021

None.

## Open Issues / Known Limitations

None.

## Commit and Push
No commit created.
No push performed.
