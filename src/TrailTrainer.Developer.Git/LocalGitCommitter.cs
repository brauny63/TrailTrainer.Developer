using TrailTrainer.Developer.Core;

namespace TrailTrainer.Developer.Git;

public sealed class LocalGitCommitter : IGitCommitter
{
    private readonly IGitRepositoryStatusProvider repositoryStatusProvider;

    public LocalGitCommitter()
        : this(new LocalGitRepositoryStatusProvider())
    {
    }

    public LocalGitCommitter(IGitRepositoryStatusProvider repositoryStatusProvider)
    {
        this.repositoryStatusProvider = repositoryStatusProvider
            ?? throw new ArgumentNullException(nameof(repositoryStatusProvider));
    }

    public async Task<GitCommitResult> CommitAsync(
        string directoryPath,
        string commitMessage,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(commitMessage);

        var repositoryStatus = await repositoryStatusProvider.GetStatusAsync(
            directoryPath,
            cancellationToken);
        if (!repositoryStatus.IsRepository || repositoryStatus.RepositoryRoot is null)
        {
            throw new InvalidOperationException(
                $"Directory '{Path.GetFullPath(directoryPath)}' is not inside a Git working tree.");
        }

        var repositoryRoot = repositoryStatus.RepositoryRoot;
        if (!await GitIndex.HasStagedChangesAsync(repositoryRoot, cancellationToken))
        {
            throw new InvalidOperationException(
                $"Repository '{repositoryRoot}' has no staged changes to commit.");
        }

        var commitResult = await GitProcessRunner.RunAsync(
            repositoryRoot,
            cancellationToken,
            "commit",
            "-m",
            commitMessage);
        commitResult.EnsureSuccess("create a commit from staged changes");

        var shaResult = await GitProcessRunner.RunAsync(
            repositoryRoot,
            cancellationToken,
            "rev-parse",
            "HEAD");
        shaResult.EnsureSuccess("determine the created commit SHA");

        return new GitCommitResult(
            repositoryRoot,
            shaResult.StandardOutput.Trim(),
            commitMessage);
    }
}
