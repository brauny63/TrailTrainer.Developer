namespace TrailTrainer.Developer.Core;

public interface IDeveloperTaskGatedCompleter
{
    Task<DeveloperTaskGatedCompletionResult> CompleteAsync(
        string developerTaskFilePath,
        string repositoryDirectoryPath,
        string expectedRepositoryName,
        string commitMessage,
        string remoteName,
        bool setUpstream,
        CancellationToken cancellationToken = default);
}
