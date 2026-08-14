namespace TrailTrainer.Developer.Core;

public interface IPostMergeCleaner
{
    Task<PostMergeCleanupResult> CleanupAsync(
        string repositoryDirectory,
        GitHubRepositoryIdentity repository,
        int pullRequestNumber,
        PullRequestMergeResult mergeResult,
        string featureBranch,
        string baseBranch,
        string remoteName,
        bool deleteRemoteBranch,
        CancellationToken cancellationToken = default);
}
