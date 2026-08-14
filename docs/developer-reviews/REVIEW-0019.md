# REVIEW-0019 – Lifecycle State Persistence

## Status
READY FOR REVIEW

## Summary

Implemented provider-neutral persistence contracts and a local JSON lifecycle-state store. The store safely round-trips DEV-0018 resume context, maps exact Task IDs to deterministic SHA-256 filenames, supports create/upsert/load/idempotent delete, and writes through unique same-directory temporary files before atomic replacement.

## Requirements Implemented

- Added immutable validated `DeveloperLifecyclePersistedState` with TaskId, optional TaskFilePath, exact resume context, and UTC timestamp.
- Added mockable asynchronous `IDeveloperLifecycleStateStore` for save, load, and delete.
- Added a focused `TrailTrainer.Developer.Persistence` infrastructure project targeting `net10.0` and depending only on Core.
- Added explicitly configured local JSON storage using `System.Text.Json` and internal DTOs.
- Preserves TaskId casing and all logical resume-context/path/timestamp values exactly.
- Creates the configured storage directory on save only when required.
- Maps the exact UTF-8 TaskId through SHA-256 to one deterministic `.lifecycle.json` file.
- Prevents TaskId path traversal, separators, invalid filename behavior, and representative sanitization collisions.
- Saves through a unique temporary file in the target directory, flushes and closes it, then atomically moves/replaces the final file.
- Does not delete an existing good target before a replacement is completely written.
- Best-effort removes the operation's temporary file on failure or cancellation.
- Returns null for missing state and makes delete idempotent.
- Rejects malformed JSON, missing fields, invalid resume context, invalid timestamps, and invalid model values clearly.
- Propagates cancellation for save/load/delete and does not translate it into normal results.
- Persists no credentials, authorization data, CI head SHA, expected merge SHA, or merge payload.
- Adds no lifecycle orchestration, Git, GitHub, HTTP, process, polling, retry, scheduling, or CLI behavior.

## Files Created

- `src/TrailTrainer.Developer.Core/DeveloperLifecyclePersistedState.cs`
- `src/TrailTrainer.Developer.Core/IDeveloperLifecycleStateStore.cs`
- `src/TrailTrainer.Developer.Persistence/TrailTrainer.Developer.Persistence.csproj`
- `src/TrailTrainer.Developer.Persistence/LocalJsonDeveloperLifecycleStateStore.cs`
- `tests/TrailTrainer.Developer.Tests/LocalJsonDeveloperLifecycleStateStoreTests.cs`
- `docs/developer-reviews/REVIEW-0019.md`

The pre-existing untracked task source `docs/developer-reviews/DEV-0019-Lifecycle-State-Persistence.md` was supplied before implementation and was neither created nor modified by this work.

## Files Modified

- `TrailTrainer.Developer.sln`
- `tests/TrailTrainer.Developer.Tests/TrailTrainer.Developer.Tests.csproj`

## Files Deleted

None.

## Architecture / Refactoring Notes

A minimal Persistence project was added because local JSON filesystem infrastructure does not belong in provider-neutral Core and is not task orchestration. The project references only Core; tests reference it directly. Same-TaskId concurrent writers are intentionally not coordinated, as distributed/cross-process locking is outside DEV-0019. Unique temporary names prevent writers from sharing a partial temporary file, and final files are never intentionally exposed partially written.

## Tests Added

- Persisted-state model value preservation and validation, including optional null task path and UTC-only timestamp.
- Store-directory configuration and creation.
- Full exact save/load round-trip for TaskId, task path, timestamp, repository directory/identity, PR number, branches, and remote.
- Same-TaskId replacement with latest-value retrieval.
- Independent distinct Task IDs and deterministic same-ID mapping across store instances.
- Traversal, slash, backslash, unusual-character, collision, and no-nested-directory path-safety scenarios.
- Missing load, existing/missing/repeated delete, and load-after-delete behavior.
- Valid final JSON and absence of successful-operation temporary files.
- Pre-cancelled replacement preserving the existing valid state.
- Malformed JSON, missing fields, invalid PR number, equal branches, non-UTC timestamp, and whitespace task path.
- Required schema values and absence of credential/head/merge fields.
- Pre-cancelled save, load, and delete propagation.
- Invalid operation TaskIds and null save state.
- The full existing regression suite remains passing.

## Verification

### dotnet build

Successful. 0 warnings, 0 errors.

### dotnet test

Successful. 364 passed, 0 failed, 0 skipped.

### git diff --check

Successful. No whitespace errors. Platform line-ending notices only.

## Deviations from DEV-0019

None.

## Open Issues / Known Limitations

None. Same-TaskId concurrent-writer serialization is intentionally outside DEV-0019; atomic final placement and unique temporary files are implemented.

## Commit and Push
No commit created.
No push performed.
