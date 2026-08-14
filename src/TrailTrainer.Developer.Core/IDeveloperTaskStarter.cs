namespace TrailTrainer.Developer.Core;

public interface IDeveloperTaskStarter
{
    Task<DeveloperTaskStartResult> StartAsync(
        string developerTaskFilePath,
        string repositoryDirectoryPath,
        string expectedRepositoryName,
        CancellationToken cancellationToken = default);
}
