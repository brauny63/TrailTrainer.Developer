namespace TrailTrainer.Developer.Core;

public sealed record DeveloperTaskWorkflowResult(
    DeveloperTaskId TaskId,
    DeveloperTaskGatedCompletionResult Completion,
    PullRequestEnsureResult PullRequest);
