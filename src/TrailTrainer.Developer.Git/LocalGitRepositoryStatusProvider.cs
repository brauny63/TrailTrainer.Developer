using TrailTrainer.Developer.Core;

namespace TrailTrainer.Developer.Git;

public sealed class LocalGitRepositoryStatusProvider : IGitRepositoryStatusProvider
{
    private const int GitFatalExitCode = 128;

    public async Task<GitRepositoryStatus> GetStatusAsync(
        string directoryPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        var workingDirectory = Path.GetFullPath(directoryPath);
        if (!Directory.Exists(workingDirectory))
        {
            throw new DirectoryNotFoundException($"Directory '{workingDirectory}' does not exist.");
        }

        var repositoryCheckResult = await GitProcessRunner.RunAsync(
            workingDirectory,
            cancellationToken,
            "rev-parse",
            "--is-inside-work-tree");

        if (repositoryCheckResult.ExitCode == GitFatalExitCode)
        {
            return GitRepositoryStatus.NotRepository;
        }

        repositoryCheckResult.EnsureSuccess("determine whether the directory is inside a working tree");
        if (!bool.TryParse(repositoryCheckResult.StandardOutput.Trim(), out var isInsideWorkingTree))
        {
            throw repositoryCheckResult.CreateException(
                "return a valid working-tree indicator");
        }

        if (!isInsideWorkingTree)
        {
            return GitRepositoryStatus.NotRepository;
        }

        var rootResult = await GitProcessRunner.RunAsync(
            workingDirectory,
            cancellationToken,
            "rev-parse",
            "--show-toplevel");
        rootResult.EnsureSuccess("determine the repository root");

        var repositoryRoot = rootResult.StandardOutput.Trim();
        var branchResult = await GitProcessRunner.RunAsync(
            repositoryRoot,
            cancellationToken,
            "symbolic-ref",
            "--quiet",
            "--short",
            "HEAD");

        string? currentBranch = branchResult.ExitCode switch
        {
            0 => NullIfWhiteSpace(branchResult.StandardOutput),
            1 => null,
            _ => throw branchResult.CreateException("determine the current branch")
        };

        var statusResult = await GitProcessRunner.RunAsync(
            repositoryRoot,
            cancellationToken,
            "status",
            "--porcelain");
        statusResult.EnsureSuccess("determine the working-tree status");

        return new GitRepositoryStatus(
            IsRepository: true,
            RepositoryRoot: Path.GetFullPath(repositoryRoot),
            CurrentBranch: currentBranch,
            HasUncommittedChanges: !string.IsNullOrWhiteSpace(statusResult.StandardOutput));
    }

    private static string? NullIfWhiteSpace(string value)
    {
        var trimmedValue = value.Trim();
        return trimmedValue.Length == 0 ? null : trimmedValue;
    }

}
