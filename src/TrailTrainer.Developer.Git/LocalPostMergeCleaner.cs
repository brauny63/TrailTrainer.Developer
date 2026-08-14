using TrailTrainer.Developer.Core;

namespace TrailTrainer.Developer.Git;

public sealed class LocalPostMergeCleaner : IPostMergeCleaner
{
    private readonly IGitRepositoryStatusProvider statusProvider;

    public LocalPostMergeCleaner(IGitRepositoryStatusProvider? statusProvider = null)
    {
        this.statusProvider = statusProvider ?? new LocalGitRepositoryStatusProvider();
    }

    public async Task<PostMergeCleanupResult> CleanupAsync(
        string repositoryDirectory,
        GitHubRepositoryIdentity repository,
        int pullRequestNumber,
        PullRequestMergeResult mergeResult,
        string featureBranch,
        string baseBranch,
        string remoteName,
        bool deleteRemoteBranch,
        CancellationToken cancellationToken = default)
    {
        ValidateInputs(
            repositoryDirectory,
            repository,
            pullRequestNumber,
            mergeResult,
            featureBranch,
            baseBranch,
            remoteName);

        var status = await statusProvider.GetStatusAsync(repositoryDirectory, cancellationToken);
        if (!status.IsRepository || string.IsNullOrWhiteSpace(status.RepositoryRoot))
        {
            throw new InvalidOperationException("The supplied directory is not inside a Git repository.");
        }

        if (status.HasUncommittedChanges)
        {
            throw new InvalidOperationException(
                "Post-merge cleanup requires a clean working tree and index.");
        }

        if (string.IsNullOrWhiteSpace(status.CurrentBranch))
        {
            throw new InvalidOperationException("Post-merge cleanup is not allowed from a detached HEAD.");
        }

        var repositoryRoot = Path.GetFullPath(status.RepositoryRoot);
        await ValidateBranchNameAsync(repositoryRoot, featureBranch, "feature", cancellationToken);
        await ValidateBranchNameAsync(repositoryRoot, baseBranch, "base", cancellationToken);
        await RequireRemoteAsync(repositoryRoot, remoteName, cancellationToken);
        await RequireLocalBranchAsync(repositoryRoot, baseBranch, cancellationToken);

        if (!string.Equals(status.CurrentBranch, baseBranch, StringComparison.Ordinal))
        {
            var switchResult = await GitProcessRunner.RunAsync(
                repositoryRoot,
                cancellationToken,
                "switch",
                baseBranch);
            switchResult.EnsureSuccess($"switch to base branch '{baseBranch}'");
        }

        var pullResult = await GitProcessRunner.RunAsync(
            repositoryRoot,
            cancellationToken,
            "pull",
            "--ff-only",
            "--",
            remoteName,
            baseBranch);
        pullResult.EnsureSuccess(
            $"fast-forward base branch '{baseBranch}' from remote '{remoteName}'");

        var localBranchDeleted = false;
        if (await LocalBranchExistsAsync(repositoryRoot, featureBranch, cancellationToken))
        {
            var deleteResult = await GitProcessRunner.RunAsync(
                repositoryRoot,
                cancellationToken,
                "branch",
                "-d",
                "--",
                featureBranch);
            deleteResult.EnsureSuccess($"delete merged local branch '{featureBranch}'");
            localBranchDeleted = true;
        }

        var remoteBranchDeleted = false;
        if (deleteRemoteBranch &&
            await RemoteBranchExistsAsync(repositoryRoot, remoteName, featureBranch, cancellationToken))
        {
            var deleteRemoteResult = await GitProcessRunner.RunAsync(
                repositoryRoot,
                cancellationToken,
                "push",
                "--delete",
                "--",
                remoteName,
                featureBranch);
            deleteRemoteResult.EnsureSuccess(
                $"delete remote branch '{remoteName}/{featureBranch}'");
            remoteBranchDeleted = true;
        }

        return new PostMergeCleanupResult(
            repositoryRoot,
            baseBranch,
            featureBranch,
            localBranchDeleted,
            remoteBranchDeleted);
    }

    private static void ValidateInputs(
        string repositoryDirectory,
        GitHubRepositoryIdentity repository,
        int pullRequestNumber,
        PullRequestMergeResult mergeResult,
        string featureBranch,
        string baseBranch,
        string remoteName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryDirectory);
        ArgumentNullException.ThrowIfNull(repository);
        if (pullRequestNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pullRequestNumber));
        }

        ArgumentNullException.ThrowIfNull(mergeResult);
        if (!mergeResult.Merged)
        {
            throw new InvalidOperationException("Post-merge cleanup requires a confirmed successful merge.");
        }

        if (mergeResult.PullRequestNumber != pullRequestNumber)
        {
            throw new InvalidOperationException(
                "The merge result does not belong to the requested Pull Request.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(featureBranch);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseBranch);
        ArgumentException.ThrowIfNullOrWhiteSpace(remoteName);
        if (string.Equals(featureBranch, baseBranch, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The feature branch and protected base branch must differ.",
                nameof(featureBranch));
        }
    }

    private static async Task ValidateBranchNameAsync(
        string repositoryRoot,
        string branchName,
        string role,
        CancellationToken cancellationToken)
    {
        var result = await GitProcessRunner.RunAsync(
            repositoryRoot,
            cancellationToken,
            "check-ref-format",
            "--branch",
            branchName);
        if (result.ExitCode != 0)
        {
            throw new ArgumentException(
                $"The supplied {role} branch name '{branchName}' is not a valid Git branch name.",
                role == "feature" ? "featureBranch" : "baseBranch");
        }
    }

    private static async Task RequireRemoteAsync(
        string repositoryRoot,
        string remoteName,
        CancellationToken cancellationToken)
    {
        var result = await GitProcessRunner.RunAsync(
            repositoryRoot,
            cancellationToken,
            "remote",
            "get-url",
            "--",
            remoteName);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Git remote '{remoteName}' does not exist in the repository.");
        }
    }

    private static async Task RequireLocalBranchAsync(
        string repositoryRoot,
        string branchName,
        CancellationToken cancellationToken)
    {
        if (!await LocalBranchExistsAsync(repositoryRoot, branchName, cancellationToken))
        {
            throw new InvalidOperationException(
                $"Local base branch '{branchName}' does not exist.");
        }
    }

    private static async Task<bool> LocalBranchExistsAsync(
        string repositoryRoot,
        string branchName,
        CancellationToken cancellationToken)
    {
        var result = await GitProcessRunner.RunAsync(
            repositoryRoot,
            cancellationToken,
            "show-ref",
            "--verify",
            "--quiet",
            $"refs/heads/{branchName}");
        return result.ExitCode switch
        {
            0 => true,
            1 => false,
            _ => throw result.CreateException($"check local branch '{branchName}'")
        };
    }

    private static async Task<bool> RemoteBranchExistsAsync(
        string repositoryRoot,
        string remoteName,
        string branchName,
        CancellationToken cancellationToken)
    {
        var result = await GitProcessRunner.RunAsync(
            repositoryRoot,
            cancellationToken,
            "ls-remote",
            "--exit-code",
            "--heads",
            "--",
            remoteName,
            $"refs/heads/{branchName}");
        return result.ExitCode switch
        {
            0 => true,
            2 => false,
            _ => throw result.CreateException($"check remote branch '{remoteName}/{branchName}'")
        };
    }
}
