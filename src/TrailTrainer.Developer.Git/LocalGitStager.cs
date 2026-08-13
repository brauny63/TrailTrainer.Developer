using TrailTrainer.Developer.Core;

namespace TrailTrainer.Developer.Git;

public sealed class LocalGitStager : IGitStager
{
    private readonly IGitRepositoryStatusProvider repositoryStatusProvider;

    public LocalGitStager()
        : this(new LocalGitRepositoryStatusProvider())
    {
    }

    public LocalGitStager(IGitRepositoryStatusProvider repositoryStatusProvider)
    {
        this.repositoryStatusProvider = repositoryStatusProvider
            ?? throw new ArgumentNullException(nameof(repositoryStatusProvider));
    }

    public async Task<GitStageResult> StageAllAsync(
        string directoryPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        var repositoryStatus = await repositoryStatusProvider.GetStatusAsync(
            directoryPath,
            cancellationToken);
        var repositoryRoot = GetRepositoryRoot(repositoryStatus, directoryPath);

        var stageResult = await GitProcessRunner.RunAsync(
            repositoryRoot,
            cancellationToken,
            "add",
            "--all");
        stageResult.EnsureSuccess("stage all repository changes");

        var hasStagedChanges = await GitIndex.HasStagedChangesAsync(
            repositoryRoot,
            cancellationToken);
        return new GitStageResult(repositoryRoot, hasStagedChanges);
    }

    private static string GetRepositoryRoot(
        GitRepositoryStatus repositoryStatus,
        string directoryPath)
    {
        if (!repositoryStatus.IsRepository || repositoryStatus.RepositoryRoot is null)
        {
            throw new InvalidOperationException(
                $"Directory '{Path.GetFullPath(directoryPath)}' is not inside a Git working tree.");
        }

        return repositoryStatus.RepositoryRoot;
    }
}
