namespace TrailTrainer.Developer.Core;

public interface IPullRequestMergeGate
{
    Task<PullRequestGatedMergeResult> MergeAsync(
        GitHubRepositoryIdentity repository,
        int pullRequestNumber,
        PullRequestMergeMethod method,
        string? commitTitle = null,
        string? commitMessage = null,
        CancellationToken cancellationToken = default);
}
