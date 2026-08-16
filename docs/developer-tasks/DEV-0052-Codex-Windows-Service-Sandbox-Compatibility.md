# DEV-0052 - Codex Windows Service Sandbox Compatibility

## Metadata
- Task ID: `DEV-0052`
- Repository: `TrailTrainer.Developer`
- Expected branch: `feature/dev-0052-codex-windows-service-sandbox`
- Review report: `docs/developer-reviews/REVIEW-0052.md`

## Goal
Resolve the remaining production blocker in the real TerrainEngine DEV-0007 pilot. TrailTrainer.Developer successfully starts Codex under the configured named Windows service account with the correct executable, working directory and user-profile environment. Codex then fails when its internal Windows sandbox runner attempts even harmless commands, reporting runner pipe connection timeouts.

## Required Investigation First
Before changing architecture, reproduce and isolate: Windows Service -> Codex CLI -> Codex command execution -> harmless command such as Get-Date. Inspect the installed Codex CLI help/configuration and use only supported execution/sandbox options. Do not guess undocumented switches.

## Requirements
- Implement the smallest supported Codex configuration that works in a non-interactive Windows Service context.
- Make the selected sandbox/execution mode explicit, configurable and startup-validated.
- Never silently enable unrestricted execution.
- Preserve safe ArgumentList-based process invocation.
- Preserve timeout, cancellation and process-tree termination.
- Preserve DEV-0050 validation: exit code 0 alone is never success.
- Preserve DEV-0051 bounded, secret-safe stdout/stderr and environment diagnostics.
- Classify the known Windows runner-pipe timeout distinctly from a missing/invalid review.
- Do not redesign the worker architecture unless investigation proves that no supported CLI configuration works from the service context.

## Compatibility Probe
Add an explicit production diagnostic command/probe using the same Codex executor/environment as normal task execution. It must use a safe temporary working directory, execute only a harmless command, have a short timeout, capture bounded stdout/stderr, make no Git/GitHub changes and modify no user repository. Normal `health` must not unexpectedly make a networked Codex request.

## Tests
Cover configured compatibility arguments, invalid modes, safe defaults, probe working directory and timeout, stdout/stderr capture, runner-pipe timeout classification, DEV-0050 BranchCreated retry semantics, cancellation/process-tree termination, DEV-0051 secret-safe diagnostics and DEV-0049 recovery. Tests must not invoke real Codex, GitHub, network access or mutate Windows SCM.

## Production Gates
Gate A must prove: Windows Service context -> configured Codex CLI -> Codex command execution -> harmless command succeeds.

Do not retry DEV-0007 until Gate A succeeds.

Gate B then verifies the complete DEV-0007 lifecycle: clean main -> service -> branch -> Codex implementation -> review -> validation -> commit -> push -> PR -> gates -> merge -> cleanup.

## Safety
Never expose/copy Codex credentials. Never invoke Codex on main. Never reset/clean/stash/delete user work. Never weaken Git/GitHub gates. Any broader Codex sandbox permission must be explicit, documented, configurable and justified.

## Verification
Run `dotnet build`, `dotnet test`, and `git diff --check`. Require 0 errors, no new warnings, all tests passing and no real external effects in automated tests.

## Codex Completion Protocol
Create `docs/developer-reviews/REVIEW-0052.md` containing: Status, Summary, Production Failure Reproduced, Root Cause, Supported Codex Execution Modes Investigated, Chosen Compatibility Strategy, Security/Sandbox Tradeoffs, Configuration Changes, Compatibility Probe, Failure Classification, Requirements Implemented, Files Created/Modified/Deleted, Tests Added, Verification, Gate A, Gate B, Deviations, Open Issues, TerrainEngine DEV-0007 Retry Readiness, and Commit and Push.

Do not modify this Developer Task. Do not create a commit. Do not push.
