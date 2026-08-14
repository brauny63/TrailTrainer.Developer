# REVIEW-0006 – Developer Task Discovery and Parsing

## Status

READY FOR REVIEW

## Summary

Implemented strongly typed Developer Task identity, discovery, and parsing for the stable task format used by DEV-0004 and later.

## Requirements Implemented

- Added validated, canonically formatted Developer Task IDs with value equality.
- Added immutable task descriptor and parsed-document models.
- Added mockable asynchronous discovery and parser abstractions.
- Discovers only direct `docs/developer-tasks` children matching `DEV-NNNN-*.md`.
- Orders discovered tasks by numeric task number and returns normalized absolute paths.
- Parses the first level-1 heading and required Metadata entries from UTF-8 Markdown.
- Supports en-dash and hyphen heading separators and removes optional metadata backticks.
- Cross-validates filename, heading, and metadata task IDs.
- Produces useful validation failures for malformed or incomplete documents.

## Files Created

- `src/TrailTrainer.Developer.Core/DeveloperTaskId.cs`
- `src/TrailTrainer.Developer.Core/DeveloperTaskDescriptor.cs`
- `src/TrailTrainer.Developer.Core/DeveloperTaskDocument.cs`
- `src/TrailTrainer.Developer.Core/IDeveloperTaskDiscovery.cs`
- `src/TrailTrainer.Developer.Core/IDeveloperTaskParser.cs`
- `src/TrailTrainer.Developer.Tasks/DeveloperTaskFileConvention.cs`
- `src/TrailTrainer.Developer.Tasks/DeveloperTaskDiscovery.cs`
- `src/TrailTrainer.Developer.Tasks/DeveloperTaskParser.cs`
- `tests/TrailTrainer.Developer.Tests/DeveloperTaskIdTests.cs`
- `tests/TrailTrainer.Developer.Tests/DeveloperTaskDiscoveryTests.cs`
- `tests/TrailTrainer.Developer.Tests/DeveloperTaskParserTests.cs`
- `docs/developer-reviews/REVIEW-0006.md`

## Files Modified

None.

## Files Deleted

None.

## Architecture / Refactoring Notes

Reusable contracts and immutable models are contained in Core. Concrete filesystem discovery and the deliberately narrow Markdown-format parser are contained in Tasks. Discovery and parsing do not depend on Git or any external Markdown package.

## Tests Added

- Canonical ID formatting, value equality, and lower/upper range rejection.
- Matching-file discovery, numeric ordering, direct-child restriction, filtering, empty results, missing directories, and absolute paths.
- Valid parsing with en-dash and hyphen separators.
- Heading title and ID extraction.
- Required metadata extraction with backtick removal.
- Repository-relative review-report path preservation.
- Additional metadata tolerance.
- Filename/heading and metadata/heading mismatch rejection.
- Missing heading and Metadata-section rejection.
- Individual rejection of every missing required metadata value.
- Invalid filename rejection.

## Verification

### dotnet build

Successful. 0 warnings, 0 errors.

### dotnet test

Successful. 48 passed, 0 failed, 0 skipped.

### git diff --check

Successful. No whitespace errors.

## Deviations from DEV-0006

None.

## Open Issues / Known Limitations

None.

## Commit and Push

No commit created.
No push performed.
