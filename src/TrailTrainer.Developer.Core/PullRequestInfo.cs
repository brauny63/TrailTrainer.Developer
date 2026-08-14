namespace TrailTrainer.Developer.Core;

public sealed record PullRequestInfo(
    int Number,
    Uri Url,
    string Title,
    string HeadBranch,
    string BaseBranch,
    bool IsDraft);
