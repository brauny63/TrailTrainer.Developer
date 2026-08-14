# DEV-0006 – Developer Task Discovery and Parsing

## Metadata

- Task ID: `DEV-0006`
- Repository: `TrailTrainer.Developer`
- Expected branch: `feature/dev-0006-task-discovery-and-parsing`
- Review report: `docs/developer-reviews/REVIEW-0006.md`
- Depends on: `DEV-0001` through `DEV-0005`

## Goal

Add the first Developer Task capability to `TrailTrainer.Developer`: discover Developer Task Markdown files and parse the task identity and metadata required by later workflow orchestration.

This package only reads and interprets task files. It must not execute tasks or perform Git workflow operations.

## Codex Execution Instructions

Work this Developer Task completely.

- Follow `docs/architecture/README.md`.
- Implement only this task's scope.
- Put concrete task functionality in `TrailTrainer.Developer.Tasks`.
- Keep reusable contracts/models in `TrailTrainer.Developer.Core`.
- Do not modify this Developer Task or architecture documentation.
- Do not implement anything under **Out of Scope**.
- Do not create a commit or push the DEV-0006 implementation.
- After verification create `docs/developer-reviews/REVIEW-0006.md`.
- If an ambiguity prevents correct completion, do not invent behavior; document it and set the review status to `BLOCKED`.

## Scope

Implement functionality that can:

1. Discover Developer Task Markdown files directly below `docs/developer-tasks`.
2. Parse a selected Developer Task file.
3. Return strongly typed identity and metadata.
4. Validate that filename, heading and metadata identify the same task.

The parser only needs to understand the stable Developer Task format used by DEV-0004 and later.

## Task File Convention

Task files follow:

`DEV-NNNN-<descriptive-name>.md`

Only matching files are discovered. Results are ordered by numeric task number ascending.

## Core Models

### DeveloperTaskId

Add an immutable strongly typed value that:

- represents the numeric task number,
- formats canonically as `DEV-NNNN`,
- supports value equality,
- accepts numbers 1 through 9999,
- rejects values outside that range.

### DeveloperTaskDescriptor

Must expose at least:

- `Id`
- `FilePath`
- `FileName`

`FilePath` must be an absolute normalized path.

### DeveloperTaskDocument

Must expose at least:

- `Id`
- `Title`
- `FilePath`
- `Repository`
- `ExpectedBranch`
- `ReviewReportPath`

Do not expose Markdown parser internals through Core.

## Abstractions

Add mockable asynchronous Core abstractions:

### IDeveloperTaskDiscovery

Accepts repository root path and optional `CancellationToken`; returns discovered task descriptors.

### IDeveloperTaskParser

Accepts Developer Task file path and optional `CancellationToken`; returns `DeveloperTaskDocument`.

## Discovery Implementation

Implement in `TrailTrainer.Developer.Tasks`.

Requirements:

1. Validate repository root.
2. Search only directly within `docs/developer-tasks`; no recursion.
3. Ignore non-Markdown files.
4. Ignore Markdown files not matching `DEV-NNNN-*.md`.
5. Parse task numbers from filenames.
6. Order results numerically.
7. Return an empty collection when the task directory exists but has no matches.
8. Fail clearly if repository root does not exist.
9. Fail clearly if `docs/developer-tasks` does not exist.
10. Respect cancellation.
11. Do not use Git for discovery.

## Parsing Implementation

Read UTF-8 Markdown.

From the first level-1 heading, for example:

`# DEV-0006 – Developer Task Discovery and Parsing`

extract the task ID and title. Accept either en dash (`–`) or normal hyphen (`-`) between ID and title.

From `## Metadata`, extract:

- `Task ID`
- `Repository`
- `Expected branch`
- `Review report`

Values may be wrapped in Markdown backticks; backticks must not be returned. Additional metadata entries must be tolerated.

## Validation

Reject documents when:

1. Filename does not match `DEV-NNNN-*.md`.
2. Level-1 heading is missing or has no valid task ID/title.
3. `## Metadata` is missing.
4. Any required metadata value is missing.
5. Filename task ID differs from heading task ID.
6. Metadata `Task ID` differs from filename/heading task ID.
7. `Repository`, `Expected branch`, or `Review report` is empty/whitespace.

Failures must contain useful diagnostics. Do not base application logic on localized OS error text.

## Path Behavior

Descriptor and document `FilePath` values must be absolute normalized paths.

`ReviewReportPath` remains exactly the repository-relative path represented by the metadata value; do not convert it to an absolute path and do not require the review file to exist.

## Tests

Cover at least:

### DeveloperTaskId
1. Valid formatting.
2. Value equality.
3. Zero rejected.
4. Values above 9999 rejected.

### Discovery
5. Matching tasks discovered.
6. Numeric ordering.
7. Nonmatching Markdown ignored.
8. Non-Markdown ignored.
9. Empty matching directory returns empty collection.
10. Missing repository root fails.
11. Missing task directory fails.
12. Returned paths are absolute.

### Parsing
13. Valid current-format task parses.
14. Heading ID/title parse.
15. Required metadata parses without backticks.
16. En-dash separator accepted.
17. Hyphen separator accepted.
18. Filename/heading mismatch fails.
19. Metadata/heading mismatch fails.
20. Missing heading fails.
21. Missing Metadata section fails.
22. Each missing required metadata field is rejected.
23. Invalid filename convention fails.
24. Parsed file path is absolute.
25. Review report path remains repository-relative.
26. Additional metadata is tolerated.

Existing DEV-0002 through DEV-0005 tests must continue to pass.

Tests use isolated temporary directories, create their own task files, clean up resources, and must not depend on this repository's actual task files or global machine configuration.

Small directly useful test helpers are allowed. Avoid unrelated refactoring.

## Out of Scope

Do not implement task execution, workflow orchestration, automatic Git operations, GitHub/PR integration, review-report parsing, task editing, Markdown rewriting, CLI commands, dependency resolution, task status persistence, automatic next-task selection, or a general-purpose Markdown parser.

## Verification

Run for the complete solution:

`dotnet build`

Required: 0 errors and no new warnings caused by DEV-0006.

Then:

`dotnet test`

All tests must pass.

Also run:

`git diff --check`

There must be no whitespace errors. Platform line-ending notices alone are acceptable.

## Acceptance Criteria

DEV-0006 is complete when:

1. `DeveloperTaskId`, `DeveloperTaskDescriptor`, and `DeveloperTaskDocument` exist as Core models.
2. Mockable asynchronous discovery/parser abstractions exist.
3. Concrete implementations exist in `TrailTrainer.Developer.Tasks`.
4. Discovery is limited to direct children of `docs/developer-tasks`.
5. Only `DEV-NNNN-*.md` files are returned, numerically ordered.
6. Task ID, title and required metadata parse correctly.
7. Filename, heading and metadata IDs are cross-validated.
8. Required malformed/missing content is rejected.
9. Absolute file-path behavior is implemented.
10. Review report path remains repository-relative.
11. No Git/GitHub operation is performed by discovery/parsing.
12. Required tests exist and previous tests continue to pass.
13. `dotnet build`, `dotnet test`, and `git diff --check` succeed.
14. No out-of-scope functionality is implemented.
15. `docs/developer-reviews/REVIEW-0006.md` is created.

## Codex Completion Protocol

After implementation and verification:

1. Do **not** create a Git commit.
2. Do **not** push changes.
3. Do **not** modify this Developer Task.
4. Create `docs/developer-reviews/REVIEW-0006.md`.
5. Use these sections:

# REVIEW-0006 – Developer Task Discovery and Parsing

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

## Deviations from DEV-0006

## Open Issues / Known Limitations

## Commit and Push
No commit created.
No push performed.

6. Use `READY FOR REVIEW` only when all acceptance criteria and verification succeed; otherwise use `BLOCKED`.
7. Record build success/failure, warning/error counts, test passed/failed/skipped counts, and `git diff --check`.
8. List every created, modified, or deleted file.
9. Write `None` for no deviations and no known issues.

The review report is part of DEV-0006 and must be included in the later Pull Request.
