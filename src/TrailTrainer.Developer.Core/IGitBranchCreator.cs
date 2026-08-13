namespace TrailTrainer.Developer.Core;

public interface IGitBranchCreator
{
    Task<GitBranchCreationResult> CreateAsync(
        string directoryPath,
        string branchName,
        CancellationToken cancellationToken = default);
}
