using TrailTrainer.Developer.Core;

namespace TrailTrainer.Developer.Tasks;

public sealed class DeveloperTaskStarter : IDeveloperTaskStarter
{
    private readonly IDeveloperTaskParser taskParser;
    private readonly IGitRepositoryStatusProvider repositoryStatusProvider;
    private readonly IGitBranchCreator branchCreator;

    public DeveloperTaskStarter(
        IDeveloperTaskParser taskParser,
        IGitRepositoryStatusProvider repositoryStatusProvider,
        IGitBranchCreator branchCreator)
    {
        this.taskParser = taskParser ?? throw new ArgumentNullException(nameof(taskParser));
        this.repositoryStatusProvider = repositoryStatusProvider
            ?? throw new ArgumentNullException(nameof(repositoryStatusProvider));
        this.branchCreator = branchCreator ?? throw new ArgumentNullException(nameof(branchCreator));
    }

    public async Task<DeveloperTaskStartResult> StartAsync(
        string developerTaskFilePath,
        string repositoryDirectoryPath,
        string expectedRepositoryName,
        CancellationToken cancellationToken = default)
    {
        var task = await taskParser.ParseAsync(developerTaskFilePath, cancellationToken);

        ArgumentException.ThrowIfNullOrWhiteSpace(expectedRepositoryName);
        if (!string.Equals(task.Repository, expectedRepositoryName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Developer Task repository '{task.Repository}' does not match expected repository " +
                $"'{expectedRepositoryName}'.");
        }

        var repositoryStatus = await repositoryStatusProvider.GetStatusAsync(
            repositoryDirectoryPath,
            cancellationToken);
        ValidateRepositoryStatus(repositoryStatus);

        var branchResult = await branchCreator.CreateAsync(
            repositoryDirectoryPath,
            task.ExpectedBranch,
            cancellationToken);

        return new DeveloperTaskStartResult(
            task.Id,
            task.Title,
            repositoryStatus.RepositoryRoot!,
            repositoryStatus.CurrentBranch!,
            branchResult.BranchName,
            task.FilePath,
            task.ReviewReportPath);
    }

    private static void ValidateRepositoryStatus(GitRepositoryStatus repositoryStatus)
    {
        if (!repositoryStatus.IsRepository || repositoryStatus.RepositoryRoot is null)
        {
            throw new InvalidOperationException("The supplied directory is not inside a Git working tree.");
        }

        if (repositoryStatus.CurrentBranch is null)
        {
            throw new InvalidOperationException(
                $"Repository '{repositoryStatus.RepositoryRoot}' has a detached HEAD.");
        }

        if (!string.Equals(repositoryStatus.CurrentBranch, "main", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Repository '{repositoryStatus.RepositoryRoot}' must be on branch 'main', " +
                $"but is on '{repositoryStatus.CurrentBranch}'.");
        }

        if (repositoryStatus.HasUncommittedChanges)
        {
            throw new InvalidOperationException(
                $"Repository '{repositoryStatus.RepositoryRoot}' has uncommitted changes.");
        }
    }
}
