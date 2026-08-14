using TrailTrainer.Developer.Core;

namespace TrailTrainer.Developer.Tasks;

public sealed class DeveloperTaskCompleter : IDeveloperTaskCompleter
{
    private readonly IDeveloperTaskParser taskParser;
    private readonly IGitRepositoryStatusProvider repositoryStatusProvider;
    private readonly IGitStager stager;
    private readonly IGitCommitter committer;
    private readonly IGitPusher pusher;

    public DeveloperTaskCompleter(
        IDeveloperTaskParser taskParser,
        IGitRepositoryStatusProvider repositoryStatusProvider,
        IGitStager stager,
        IGitCommitter committer,
        IGitPusher pusher)
    {
        this.taskParser = taskParser ?? throw new ArgumentNullException(nameof(taskParser));
        this.repositoryStatusProvider = repositoryStatusProvider
            ?? throw new ArgumentNullException(nameof(repositoryStatusProvider));
        this.stager = stager ?? throw new ArgumentNullException(nameof(stager));
        this.committer = committer ?? throw new ArgumentNullException(nameof(committer));
        this.pusher = pusher ?? throw new ArgumentNullException(nameof(pusher));
    }

    public async Task<DeveloperTaskCompletionResult> CompleteAsync(
        string developerTaskFilePath,
        string repositoryDirectoryPath,
        string expectedRepositoryName,
        string commitMessage,
        string remoteName,
        bool setUpstream,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedRepositoryName);
        ArgumentException.ThrowIfNullOrWhiteSpace(commitMessage);
        ArgumentException.ThrowIfNullOrWhiteSpace(remoteName);

        var task = await taskParser.ParseAsync(developerTaskFilePath, cancellationToken);
        if (!string.Equals(task.Repository, expectedRepositoryName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Developer Task repository '{task.Repository}' does not match expected repository " +
                $"'{expectedRepositoryName}'.");
        }

        var repositoryStatus = await repositoryStatusProvider.GetStatusAsync(
            repositoryDirectoryPath,
            cancellationToken);
        ValidateRepositoryStatus(repositoryStatus, task.ExpectedBranch);

        var stageResult = await stager.StageAllAsync(repositoryDirectoryPath, cancellationToken);
        if (!stageResult.HasStagedChanges)
        {
            throw new InvalidOperationException(
                $"Repository '{stageResult.RepositoryRoot}' has no staged changes to commit.");
        }

        var commitResult = await committer.CommitAsync(
            repositoryDirectoryPath,
            commitMessage,
            cancellationToken);
        var pushResult = await pusher.PushAsync(
            repositoryDirectoryPath,
            remoteName,
            setUpstream,
            cancellationToken);

        return new DeveloperTaskCompletionResult(
            task.Id,
            task.Title,
            repositoryStatus.RepositoryRoot!,
            pushResult.BranchName,
            commitResult.CommitSha,
            commitResult.CommitMessage,
            pushResult.RemoteName,
            pushResult.SetUpstream,
            task.FilePath,
            task.ReviewReportPath);
    }

    private static void ValidateRepositoryStatus(
        GitRepositoryStatus repositoryStatus,
        string expectedBranch)
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

        if (!string.Equals(repositoryStatus.CurrentBranch, expectedBranch, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Repository '{repositoryStatus.RepositoryRoot}' must be on task branch " +
                $"'{expectedBranch}', but is on '{repositoryStatus.CurrentBranch}'.");
        }

        if (!repositoryStatus.HasUncommittedChanges)
        {
            throw new InvalidOperationException(
                $"Repository '{repositoryStatus.RepositoryRoot}' has no uncommitted changes.");
        }
    }
}
