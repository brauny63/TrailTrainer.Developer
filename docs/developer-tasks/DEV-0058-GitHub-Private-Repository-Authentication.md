# DEV-0058 - GitHub Private Repository Authentication and PR Recovery

## Metadata
- Task ID: `DEV-0058`
- Repository: `TrailTrainer.Developer`
- Expected branch: `feature/dev-0058-github-private-repo-auth`
- Review report: `docs/developer-reviews/REVIEW-0058.md`

## Goal
Fix the remaining production blocker in the real TerrainEngine DEV-0007 workflow.

DEV-0007 has already:
- been implemented;
- passed build and 13/13 tests;
- repaired its Developer Review;
- been committed as `dae4ea9`;
- been pushed to `origin/feature/dev-0007-implement-valueobject`.

The workflow now fails only when GitHubPullRequestService tries to look up open pull requests for private repository `brauny63/TrailTrainer.TerrainEngine`, returning HTTP 404.

Manual `gh` access under the same Windows user succeeds:
- `gh auth status` is authenticated as `brauny63`;
- token includes `repo`;
- `gh repo view brauny63/TrailTrainer.TerrainEngine` succeeds;
- `gh pr list --repo brauny63/TrailTrainer.TerrainEngine` succeeds.

Therefore diagnose and fix the GitHub API credential/source used by TrailTrainer.Developer.

## Requirements
- Determine exactly how GitHubPullRequestService obtains authentication.
- Ensure production access to private repositories uses an explicit supported credential source.
- Do not assume unauthenticated GitHub API access.
- Do not log token values.
- Do not copy secrets into source control.
- Preserve existing Git remote authentication behavior.
- Preserve GitHubRepositoryIdentity validation.
- Distinguish:
  - repository not found;
  - authentication missing;
  - authentication rejected;
  - insufficient private-repository access;
  - rate limiting;
  - other GitHub HTTP failures.
- A private-repository 404 caused by unavailable credentials must produce actionable diagnostics rather than a generic Not Found message.
- GitHub failures must be controlled workflow failures and must not terminate the Windows Service process.
- Existing persisted lifecycle state must remain retryable after PR lookup/create failure.
- Do not recreate commits or push duplicate commits on retry.
- PR creation must remain idempotent through EnsureOpenAsync.

## Credential Strategy
Prefer one explicit production credential strategy.

If the existing implementation supports configuration such as a GitHub token:
- validate configuration at startup;
- allow environment / secret-store injection;
- never persist the secret in lifecycle files;
- never log it.

If reusing GitHub CLI authentication is considered:
- investigate whether `gh auth token` or another supported mechanism is appropriate;
- do not shell-concatenate secrets;
- do not expose token output in logs;
- keep the credential acquisition component testable and isolated.

Do not invent unsupported authentication behavior.

## Private Repository Probe
Add an explicit diagnostic command suitable for production that:
- uses the same GitHub client/authentication path as GitHubPullRequestService;
- checks repository metadata for configured owner/repository;
- optionally checks open-PR listing permission;
- performs no mutation;
- prints only success/failure diagnostics;
- never prints credentials.

Normal `health` must not unexpectedly make network calls unless explicitly designed/configured to do so.

## Recovery
The exact existing DEV-0007 state must be retryable:
- clean feature branch;
- commit `dae4ea9`;
- branch already pushed;
- Codex state already succeeded;
- lifecycle pending PR creation.

After authentication is corrected, resume must:
1. not rerun Codex;
2. not create another implementation commit;
3. not force-push;
4. detect/open the missing PR;
5. continue through existing gates and lifecycle.

## Tests
Cover:
- authenticated private repository success;
- missing credential;
- invalid credential;
- private repository 404 diagnostic;
- public/unrelated 404 behavior;
- 401;
- 403;
- rate limit;
- token never appears in logs/exceptions;
- PR lookup remains idempotent;
- retry after PR failure does not re-commit or re-push;
- hosted service catches GitHub workflow failures as controlled failures;
- existing DEV-0007 persisted state resumes at PR stage.

Tests must not call real GitHub or Windows SCM.

## Verification
Run:
- `dotnet build`
- `dotnet test`
- `git diff --check`

Require 0 errors, no new warnings, all tests passing and no whitespace errors.

## Codex Completion Protocol
Create `docs/developer-reviews/REVIEW-0058.md` using the exact DeveloperReviewParser contract.

Do not modify this Developer Task.
Do not create a commit.
Do not push.
