namespace TrailTrainer.Developer.Core;

public sealed record InitialDeveloperTaskIntakeRequest(
    bool Enabled,
    string RepositoryPath,
    string RepositoryName,
    string GitHubOwner,
    string BaseBranch,
    string RemoteName,
    PullRequestMergeMethod MergeMethod,
    string? MergeCommitTitle,
    string? MergeCommitMessage,
    bool DeleteRemoteBranch);
