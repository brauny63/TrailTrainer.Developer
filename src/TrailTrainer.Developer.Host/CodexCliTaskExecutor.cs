using System.Diagnostics;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using TrailTrainer.Developer.Core;

namespace TrailTrainer.Developer.Host;

public sealed class CodexCliTaskExecutor : ICodexTaskExecutor, ICodexCompatibilityProbe
{
    private readonly CodexExecutionOptions options;
    private readonly ILogger<CodexCliTaskExecutor> logger;

    public CodexCliTaskExecutor(
        IOptions<CodexExecutionOptions> options,
        ILogger<CodexCliTaskExecutor>? logger = null)
    {
        this.options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        this.logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<CodexCliTaskExecutor>.Instance;
    }

    public async Task<CodexTaskExecutionResult> ExecuteAsync(
        CodexTaskExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        return await ExecuteProcessAsync(Path.GetFullPath(request.RepositoryPath), request.Instruction, options.Timeout,
            request.DeveloperTaskFilePath, cancellationToken);
    }

    public async Task<CodexTaskExecutionResult> ProbeAsync(CancellationToken cancellationToken = default)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"trailtrainer-codex-probe-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            return await ExecuteProcessAsync(directory,
                "Compatibility probe: execute only the harmless PowerShell command Get-Date, report its output, and make no other changes.",
                options.CompatibilityProbeTimeout, "<compatibility-probe>", cancellationToken);
        }
        finally
        {
            try { Directory.Delete(directory, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private async Task<CodexTaskExecutionResult> ExecuteProcessAsync(
        string workingDirectory, string instruction, TimeSpan executionTimeout, string context,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = options.ExecutablePath,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add("exec");
        foreach (var argument in options.AdditionalArguments) startInfo.ArgumentList.Add(argument);
        startInfo.ArgumentList.Add("--sandbox");
        startInfo.ArgumentList.Add(options.SandboxMode);
        startInfo.ArgumentList.Add("--ask-for-approval");
        startInfo.ArgumentList.Add(options.ApprovalPolicy);
        startInfo.ArgumentList.Add("--skip-git-repo-check");
        startInfo.ArgumentList.Add(instruction);
        ApplyUserProfileEnvironment(startInfo);

        logger.LogInformation(
            "Starting Codex for task {TaskFile} in repository {Repository}. Executable: {Executable}; working directory: {WorkingDirectory}; user: {User}; environment: {Environment}",
            context,
            workingDirectory,
            options.ExecutablePath,
            startInfo.WorkingDirectory,
            $"{Environment.UserDomainName}\\{Environment.UserName}",
            FormatEnvironment(startInfo));

        using var process = new Process { StartInfo = startInfo };
        try
        {
            if (!process.Start()) throw new InvalidOperationException("The Codex process could not be started.");
        }
        catch (Exception exception) when (exception is not InvalidOperationException)
        {
            logger.LogError(exception,
                "Codex process could not be started for task {TaskFile} in repository {Repository} with executable {Executable}.",
                context, workingDirectory, options.ExecutablePath);
            throw new InvalidOperationException($"The configured Codex executable '{options.ExecutablePath}' could not be started.", exception);
        }

        var output = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var error = process.StandardError.ReadToEndAsync(CancellationToken.None);
        using var timeout = new CancellationTokenSource(executionTimeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            await process.WaitForExitAsync(linked.Token);
            var stdout = Bound(await output);
            var stderr = Bound(await error);
            var result = new CodexTaskExecutionResult(process.ExitCode, stdout, stderr, false, Classify(stdout, stderr));
            LogCompletion(context, workingDirectory, result);
            return result;
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            Kill(process);
            await Task.WhenAll(output, error);
            var result = new CodexTaskExecutionResult(-1, Bound(output.Result), Bound(error.Result), true);
            LogCompletion(context, workingDirectory, result);
            return result;
        }
        catch (OperationCanceledException)
        {
            Kill(process);
            logger.LogWarning(
                "Codex execution was cancelled for task {TaskFile} in repository {Repository}; the process tree was terminated.",
                context, workingDirectory);
            throw;
        }
    }

    private string Bound(string value) => value.Length <= options.MaximumDiagnosticCharacters
        ? value : value[^options.MaximumDiagnosticCharacters..];

    private void ApplyUserProfileEnvironment(ProcessStartInfo startInfo)
    {
        var profile = string.IsNullOrWhiteSpace(options.UserProfileDirectory)
            ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            : Environment.ExpandEnvironmentVariables(options.UserProfileDirectory);
        if (string.IsNullOrWhiteSpace(profile))
        {
            throw new InvalidOperationException("The effective service user profile directory could not be resolved.");
        }

        profile = Path.GetFullPath(profile);
        var root = Path.GetPathRoot(profile) ?? string.Empty;
        startInfo.Environment["USERPROFILE"] = profile;
        startInfo.Environment["HOME"] = profile;
        startInfo.Environment["HOMEDRIVE"] = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        startInfo.Environment["HOMEPATH"] = profile[root.Length..];
        startInfo.Environment["APPDATA"] = Path.Combine(profile, "AppData", "Roaming");
        startInfo.Environment["LOCALAPPDATA"] = Path.Combine(profile, "AppData", "Local");
    }

    private string FormatEnvironment(ProcessStartInfo startInfo)
    {
        string[] names = ["USERPROFILE", "HOME", "HOMEDRIVE", "HOMEPATH", "APPDATA", "LOCALAPPDATA", "TEMP", "TMP", "PATH"];
        return Bound(string.Join("; ", names.Select(name =>
            $"{name}={(startInfo.Environment.TryGetValue(name, out var value) ? value : "<unset>")}")));
    }

    private static CodexExecutionFailureKind Classify(string stdout, string stderr)
    {
        var diagnostic = stdout + "\n" + stderr;
        return diagnostic.Contains("runner pipe", StringComparison.OrdinalIgnoreCase) &&
               (diagnostic.Contains("timeout", StringComparison.OrdinalIgnoreCase) || diagnostic.Contains("timed out", StringComparison.OrdinalIgnoreCase))
            ? CodexExecutionFailureKind.RunnerPipeTimeout
            : CodexExecutionFailureKind.None;
    }

    private void LogCompletion(string context, string workingDirectory, CodexTaskExecutionResult result)
    {
        logger.LogInformation(
            "Finished Codex for task {TaskFile} in repository {Repository}. Exit code: {ExitCode}; timed out: {TimedOut}; stdout: {StandardOutput}; stderr: {StandardError}",
            context, workingDirectory, result.ExitCode, result.TimedOut,
            result.StandardOutput, result.StandardError);
    }
    private static void Kill(Process process)
    {
        if (!process.HasExited) process.Kill(entireProcessTree: true);
    }
}

