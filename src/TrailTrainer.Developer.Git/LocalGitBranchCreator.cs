using TrailTrainer.Developer.Core;

namespace TrailTrainer.Developer.Git;

public sealed class LocalGitBranchCreator : IGitBranchCreator
{
    private readonly IGitRepositoryStatusProvider repositoryStatusProvider;

    public LocalGitBranchCreator()
        : this(new LocalGitRepositoryStatusProvider())
    {
    }

    public LocalGitBranchCreator(IGitRepositoryStatusProvider repositoryStatusProvider)
    {
        this.repositoryStatusProvider = repositoryStatusProvider
            ?? throw new ArgumentNullException(nameof(repositoryStatusProvider));
    }

    public async Task<GitBranchCreationResult> CreateAsync(
        string directoryPath,
        string branchName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(branchName);

        var repositoryStatus = await repositoryStatusProvider.GetStatusAsync(
            directoryPath,
            cancellationToken);
        if (!repositoryStatus.IsRepository || repositoryStatus.RepositoryRoot is null)
        {
            throw new InvalidOperationException(
                $"Directory '{Path.GetFullPath(directoryPath)}' is not inside a Git working tree.");
        }

        var branchReference = $"refs/heads/{branchName}";
        var existingBranchResult = await GitProcessRunner.RunAsync(
            repositoryStatus.RepositoryRoot,
            cancellationToken,
            "show-ref",
            "--verify",
            "--quiet",
            branchReference);

        if (existingBranchResult.ExitCode == 0)
        {
            throw new InvalidOperationException(
                $"A local branch named '{branchName}' already exists in repository " +
                $"'{repositoryStatus.RepositoryRoot}'.");
        }

        if (existingBranchResult.ExitCode != 1)
        {
            throw existingBranchResult.CreateException("check whether the local branch already exists");
        }

        var createResult = await GitProcessRunner.RunAsync(
            repositoryStatus.RepositoryRoot,
            cancellationToken,
            "switch",
            "-c",
            branchName);
        createResult.EnsureSuccess($"create and switch to branch '{branchName}'");

        return new GitBranchCreationResult(
            repositoryStatus.RepositoryRoot,
            branchName);
    }
}
