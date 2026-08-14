namespace TrailTrainer.Developer.Core;

public interface IDeveloperTaskCompleter
{
    Task<DeveloperTaskCompletionResult> CompleteAsync(
        string developerTaskFilePath,
        string repositoryDirectoryPath,
        string expectedRepositoryName,
        string commitMessage,
        string remoteName,
        bool setUpstream,
        CancellationToken cancellationToken = default);
}
