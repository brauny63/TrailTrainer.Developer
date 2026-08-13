namespace TrailTrainer.Developer.Core;

public interface IGitCommitter
{
    Task<GitCommitResult> CommitAsync(
        string directoryPath,
        string commitMessage,
        CancellationToken cancellationToken = default);
}
