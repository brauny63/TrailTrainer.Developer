namespace TrailTrainer.Developer.Core;

public interface IPullRequestMerger
{
    Task<PullRequestMergeResult> MergeAsync(
        GitHubRepositoryIdentity repository,
        int pullRequestNumber,
        string expectedHeadSha,
        PullRequestMergeMethod method,
        string? commitTitle = null,
        string? commitMessage = null,
        CancellationToken cancellationToken = default);
}
