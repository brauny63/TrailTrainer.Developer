# DEV-0025 – Automatic Persisted Lifecycle Resume

## Metadata
- Task ID: `DEV-0025`
- Repository: `TrailTrainer.Developer`
- Expected branch: `feature/dev-0025-automatic-persisted-lifecycle-resume`
- Review report: `docs/developer-reviews/REVIEW-0025.md`
- Depends on: `DEV-0020`, `DEV-0024`

## Goal
Connect DEV-0024 automatic resume-candidate selection with DEV-0020 persisted lifecycle resume execution.

One invocation selects the automatic candidate exactly once and, when found, resumes that selected TaskId exactly once. If no candidate exists, return NotFound without resume.

This task is orchestration only. It must not poll, wait, retry, schedule, loop, process multiple states, mutate persistence directly, or add Git/GitHub/filesystem/JSON/process behavior.

## Codex Execution Instructions
Work this Developer Task completely.

- Follow `docs/architecture/README.md`.
- Implement only the scope defined here.
- Reuse `IAutomaticResumeCandidateSelector` from DEV-0024.
- Reuse `IPersistedDeveloperLifecycle` from DEV-0020.
- Reuse existing persisted-lifecycle and merge-method models.
- Keep provider-neutral contracts/models in `TrailTrainer.Developer.Core`.
- Put orchestration in `TrailTrainer.Developer.Tasks`.
- Do not instantiate concrete persistence, discovery, Git, GitHub, HTTP, filesystem, JSON, process, or shell implementations.
- Do not add polling, delays, retries, timers, scheduling, or background workers.
- Do not modify this Developer Task or architecture documentation.
- Do not implement anything under **Out of Scope**.
- Do not create a Git commit for DEV-0025.
- Do not push the DEV-0025 implementation branch.
- After implementation and verification create `docs/developer-reviews/REVIEW-0025.md`.

If ambiguity prevents correct completion, document it and set the review status to `BLOCKED`.

## Request

### AutomaticPersistedLifecycleResumeRequest
Add an immutable provider-neutral request exposing:
- `PullRequestMergeMethod MergeMethod`
- optional `MergeCommitTitle`
- optional `MergeCommitMessage`
- `DeleteRemoteBranch`

Preserve values exactly. Do not accept TaskId, PR number, branch, repository identity, or resume context. TaskId must come only from DEV-0024.

Follow DEV-0020 merge-method validation behavior; do not invent stricter validation here.

## Result State

### AutomaticPersistedLifecycleResumeState
Exactly:
- `NotFound`
- `Pending`
- `Failed`
- `Completed`

## Result

### AutomaticPersistedLifecycleResumeResult
Expose:
- `State`
- exact `AutomaticResumeCandidateResult Candidate`
- optional exact `PersistedDeveloperLifecycleResumeResult Resume`

Invariants:
- NotFound: Candidate.NotFound and Resume null.
- Pending: Candidate.Found and Resume.Pending.
- Failed: Candidate.Found and Resume.Failed.
- Completed: Candidate.Found and Resume.Completed.
- Reject unsupported enum values.
- Preserve exact Candidate and Resume object identities.

## Core Abstraction

### IAutomaticPersistedLifecycleResumer
Add a mockable asynchronous abstraction equivalent to:

`ResumeAsync(AutomaticPersistedLifecycleResumeRequest request, CancellationToken cancellationToken = default)`

Return `AutomaticPersistedLifecycleResumeResult`.

## Concrete Orchestration

### AutomaticPersistedLifecycleResumer
Implement in `TrailTrainer.Developer.Tasks`.

Inject exactly:
- `IAutomaticResumeCandidateSelector`
- `IPersistedDeveloperLifecycle`

Do not inject discovery, state store, DEV-0022 selector, Git/GitHub services, or persistence implementations.

## Required Flow

```text
Validate request
      |
      v
IAutomaticResumeCandidateSelector.SelectAsync
      |
      +-- NotFound --> return NotFound
      |
      +-- Found
            |
            v
Candidate.ResumeTarget.TaskId
            |
            v
PersistedDeveloperLifecycleResumeRequest
            |
            v
IPersistedDeveloperLifecycle.ResumeAsync
            |
            +-- NotFound -> inconsistency failure
            +-- Pending  -> Pending
            +-- Failed   -> Failed
            +-- Completed-> Completed
```

## Candidate Selection

Call the automatic candidate selector exactly once with the exact caller cancellation token.

### NotFound
- Do not invoke DEV-0020.
- Return NotFound.
- Preserve exact candidate result.

### Found
- Require valid `ResumeTarget`.
- Use exactly `Candidate.ResumeTarget.TaskId`.
- Do not independently reconstruct, rediscover, or derive TaskId from naming conventions.
- Construct DEV-0020 request using selected TaskId plus exact caller merge/delete options.
- Invoke DEV-0020 Resume exactly once.

## Race Handling

If DEV-0024 returns Found but DEV-0020 returns NotFound, treat it as a race/inconsistency because the selected persisted state disappeared between selection and resume.

- Fail clearly.
- Do not convert to normal NotFound.
- Do not reselect.
- Do not re-resume.
- Do not retry.

## Resume Outcomes

### Pending
Return Pending with exact Candidate and exact DEV-0020 Resume result. DEV-0020 owns state retention.

### Failed
Return Failed with exact Candidate and exact DEV-0020 Resume result. DEV-0020 owns state retention.

### Completed
Return Completed with exact Candidate and exact DEV-0020 Resume result. DEV-0020 owns state deletion.

DEV-0025 must not save/delete state itself.

## Failure / Cancellation

- Null request -> candidate selector not called.
- Candidate-selector exception -> DEV-0020 not called.
- Candidate NotFound -> DEV-0020 not called.
- Invalid Found candidate -> fail clearly.
- DEV-0020 exception -> propagate.
- DEV-0020 NotFound after Found -> inconsistency failure.
- No retry, rollback, second selection, or second resume.
- Propagate exact cancellation token to candidate selector and DEV-0020.
- Cancellation must not become a normal lifecycle state.

## Tests

Use injected fakes/stubs only. No filesystem, JSON, Git, GitHub, network, or child process dependency.

Cover at least:
1. Request preserves MergeMethod.
2. Request preserves null optional title/message.
3. Request preserves exact non-null title/message.
4. Request preserves DeleteRemoteBranch.
5. Merge enum behavior matches DEV-0020.
6. Unsupported result state rejected.
7. NotFound requires Candidate.NotFound.
8. NotFound rejects Resume.
9. Pending requires Candidate.Found and Resume.Pending.
10. Failed requires Candidate.Found and Resume.Failed.
11. Completed requires Candidate.Found and Resume.Completed.
12. Valid results preserve exact Candidate identity.
13. Non-NotFound results preserve exact Resume identity.
14. Candidate selector called exactly once.
15. Exact cancellation token delegated to candidate selector.
16. Candidate exception propagates and prevents Resume.
17. Candidate NotFound maps to NotFound and does not Resume.
18. Exact Candidate.ResumeTarget.TaskId is used.
19. Caller cannot supply/override TaskId.
20. MergeMethod delegated exactly.
21. MergeCommitTitle delegated exactly.
22. MergeCommitMessage delegated exactly.
23. DeleteRemoteBranch delegated exactly.
24. Exact cancellation token delegated to DEV-0020.
25. DEV-0020 Resume called exactly once and after candidate selection.
26. Pending maps correctly.
27. Failed maps correctly.
28. Completed maps correctly.
29. DEV-0020 NotFound after Found fails clearly.
30. Race failure does not reselect or re-resume.
31. DEV-0020 exception propagates without retry.
32. Pre-cancelled candidate selection prevents Resume.
33. DEV-0020 cancellation propagates.
34. No retry exists.
35. Service depends only on automatic candidate selector and persisted lifecycle.
36. No direct discovery/state-store/DEV-0022 dependency.
37. No filesystem/JSON/Git/GitHub/process work.
38. Existing DEV-0002 through DEV-0024 tests continue to pass.

## Out of Scope
Do not implement polling, wait loops, repeated Resume, repeated candidate selection, retries, timers, scheduling, background workers, batch processing, resume-all, state enumeration, direct state-store access, persistence changes, filesystem/JSON changes, Git operations, GitHub REST, CI lookup, PR creation, new merge/cleanup logic, CLI/UI, automatic next Developer Task selection, or Codex execution.

## Verification
Run:
```text
dotnet build
dotnet test
git diff --check
```

Required:
- build: 0 errors and no new DEV-0025 warnings,
- all tests pass,
- no whitespace errors.

## Acceptance Criteria
DEV-0025 is complete when:
1. `AutomaticPersistedLifecycleResumeRequest` exists.
2. `AutomaticPersistedLifecycleResumeState` has exactly NotFound/Pending/Failed/Completed.
3. `AutomaticPersistedLifecycleResumeResult` enforces invariants.
4. `IAutomaticPersistedLifecycleResumer` exists in Core.
5. `AutomaticPersistedLifecycleResumer` exists in Tasks.
6. It depends only on DEV-0024 selector and DEV-0020 persisted lifecycle.
7. Candidate selection occurs exactly once.
8. Candidate NotFound returns without Resume.
9. Found uses exact `ResumeTarget.TaskId`.
10. Caller cannot override TaskId.
11. DEV-0020 Resume occurs exactly once after Found.
12. Resume options are delegated exactly.
13. Pending/Failed/Completed map correctly.
14. DEV-0020 NotFound after Found fails as inconsistency.
15. Exact nested result identities are preserved.
16. Cancellation is propagated.
17. No retries/polling/scheduling/direct persistence/discovery/Git/GitHub/filesystem/JSON/process logic is introduced.
18. Tests use fakes only.
19. Existing tests pass.
20. Build succeeds.
21. Tests succeed.
22. `git diff --check` succeeds.
23. No out-of-scope functionality is implemented.
24. `docs/developer-reviews/REVIEW-0025.md` is created.

## Codex Completion Protocol

After implementation and verification:
1. Do **not** create a Git commit.
2. Do **not** push changes.
3. Do **not** modify this Developer Task.
4. Create `docs/developer-reviews/REVIEW-0025.md`.
5. Use:

```text
# REVIEW-0025 – Automatic Persisted Lifecycle Resume

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

## Deviations from DEV-0025

## Open Issues / Known Limitations

## Commit and Push
No commit created.
No push performed.
```

6. Use READY FOR REVIEW only if all acceptance criteria and verification succeed; otherwise BLOCKED.
7. Record build warning/error counts, test passed/failed/skipped counts, and diff-check result.
8. List every created/modified/deleted file.
9. Write `None` for no deviations/open issues.

The review report is part of DEV-0025 and must be included in the later Pull Request.
