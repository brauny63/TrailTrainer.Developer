# DEV-0057 - Recover Stranded Codex State Without Lifecycle State

## Metadata
- Task ID: `DEV-0057`
- Repository: `TrailTrainer.Developer`
- Expected branch: `feature/dev-0057-stranded-codex-recovery`
- Review report: `docs/developer-reviews/REVIEW-0057.md`

## Goal
Recover a narrowly defined legacy production state left by pre-DEV-0055 execution.

A real DEV-0007 task has:
- a persisted Codex state with Phase=BranchCreated;
- the exact expected task ID, repository path and task file;
- the exact expected feature branch checked out;
- Codex-produced dirty implementation files;
- an existing REVIEW-0007.md that is parser-invalid;
- no persisted Developer lifecycle state.

AutomaticResumeCandidateSelector only discovers persisted Developer lifecycle states, so this stranded Codex state is invisible to automatic resume. Startup falls through to InitialDeveloperTaskIntake and rejects the intentionally dirty repository.

## Required Behavior
Introduce a safe recovery/adoption path for this exact stranded state.

Recovery is allowed only when ALL of the following are proven:
1. Codex execution state exists.
2. State phase is BranchCreated.
3. State task ID matches the discovered Developer Task.
4. State repository path matches the configured repository.
5. State task file matches the discovered Developer Task file.
6. Current Git branch exactly matches the task Expected branch.
7. Repository contains uncommitted changes.
8. Expected review file exists.
9. Review parsing/validation proves the review is invalid in a way eligible for review repair.
10. No conflicting persisted lifecycle state exists.

When all conditions hold:
- adopt/reconstruct the minimal persisted lifecycle state needed by the existing resume pipeline;
- transition Codex state to ReviewRepairRequired;
- preserve every implementation byte;
- resume through the existing review-only repair workflow;
- never recreate the branch;
- never rerun full implementation unnecessarily;
- never reset, clean, stash or delete files.

## Safety
If any identity, branch, repository, task-file, review-path or lifecycle condition does not match exactly, fail safely and do not adopt the state.

A dirty repository with:
- no Codex state;
- a different task;
- a different branch;
- missing review;
- conflicting lifecycle state;
- or ambiguous state

must remain blocked.

Do not weaken InitialDeveloperTaskIntake dirty-repository safety for ordinary new work.

## Ownership
Prefer a dedicated stranded-state recovery/adoption component invoked before ordinary Initial Intake.

Do not overload AutomaticResumeCandidateSelector with unrelated repository mutation logic.

After adoption, normal AutomaticResumeWorker / PersistedDeveloperLifecycle / DeveloperTaskWorkflow must own execution.

## Tests
Add regression coverage for the exact production DEV-0007 state:
- Phase=BranchCreated Codex state;
- no lifecycle state;
- expected dirty feature branch;
- implementation files present;
- invalid review with `## Architecture Notes`;
- adoption persists ReviewRepairRequired;
- lifecycle recovery becomes discoverable;
- review-only Codex instruction is used;
- implementation bytes remain unchanged;
- corrected review parses;
- workflow continues.

Also test rejection for:
- wrong branch;
- wrong repository;
- wrong task ID;
- wrong task file;
- no review;
- clean repository;
- conflicting lifecycle state;
- valid review;
- missing Codex state.

No real Codex, GitHub, network or Windows SCM effects in tests.

## Verification
Run:
- `dotnet build`
- `dotnet test`
- `git diff --check`

Require 0 errors, no new warnings, all tests passing and no whitespace errors.

## Codex Completion Protocol
Create `docs/developer-reviews/REVIEW-0057.md` using the exact DeveloperReviewParser contract.

Do not modify this Developer Task.
Do not create a commit.
Do not push.
