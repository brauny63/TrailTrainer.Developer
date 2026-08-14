namespace TrailTrainer.Developer.Core;

public interface IDeveloperTaskWorkflow
{
    Task<DeveloperTaskWorkflowResult> ExecuteAsync(
        string developerTaskFilePath,
        string repositoryDirectoryPath,
        string expectedRepositoryName,
        string commitMessage,
        string gitRemoteName,
        bool setUpstream,
        GitHubRepositoryIdentity gitHubRepository,
        string pullRequestBaseBranch,
        string? pullRequestBody = null,
        bool pullRequestDraft = false,
        CancellationToken cancellationToken = default);
}
