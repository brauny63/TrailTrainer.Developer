using System.Diagnostics;
using TrailTrainer.Developer.Core;

namespace TrailTrainer.Developer.Git;

public sealed class LocalGitRepositoryStatusProvider : IGitRepositoryStatusProvider
{
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

        var rootResult = await RunGitAsync(
            workingDirectory,
            cancellationToken,
            "rev-parse",
            "--show-toplevel");

        if (rootResult.ExitCode != 0 && IsNotRepository(rootResult.StandardError))
        {
            return GitRepositoryStatus.NotRepository;
        }

        EnsureSuccess(rootResult, "determine the repository root");

        var repositoryRoot = rootResult.StandardOutput.Trim();
        var branchResult = await RunGitAsync(
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
            _ => throw CreateGitException(branchResult, "determine the current branch")
        };

        var statusResult = await RunGitAsync(
            repositoryRoot,
            cancellationToken,
            "status",
            "--porcelain");
        EnsureSuccess(statusResult, "determine the working-tree status");

        return new GitRepositoryStatus(
            IsRepository: true,
            RepositoryRoot: Path.GetFullPath(repositoryRoot),
            CurrentBranch: currentBranch,
            HasUncommittedChanges: !string.IsNullOrWhiteSpace(statusResult.StandardOutput));
    }

    private static bool IsNotRepository(string standardError) =>
        standardError.Contains("not a git repository", StringComparison.OrdinalIgnoreCase);

    private static string? NullIfWhiteSpace(string value)
    {
        var trimmedValue = value.Trim();
        return trimmedValue.Length == 0 ? null : trimmedValue;
    }

    private static void EnsureSuccess(GitProcessResult result, string operation)
    {
        if (result.ExitCode != 0)
        {
            throw CreateGitException(result, operation);
        }
    }

    private static InvalidOperationException CreateGitException(
        GitProcessResult result,
        string operation)
    {
        var details = string.IsNullOrWhiteSpace(result.StandardError)
            ? "Git did not provide error output."
            : result.StandardError.Trim();

        return new InvalidOperationException(
            $"Git failed to {operation} (exit code {result.ExitCode}): {details}");
    }

    private static async Task<GitProcessResult> RunGitAsync(
        string workingDirectory,
        CancellationToken cancellationToken,
        params string[] arguments)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException("The Git process could not be started.");
            }
        }
        catch (Exception exception) when (exception is not InvalidOperationException)
        {
            throw new InvalidOperationException(
                "The Git executable could not be started. Ensure Git is installed and available on PATH.",
                exception);
        }

        var standardOutputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        try
        {
            await process.WaitForExitAsync(cancellationToken);
            return new GitProcessResult(
                process.ExitCode,
                await standardOutputTask,
                await standardErrorTask);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            throw;
        }
    }

    private sealed record GitProcessResult(
        int ExitCode,
        string StandardOutput,
        string StandardError);
}
