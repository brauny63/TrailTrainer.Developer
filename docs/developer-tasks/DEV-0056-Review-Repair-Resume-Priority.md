# DEV-0056 - Review Repair Resume Priority Before Initial Intake

## Metadata
- Task ID: `DEV-0056`
- Repository: `TrailTrainer.Developer`
- Expected branch: `feature/dev-0056-review-repair-resume-priority`
- Review report: `docs/developer-reviews/REVIEW-0056.md`

## Goal
Fix the production recovery gap discovered after DEV-0055.

A real DEV-0007 run successfully produced implementation changes and an invalid review. After deploying DEV-0055, restarting the service still fails before review repair because InitialDeveloperTaskIntake rejects the dirty repository before the existing Codex recovery state is resumed.

Existing recoverable work must always have priority over initial intake safety checks.

## Production Evidence
Current TerrainEngine state:
- branch `feature/dev-0007-implement-valueobject`
- implementation changes present
- invalid `REVIEW-0007.md` present
- Codex lifecycle state exists
- repository is intentionally dirty because Codex produced the implementation
- service restart fails in `InitialDeveloperTaskIntake` with `has uncommitted changes`

## Requirements
- Existing resumable/recoverable work must be detected before any new-task intake dirty-repository rejection.
- ReviewRepairRequired must participate in the existing automatic-resume candidate semantics.
- A dirty repository is allowed only when it belongs to the matching persisted/recoverable task and the recovery phase explicitly permits it.
- New intake on an unrelated dirty repository must remain blocked.
- Do not weaken repository safety globally.
- Do not reset, clean, stash, delete or overwrite existing implementation work.
- Do not create a second task.
- Resume must use the existing lifecycle/workflow boundaries.
- Hosted service must not terminate merely because recoverable task work is dirty.
- Controlled recovery failures must keep the service healthy where existing conventions require that behavior.

## Hosted Service Ordering
The intended startup order is:
1. detect/resume persisted recoverable work;
2. if recoverable work exists, do not run initial intake;
3. only if no recoverable work exists, evaluate initial intake;
4. dirty-repository safety remains strict for new intake.

Do not duplicate resume logic inside InitialDeveloperTaskIntake if the existing AutomaticResumeWorker/candidate pipeline can own this ordering.

## DEV-0007 Regression
Add an executable regression scenario representing the real state:
- expected feature branch exists;
- implementation files are dirty/untracked;
- invalid review exists with `## Architecture Notes`;
- recovery phase indicates review repair;
- service startup selects recovery, not new intake;
- Codex instruction is review-only;
- implementation files remain unchanged;
- repaired review becomes parser-valid;
- workflow can continue beyond review validation.

## Tests
Cover:
- resumable review-repair work takes priority over initial intake;
- dirty matching task branch is allowed only for review repair;
- dirty unrelated repository still blocks new intake;
- no duplicate task start;
- no branch recreation;
- no reset/clean/stash;
- existing implementation bytes remain unchanged;
- controlled recovery failure does not crash the hosted service;
- DEV-0047 initial intake semantics remain intact;
- DEV-0049 recovery remains intact;
- DEV-0050 validation remains intact;
- DEV-0055 exact review contract remains intact.

No real Codex, GitHub, network or Windows SCM effects in tests.

## Verification
Run:
- `dotnet build`
- `dotnet test`
- `git diff --check`

Require 0 errors, no new warnings, all tests passing and no whitespace errors.

## Codex Completion Protocol
Create `docs/developer-reviews/REVIEW-0056.md` using the exact DeveloperReviewParser contract.

Do not modify this Developer Task.
Do not create a commit.
Do not push.
