namespace TrailTrainer.Developer.Core;

public interface IGitRepositoryStatusProvider
{
    Task<GitRepositoryStatus> GetStatusAsync(
        string directoryPath,
        CancellationToken cancellationToken = default);
}
