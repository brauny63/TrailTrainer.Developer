# DEV-0010 – Review Report Parsing and Validation

## Metadata
- Task ID: `DEV-0010`
- Repository: `TrailTrainer.Developer`
- Expected branch: `feature/dev-0010-review-report-parsing-and-validation`
- Review report: `docs/developer-reviews/REVIEW-0010.md`
- Depends on: `DEV-0006`, `DEV-0008`, `DEV-0009`

## Goal
Add support for reading and validating the structured `REVIEW-NNNN.md` reports produced by the Codex completion protocol. Parse them into strongly typed models and validate that a report belongs to a selected Developer Task and represents a reviewable completion state.

This task is read-only. It must not mutate Git state, use GitHub, execute Codex, create Pull Requests, or make merge decisions.

## Codex Execution Instructions
Work this Developer Task completely.

- Follow `docs/architecture/README.md`.
- Implement only this task's scope.
- Reuse `DeveloperTaskId` and `DeveloperTaskDocument`.
- Keep reusable contracts/models in `TrailTrainer.Developer.Core`.
- Put concrete parsing and validation in `TrailTrainer.Developer.Tasks`.
- Do not introduce Git/process execution in Tasks.
- Do not modify this task or architecture documentation.
- Do not implement anything under **Out of Scope**.
- Do not commit or push the DEV-0010 implementation.
- After verification create `docs/developer-reviews/REVIEW-0010.md`.
- If ambiguity prevents correct completion, document it and set the report to `BLOCKED`.

## Review File Convention
Review files are named `REVIEW-NNNN.md`. The parser only needs to support the stable completion-protocol format used by current Developer Tasks; do not build a general-purpose Markdown parser.

## Core Models

### DeveloperReviewStatus
Strongly typed status supporting exactly:
- `ReadyForReview` for `READY FOR REVIEW`
- `Blocked` for `BLOCKED`

Unknown textual values are rejected.

### DeveloperReviewVerification
Immutable model exposing at least:
- `BuildSuccessful`
- `BuildWarningCount`
- `BuildErrorCount`
- `TestSuccessful`
- `TestsPassed`
- `TestsFailed`
- `TestsSkipped`
- `DiffCheckSuccessful`

### DeveloperReviewDocument
Immutable model exposing at least:
- `TaskId`
- `Title`
- `FilePath`
- `Status`
- `Summary`
- `RequirementsImplemented`
- `FilesCreated`
- `FilesModified`
- `FilesDeleted`
- `ArchitectureNotes`
- `TestsAdded`
- `Verification`
- `Deviations`
- `OpenIssues`
- `CommitCreated`
- `PushPerformed`

Collections are read-only. `FilePath` is absolute and normalized. Do not expose parser internals.

### DeveloperReviewValidationResult
Immutable model exposing at least:
- `IsValid`
- `TaskId`
- `ReviewStatus`
- `Errors`
- `Warnings`

Errors/warnings are read-only collections. Normal validation failures are returned, not thrown; unparseable input may throw.

## Core Abstractions

### IDeveloperReviewParser
Asynchronous operation accepting review-file path and optional `CancellationToken`, returning `DeveloperReviewDocument`.

### IDeveloperReviewValidator
Asynchronous operation accepting `DeveloperTaskDocument`, `DeveloperReviewDocument`, optional `CancellationToken`, returning `DeveloperReviewValidationResult`.

The validator must not access filesystem or Git.

## Parsing
Implement `IDeveloperReviewParser` in `TrailTrainer.Developer.Tasks`. Read UTF-8 Markdown.

### Heading
Parse the first H1, e.g.:
`# REVIEW-0010 – Review Report Parsing and Validation`

Accept en dash (`–`) or normal hyphen (`-`) between ID and title. Convert `REVIEW-NNNN` to corresponding `DeveloperTaskId` (`DEV-NNNN`). Filename and heading IDs must match.

### Required Sections
Require these H2 sections:
- `## Status`
- `## Summary`
- `## Requirements Implemented`
- `## Files Created`
- `## Files Modified`
- `## Files Deleted`
- `## Architecture / Refactoring Notes`
- `## Tests Added`
- `## Verification`
- `## Deviations from DEV-NNNN`
- `## Open Issues / Known Limitations`
- `## Commit and Push`

The deviations heading must reference the same task ID.

### Status
The first non-empty line under Status must be exactly `READY FOR REVIEW` or `BLOCKED`, using ordinal comparison.

### Text and Lists
Preserve meaningful content of Summary, Architecture Notes, Deviations, and Open Issues, allowing only surrounding blank-line normalization.

Parse Markdown bullets from Requirements Implemented, Files Created, Files Modified, Files Deleted, and Tests Added. Inline backticks around values may be removed.

For file-list sections, a single `None` or `None.` means an empty collection.

### Verification
Parse:
- `### dotnet build` with `Successful. N warnings, N errors.` or `Failed. N warnings, N errors.`
- `### dotnet test` with `Successful. N passed, N failed, N skipped.` or `Failed. N passed, N failed, N skipped.`
- `### git diff --check` where content begins with `Successful.` or `Failed.`

Parse numbers using invariant culture. Missing/malformed verification subsections fail parsing. Never infer verification success from other sections.

### Commit and Push
Parse current protocol statements such as:
- `No commit created.`
- `No push performed.`

These map to false/false. If the report explicitly says a commit was created or a push was performed, represent true. If either state cannot be determined, fail parsing.

## Parser Rejection Rules
Reject when:
1. File does not exist.
2. Filename is not `REVIEW-NNNN.md`.
3. H1 is missing/malformed.
4. Filename/heading IDs differ.
5. Title is empty.
6. Required section is missing.
7. Deviations heading references another task.
8. Status is missing/unsupported.
9. Verification is missing/malformed.
10. Commit/push state cannot be determined.

Diagnostics must be useful.

## Review Validation
Implement `IDeveloperReviewValidator` in Tasks.

### Errors
Return invalid when:
1. Review TaskId differs from task Id.
2. Filename of task `ReviewReportPath` differs from actual review filename.
3. Status is Blocked.
4. Build was unsuccessful.
5. Build error count is non-zero.
6. Tests were unsuccessful.
7. Failed-test count is non-zero.
8. Diff check was unsuccessful.
9. `CommitCreated` is true.
10. `PushPerformed` is true.

Accumulate distinct useful errors.

### Warnings
Warn without invalidating when:
1. Build warning count > 0.
2. Skipped-test count > 0.
3. Deviations is not equivalent to `None`/`None.`.
4. Open Issues is not equivalent to `None`/`None.`.

For the None marker, ignore surrounding whitespace and an optional final period. Do not semantically judge deviations/issues.

`IsValid` is true exactly when there are no errors.

## Tests
Cover at least:

Parser:
1. Valid current-format report.
2. En-dash heading.
3. Hyphen heading.
4. Review ID converts to DeveloperTaskId.
5. Absolute normalized file path.
6. READY FOR REVIEW.
7. BLOCKED.
8. Unknown status fails.
9. Required text sections.
10. Bullet-list sections.
11. None file sections become empty.
12. Successful build counts.
13. Failed build counts.
14. Successful test counts.
15. Failed test counts.
16. Successful diff check.
17. Failed diff check.
18. No-commit/no-push maps false/false.
19. Explicit commit/push maps true.
20. Invalid filename fails.
21. Filename/heading mismatch fails.
22. Missing heading fails.
23. Empty title fails.
24. Each required section missing fails.
25. Deviations-ID mismatch fails.
26. Missing/malformed verification fails.
27. Undeterminable commit/push fails.

Validator:
28. Valid READY FOR REVIEW gives IsValid true.
29. Task/review ID mismatch error.
30. Review filename mismatch error.
31. BLOCKED error.
32. Failed build error.
33. Non-zero build errors error.
34. Failed tests error.
35. Non-zero failed tests error.
36. Failed diff-check error.
37. Commit-created error.
38. Push-performed error.
39. Build warnings are warning only.
40. Skipped tests are warning only.
41. Deviations are warning only.
42. Open issues are warning only.
43. Multiple errors accumulate.
44. Multiple warnings accumulate.
45. Warnings do not invalidate otherwise valid report.
46. Cancellation is respected/propagated where applicable.

Existing DEV-0002 through DEV-0009 tests must continue to pass. Parser tests use isolated temporary files and do not depend on repository review files. Avoid unrelated refactoring.

## Out of Scope
Do not implement Git mutations, stage/commit/push, GitHub API, PR creation/review/merge, Codex execution, report generation/editing, automatic completion/next-task selection, semantic judgment of deviations/issues, CLI review commands, or general-purpose Markdown parsing.

## Verification
Run:
- `dotnet build` — 0 errors and no new DEV-0010 warnings.
- `dotnet test` — all tests pass.
- `git diff --check` — no whitespace errors; line-ending notices alone are acceptable.

## Acceptance Criteria
DEV-0010 is complete when:
1. Strongly typed review status, verification, document, and validation-result models exist.
2. Mockable async parser/validator abstractions exist.
3. Concrete parser/validator exist in Tasks.
4. Stable current report format parses.
5. Filename, heading, deviations ID, sections, status, verification, and commit/push state are validated.
6. Verification becomes structured data.
7. All defined validation errors accumulate.
8. Defined warnings do not invalidate otherwise valid reports.
9. Validator performs no filesystem/Git access.
10. No Git process execution is introduced in Tasks.
11. Required tests exist and prior tests pass.
12. Build, tests, and diff check succeed.
13. No out-of-scope functionality is implemented.
14. `docs/developer-reviews/REVIEW-0010.md` is created.

## Codex Completion Protocol
After implementation and verification:
1. Do not commit.
2. Do not push.
3. Do not modify this Developer Task.
4. Create `docs/developer-reviews/REVIEW-0010.md`.
5. Use exactly these report sections:

# REVIEW-0010 – Review Report Parsing and Validation

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

## Deviations from DEV-0010

## Open Issues / Known Limitations

## Commit and Push
No commit created.
No push performed.

6. Use READY FOR REVIEW only when all acceptance criteria and verification succeed; otherwise BLOCKED.
7. Record build success/failure, warning/error counts, test passed/failed/skipped counts, and diff-check result.
8. List every created, modified, or deleted file.
9. Write `None` when there are no deviations or open issues.

The review report is part of DEV-0010 and must be included in the later Pull Request.
