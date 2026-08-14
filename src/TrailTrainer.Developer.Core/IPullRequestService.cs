namespace TrailTrainer.Developer.Core;

public interface IPullRequestService
{
    Task<PullRequestEnsureResult> EnsureOpenAsync(
        GitHubRepositoryIdentity repository,
        string headBranch,
        string baseBranch,
        string title,
        string? body = null,
        bool draft = false,
        CancellationToken cancellationToken = default);
}
