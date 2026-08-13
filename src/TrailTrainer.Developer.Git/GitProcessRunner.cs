using System.Diagnostics;

namespace TrailTrainer.Developer.Git;

internal static class GitProcessRunner
{
    public static async Task<GitProcessResult> RunAsync(
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
}

internal sealed record GitProcessResult(
    int ExitCode,
    string StandardOutput,
    string StandardError)
{
    public void EnsureSuccess(string operation)
    {
        if (ExitCode != 0)
        {
            throw CreateException(operation);
        }
    }

    public InvalidOperationException CreateException(string operation)
    {
        var details = string.IsNullOrWhiteSpace(StandardError)
            ? "Git did not provide error output."
            : StandardError.Trim();

        return new InvalidOperationException(
            $"Git failed to {operation} (exit code {ExitCode}): {details}");
    }
}
