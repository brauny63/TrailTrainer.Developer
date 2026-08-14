namespace TrailTrainer.Developer.Core;

public sealed record PullRequestEnsureResult(
    PullRequestInfo PullRequest,
    bool Created);
