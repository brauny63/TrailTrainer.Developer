using System.Diagnostics;
using Microsoft.Extensions.Options;
using TrailTrainer.Developer.Core;

namespace TrailTrainer.Developer.Host;

public sealed class CodexCliTaskExecutor : ICodexTaskExecutor
{
    private readonly CodexExecutionOptions options;

    public CodexCliTaskExecutor(IOptions<CodexExecutionOptions> options)
    {
        this.options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<CodexTaskExecutionResult> ExecuteAsync(
        CodexTaskExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var startInfo = new ProcessStartInfo
        {
            FileName = options.ExecutablePath,
            WorkingDirectory = Path.GetFullPath(request.RepositoryPath),
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add("exec");
        foreach (var argument in options.AdditionalArguments) startInfo.ArgumentList.Add(argument);
        startInfo.ArgumentList.Add(request.Instruction);

        using var process = new Process { StartInfo = startInfo };
        try
        {
            if (!process.Start()) throw new InvalidOperationException("The Codex process could not be started.");
        }
        catch (Exception exception) when (exception is not InvalidOperationException)
        {
            throw new InvalidOperationException($"The configured Codex executable '{options.ExecutablePath}' could not be started.", exception);
        }

        var output = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var error = process.StandardError.ReadToEndAsync(CancellationToken.None);
        using var timeout = new CancellationTokenSource(options.Timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            await process.WaitForExitAsync(linked.Token);
            return new CodexTaskExecutionResult(process.ExitCode, Bound(await output), Bound(await error));
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            Kill(process);
            await Task.WhenAll(output, error);
            return new CodexTaskExecutionResult(-1, Bound(output.Result), Bound(error.Result), true);
        }
        catch (OperationCanceledException)
        {
            Kill(process);
            throw;
        }
    }

    private string Bound(string value) => value.Length <= options.MaximumDiagnosticCharacters
        ? value : value[^options.MaximumDiagnosticCharacters..];
    private static void Kill(Process process)
    {
        if (!process.HasExited) process.Kill(entireProcessTree: true);
    }
}
