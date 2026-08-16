using System.Diagnostics;

namespace TrailTrainer.Developer.Host;

public sealed record WindowsServiceProcessResult(int ExitCode, string StandardOutput, string StandardError);

public interface IWindowsServiceProcessRunner
{
    Task<WindowsServiceProcessResult> RunAsync(
        string executable,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default);
}

public sealed class WindowsServiceProcessRunner : IWindowsServiceProcessRunner
{
    public async Task<WindowsServiceProcessResult> RunAsync(
        string executable,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executable);
        ArgumentNullException.ThrowIfNull(arguments);

        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start '{executable}'.");
        var standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardError = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return new WindowsServiceProcessResult(
            process.ExitCode,
            await standardOutput,
            await standardError);
    }
}
