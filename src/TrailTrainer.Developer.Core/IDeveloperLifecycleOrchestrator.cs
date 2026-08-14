namespace TrailTrainer.Developer.Core;

public interface IDeveloperLifecycleOrchestrator
{
    Task<DeveloperLifecycleResult> ExecuteAsync(
        string developerTaskFilePath,
        string repositoryDirectoryPath,
        string expectedRepositoryName,
        string commitMessage,
        string gitRemoteName,
        bool setUpstream,
        GitHubRepositoryIdentity gitHubRepository,
        string pullRequestBaseBranch,
        string? pullRequestBody,
        bool pullRequestDraft,
        PullRequestMergeMethod mergeMethod,
        string? mergeCommitTitle,
        string? mergeCommitMessage,
        bool deleteRemoteBranch,
        CancellationToken cancellationToken = default);
}
