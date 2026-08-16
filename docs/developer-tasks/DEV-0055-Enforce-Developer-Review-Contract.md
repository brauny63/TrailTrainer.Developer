# DEV-0055 - Enforce Developer Review Contract in Codex Instructions

## Metadata
- Task ID: `DEV-0055`
- Repository: `TrailTrainer.Developer`
- Expected branch: `feature/dev-0055-enforce-review-contract`
- Review report: `docs/developer-reviews/REVIEW-0055.md`

## Goal
Prevent successful Codex implementations from failing only because the generated Developer Review does not exactly match the contract enforced by DeveloperReviewParser.

The real TerrainEngine DEV-0007 pilot successfully produced code, 13 passing tests, a clean build, git diff validation, and REVIEW-0007.md, but completion was blocked because Codex wrote `## Architecture Notes` instead of the required exact heading `## Architecture / Refactoring Notes`.

## Requirements
- Treat DeveloperReviewParser as the authoritative review contract.
- Do not duplicate its required section list manually in unrelated workflow code.
- Codex task instructions must explicitly provide the exact review contract expected by the parser.
- Required level-2 headings must be emitted exactly:
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
- Verification must explicitly instruct Codex to emit:
  - `### dotnet build`
  - first non-empty line exactly in parser-supported form: `Successful. N warnings, N errors.` or `Failed. N warnings, N errors.`
  - `### dotnet test`
  - first non-empty line exactly: `Successful. N passed, N failed, N skipped.` or `Failed. N passed, N failed, N skipped.`
  - `### git diff --check`
  - first non-empty line beginning exactly `Successful.` or `Failed.`
- Status must be exactly `READY FOR REVIEW` or `BLOCKED`.
- Commit/push state must use parser-supported wording such as `No commit created.` and `No push performed.`
- Files sections must use bullet lists or `None`.
- The review filename and heading ID must match the task ID.
- Codex must not invent alternative section names or synonyms.

## Contract Ownership
Prefer exposing the parser contract through one reusable contract/template/provider rather than maintaining two independent lists that can drift.

If appropriate, extract the required review headings and formatting guidance from DeveloperReviewParser into a small reusable contract component consumed by both parser validation and Codex instruction generation.

Do not weaken parser strictness merely to accept arbitrary headings.

## Retry / Recovery
When Codex has produced implementation changes and a review exists but review parsing fails:
- preserve the implementation;
- do not reset, clean, stash, or overwrite repository work;
- keep the task retryable only through existing safe lifecycle semantics;
- a subsequent Codex invocation should be instructed to inspect and repair only the invalid review when implementation output already exists and remains safe;
- do not duplicate implementation unnecessarily.

## Tests
Add regression coverage for the real DEV-0007 case:
- valid implementation output exists;
- review contains `## Architecture Notes`;
- validation rejects it;
- generated retry/instruction contains the exact required `## Architecture / Refactoring Notes`;
- corrected review passes parsing;
- existing implementation remains untouched.
Also cover every required heading, deviations heading, exact verification formats, status values, file-list conventions, and commit/push wording.

No real Codex, GitHub, network, or SCM effects in tests.

## Safety
- Never weaken repository safety.
- Never infer success from exit code alone.
- Never auto-reset or clean user work.
- Never commit/push/merge after invalid review.
- Never silently rewrite implementation files merely to repair review formatting.
- Preserve DEV-0049 through DEV-0052 behavior.

## Verification
Run `dotnet build`, `dotnet test`, and `git diff --check`. Require 0 errors, no new warnings, all tests passing and no whitespace errors.

## Codex Completion Protocol
Create `docs/developer-reviews/REVIEW-0055.md` using the exact DeveloperReviewParser contract, including `## Architecture / Refactoring Notes` and the exact parser-supported verification sentence formats.

Do not modify this Developer Task.
Do not create a commit.
Do not push.
