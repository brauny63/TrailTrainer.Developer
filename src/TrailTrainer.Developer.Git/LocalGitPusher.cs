using TrailTrainer.Developer.Core;

namespace TrailTrainer.Developer.Git;

public sealed class LocalGitPusher : IGitPusher
{
    private readonly IGitRepositoryStatusProvider repositoryStatusProvider;

    public LocalGitPusher()
        : this(new LocalGitRepositoryStatusProvider())
    {
    }

    public LocalGitPusher(IGitRepositoryStatusProvider repositoryStatusProvider)
    {
        this.repositoryStatusProvider = repositoryStatusProvider
            ?? throw new ArgumentNullException(nameof(repositoryStatusProvider));
    }

    public async Task<GitPushResult> PushAsync(
        string directoryPath,
        string remoteName,
        bool setUpstream,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(remoteName);

        var repositoryStatus = await repositoryStatusProvider.GetStatusAsync(
            directoryPath,
            cancellationToken);
        if (!repositoryStatus.IsRepository || repositoryStatus.RepositoryRoot is null)
        {
            throw new InvalidOperationException(
                $"Directory '{Path.GetFullPath(directoryPath)}' is not inside a Git working tree.");
        }

        if (repositoryStatus.CurrentBranch is null)
        {
            throw new InvalidOperationException(
                $"Repository '{repositoryStatus.RepositoryRoot}' has a detached HEAD and cannot push a current branch.");
        }

        var remoteResult = await GitProcessRunner.RunAsync(
            repositoryStatus.RepositoryRoot,
            cancellationToken,
            "remote");
        remoteResult.EnsureSuccess("list configured remotes");

        var remoteExists = remoteResult.StandardOutput
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Contains(remoteName, StringComparer.Ordinal);
        if (!remoteExists)
        {
            throw new InvalidOperationException(
                $"Remote '{remoteName}' does not exist in repository '{repositoryStatus.RepositoryRoot}'.");
        }

        var arguments = setUpstream
            ? new[] { "push", "--set-upstream", remoteName, repositoryStatus.CurrentBranch }
            : new[] { "push", remoteName, repositoryStatus.CurrentBranch };
        var pushResult = await GitProcessRunner.RunAsync(
            repositoryStatus.RepositoryRoot,
            cancellationToken,
            arguments);
        pushResult.EnsureSuccess(
            $"push branch '{repositoryStatus.CurrentBranch}' to remote '{remoteName}'");

        return new GitPushResult(
            repositoryStatus.RepositoryRoot,
            remoteName,
            repositoryStatus.CurrentBranch,
            setUpstream);
    }
}
