# DEV-0047 – Initial Developer Task Intake

## Goal
Close the concrete v1.1 pilot gap: allow the production TrailTrainer.Developer Windows Service to discover and start one brand-new Developer Task from a configured local target repository.

Reuse the existing discovery, starter, lifecycle, persistence, and automatic-resume components. This is an intake boundary, not a new workflow engine.

## Scope
Add production configuration and orchestration so the hosted service can:
1. know one local target repository path;
2. discover tasks using the existing DeveloperTaskDiscovery;
3. deterministically select at most one eligible new task;
4. start it through existing workflow/lifecycle boundaries;
5. persist lifecycle state through the existing persistence implementation;
6. leave continuation/recovery to the existing automatic-resume pipeline.

## Configuration
Add a small section such as:

InitialTaskIntake:
- Enabled (default false)
- RepositoryPath
- RepositoryName
- GitHubOwner
- BaseBranch (default main)
- RemoteName (default origin)

Reuse existing defaults/configuration for merge behavior where possible. Do not store credentials or GitHub tokens here.

## Selection and Safety
- Disabled intake does nothing.
- RepositoryPath must exist and identify a Git repository.
- Use existing task discovery and ordering/ID semantics.
- Never start more than one new task per intake attempt.
- Existing resumable/persisted work has priority; do not start a second task.
- Do not overwrite persisted lifecycle state.
- Invalid tasks fail visibly.
- Rely on existing repository-status safety checks.
- Never clean, reset, stash, delete, or overwrite local/untracked work to make a repository startable.
- A dirty/unsafe repository must block intake visibly.

## Hosted-Service Behavior
1. Check existing resumable work first.
2. If resumable work exists, use the existing automatic-resume behavior only.
3. If no resumable work exists, intake may select one new task.
4. Once started/persisted, existing lifecycle/resume machinery owns continuation.
5. Preserve existing bounded execution limits.
6. Do not add a tight polling loop, filesystem watcher, queue, database, or new scheduler.

## Requirements
- Reuse DeveloperTaskDiscovery.
- Reuse DeveloperTaskStarter and/or the existing lifecycle orchestration boundary.
- Reuse IPersistedDeveloperLifecycle and the existing lifecycle state store.
- Reuse existing automatic-resume discovery/candidate semantics.
- Preserve Git/GitHub/merge/cleanup behavior.
- Preserve DEV-0045 health and all Windows Service management commands.
- No real GitHub, Codex, network, or Windows SCM effects in tests.
- Production DI must work with intake disabled and with valid enabled configuration.
- Log/report enough context using existing logging conventions to diagnose why intake did or did not start a task.

## Tests
Cover at least:
1. disabled -> no discovery/start;
2. missing repository -> deterministic failure/no start;
3. non-Git repository -> deterministic failure/no start;
4. clean repo + one eligible task -> selected and started;
5. deterministic selection with multiple eligible tasks;
6. at most one task per attempt;
7. existing resumable lifecycle -> no new task;
8. resumable work has priority;
9. persisted state is not overwritten;
10. dirty/unsafe repo blocks intake;
11. malformed task is surfaced;
12. existing branch/starter behavior is used;
13. initial lifecycle state uses existing persistence boundary;
14. existing automatic-resume path can continue afterward;
15. automatic-resume limits remain effective;
16. no real GitHub call;
17. no real Codex call;
18. no SCM mutation;
19. health unchanged;
20. management commands unchanged;
21. production DI resolves with valid enabled intake;
22. production DI resolves with default disabled intake;
23. all existing tests pass.

## Architecture
Keep the concept small. Suggested names:
- InitialTaskIntakeOptions
- IInitialDeveloperTaskIntake
- InitialDeveloperTaskIntake

Names may differ if existing architecture suggests a better fit. Do not duplicate Git, GitHub, parsing, lifecycle, or persistence logic.

## Out of Scope
Repository cloning or auto-pull; multiple repositories; concurrent execution; queues; filesystem watchers; web/API intake; UI; notifications; new GitHub/Codex authentication; automatic stash/reset/cleanup of dirty repositories; installer changes; Linux service support; Developer Task format redesign.

## Verification
Run:
- dotnet build
- dotnet test
- git diff --check

Required: 0 errors, no new warnings, all tests pass, no whitespace errors, and no real external side effects.

## Acceptance Criteria
The installed production host can be configured with one local target repository and, when no resumable lifecycle exists, deterministically discover and start one new Developer Task using existing workflow/lifecycle/persistence boundaries.

Dirty repositories and existing resumable work prevent unsafe/parallel intake. After initial persistence, the existing automatic-resume pipeline can continue without a manual initial-start command.

## Codex Completion Protocol
Create `docs/developer-reviews/REVIEW-0047.md` containing:

# REVIEW-0047 – Initial Developer Task Intake
## Status
READY FOR REVIEW | BLOCKED
## Summary
## Intake Semantics Implemented
## Configuration Added
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
## Deviations from DEV-0047
## Open Issues / Known Limitations
## Pilot Readiness Assessment
READY | NOT READY
## Commit and Push
No commit created.
No push performed.

Do not modify this task. Do not commit or push.
