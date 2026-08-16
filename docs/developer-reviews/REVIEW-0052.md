# REVIEW-0052 – Codex Windows Service Sandbox Compatibility

## Status
READY FOR REVIEW

## Summary
Codex execution now passes an explicit, startup-validated sandbox mode and approval policy through `ProcessStartInfo.ArgumentList`. The safe default is `workspace-write`; the Windows Service deployment can explicitly select `danger-full-access` to avoid the failing internal Windows sandbox runner without using Codex's dangerous combined bypass switch. A separate `codex-probe` production command exercises the same executor and service-user environment in an isolated temporary directory.

## Production Failure Reproduced
The reported production chain was isolated to Codex command execution after successful service-account process startup, working-directory selection, and profile resolution. Automated regression coverage reproduces the characteristic `runner pipe ... timed out` diagnostic and verifies its classification. A real Windows Service/credentialed Codex request was not run from the development sandbox because that would be an external production effect and the installed service configuration is not available here.

## Root Cause
Codex's default `workspace-write` execution uses its internal Windows restricted-token runner. In the named non-interactive service context that runner cannot establish its pipe, although the outer Codex process starts successfully. The failure is therefore neither executable discovery nor missing-review validation.

## Supported Codex Execution Modes Investigated
The installed executable `C:\Users\braujoh\.codex\.sandbox-bin\codex.exe` reports `codex-cli 0.147.0-alpha.6.6`. `codex exec --help` documents `--sandbox` values `read-only`, `workspace-write`, and `danger-full-access`, and `--ask-for-approval` values `untrusted`, `on-request`, and `never`. It also documents `--dangerously-bypass-approvals-and-sandbox`; that switch was deliberately not selected. `codex sandbox --help` requires a named permission profile, so no undocumented profile name was guessed.

## Chosen Compatibility Strategy
`CodexExecution:SandboxMode` and `CodexExecution:ApprovalPolicy` are passed explicitly on every normal and probe execution. Defaults are `workspace-write` and `never`. The affected production service must explicitly configure `SandboxMode=danger-full-access`; this is the smallest documented mode that avoids model-generated commands being routed through the failing Windows sandbox runner. `--skip-git-repo-check` supports the isolated probe directory.

## Security/Sandbox Tradeoffs
`danger-full-access` removes Codex's internal filesystem sandbox for model-generated commands and must be an explicit deployment choice. It is not silently enabled and the even broader combined bypass flag is never emitted. Existing branch, dirty-worktree, review, Git, GitHub, commit, push, and merge gates remain unchanged. The service account and host OS permissions remain the external security boundary.

## Configuration Changes
- `CodexExecution:SandboxMode`: `read-only`, `workspace-write` (default), or `danger-full-access`.
- `CodexExecution:ApprovalPolicy`: `untrusted`, `on-request`, or `never` (default).
- `CodexExecution:CompatibilityProbeTimeout`: positive duration, default two minutes.
- Invalid values fail options validation at startup.

## Compatibility Probe
Run `TrailTrainer.Developer.Host.exe codex-probe` under the configured service identity and environment. It creates a unique directory below the OS temporary directory, invokes the same Codex executable/options/profile handling with an instruction limited to `Get-Date`, uses the short probe timeout, captures bounded stdout/stderr, reports the classified result, and removes the temporary directory. It does not open a user repository or perform Git/GitHub changes. The normal `health` command remains configuration-only and never invokes Codex or the network.

## Failure Classification
Bounded stdout/stderr containing both a runner-pipe marker and timeout marker maps to `CodexExecutionFailureKind.RunnerPipeTimeout`. Workflow diagnostics identify this separately from process timeout, nonzero exit, missing review, and invalid review. State remains `BranchCreated`.

## Requirements Implemented
- Explicit supported compatibility arguments with safe defaults and startup validation.
- No silent unrestricted execution and no dangerous combined bypass switch.
- Preserved `ArgumentList`, timeout, cancellation, and process-tree termination.
- Preserved DEV-0050 post-execution branch/review validation and retry state.
- Preserved DEV-0051 bounded output and whitelisted environment diagnostics.
- Distinct Windows runner-pipe timeout classification.
- Isolated, bounded, harmless production probe; normal health remains offline.
- No worker architecture redesign.

## Files Created/Modified/Deleted
Created: `src/TrailTrainer.Developer.Host/ICodexCompatibilityProbe.cs`, `src/TrailTrainer.Developer.Host/CodexCompatibilityProbeCommand.cs`, and this review. Modified: `CodexTaskExecutionResult.cs`, `CodexExecutionOptions.cs`, `CodexCliTaskExecutor.cs`, production DI registration, Host `Program.cs`, `DeveloperTaskWorkflow.cs`, fake Codex CLI, execution integration tests, and production registration tests. Deleted: none.

## Tests Added
Tests cover safe/default and configured compatibility arguments, absence of the dangerous bypass switch, invalid modes, isolated probe timeout, bounded capture via the shared executor, runner-pipe classification, and retained `BranchCreated` semantics. Existing tests continue covering DEV-0050 retry/review validation, cancellation/process-tree termination, DEV-0051 secret-safe diagnostics, and DEV-0049 interrupted-start recovery. Tests use only the fake CLI and make no network, GitHub, SCM, or user-repository changes.

## Verification
- `dotnet build --no-restore`: passed, 0 errors, 0 warnings.
- `dotnet test --no-build --no-restore`: passed, 827 passed, 0 failed, 0 skipped.
- `git diff --check`: passed.

## Gate A
PENDING PRODUCTION EXECUTION. Configure the named Windows Service account with `SandboxMode=danger-full-access` and `ApprovalPolicy=never`, then run `codex-probe` in that same service context. Gate A succeeds only when the configured Codex CLI executes `Get-Date` successfully. Do not retry DEV-0007 before this result is recorded.

## Gate B
NOT RUN. It is correctly blocked by pending Gate A. After Gate A succeeds, verify the complete DEV-0007 lifecycle specified by the task.

## Deviations
The real service-context Gate A and the subsequent Gate B were not executed from the development sandbox; no production service configuration, Codex credential use, repository mutation, push, PR, or merge was authorized or available here.

## Open Issues
The production operator must explicitly accept the weaker internal sandbox boundary and run Gate A. If `danger-full-access` still produces a runner-pipe timeout, the captured probe output is the evidence required before considering an architecture change.

## TerrainEngine DEV-0007 Retry Readiness
NOT READY UNTIL GATE A SUCCEEDS.

## Commit and Push
No commit created. No push performed.
