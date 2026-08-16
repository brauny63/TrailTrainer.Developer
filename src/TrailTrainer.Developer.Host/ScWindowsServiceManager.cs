using System.Globalization;
using System.Text.RegularExpressions;

namespace TrailTrainer.Developer.Host;

public sealed partial class ScWindowsServiceManager : IWindowsServiceManager
{
    private const string ScExecutable = "sc.exe";
    private const int ServiceDoesNotExistExitCode = 1060;
    private readonly IWindowsServiceProcessRunner processRunner;
    private readonly IWindowsPlatform platform;

    public ScWindowsServiceManager(
        IWindowsServiceProcessRunner processRunner,
        IWindowsPlatform platform)
    {
        this.processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
        this.platform = platform ?? throw new ArgumentNullException(nameof(platform));
    }

    public async Task InstallAsync(
        string executablePath,
        CancellationToken cancellationToken = default)
    {
        EnsureWindows();
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        var fullPath = Path.GetFullPath(executablePath);
        if (await GetStatusAsync(cancellationToken) != WindowsServiceState.NotInstalled)
        {
            throw new InvalidOperationException(
                $"Windows service '{AutomaticResumeWindowsServiceExtensions.ServiceName}' already exists.");
        }

        await RunRequiredAsync(
            "install",
            [
                "create",
                AutomaticResumeWindowsServiceExtensions.ServiceName,
                "binPath=",
                QuoteServiceExecutablePath(fullPath),
                "start=",
                "auto",
                "DisplayName=",
                AutomaticResumeWindowsServiceExtensions.ServiceName
            ],
            cancellationToken);
    }

    public async Task UninstallAsync(CancellationToken cancellationToken = default)
    {
        EnsureWindows();
        if (await GetStatusAsync(cancellationToken) == WindowsServiceState.NotInstalled)
        {
            return;
        }

        await RunRequiredAsync(
            "uninstall",
            ["delete", AutomaticResumeWindowsServiceExtensions.ServiceName],
            cancellationToken);
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        EnsureWindows();
        return RunRequiredAsync(
            "start",
            ["start", AutomaticResumeWindowsServiceExtensions.ServiceName],
            cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        EnsureWindows();
        return RunRequiredAsync(
            "stop",
            ["stop", AutomaticResumeWindowsServiceExtensions.ServiceName],
            cancellationToken);
    }

    public async Task ConfigureRecoveryAsync(CancellationToken cancellationToken = default)
    {
        EnsureWindows();
        if (await GetStatusAsync(cancellationToken) == WindowsServiceState.NotInstalled)
        {
            throw new InvalidOperationException(
                $"Windows service '{AutomaticResumeWindowsServiceExtensions.ServiceName}' is not installed.");
        }

        await RunRequiredAsync(
            "recovery policy configuration",
            [
                "failure",
                AutomaticResumeWindowsServiceExtensions.ServiceName,
                "reset=",
                "86400",
                "actions=",
                "restart/60000/restart/60000/restart/60000"
            ],
            cancellationToken);
        await RunRequiredAsync(
            "non-crash recovery configuration",
            ["failureflag", AutomaticResumeWindowsServiceExtensions.ServiceName, "1"],
            cancellationToken);
    }

    public async Task ConfigureDelayedStartAsync(CancellationToken cancellationToken = default)
    {
        EnsureWindows();
        if (await GetStatusAsync(cancellationToken) == WindowsServiceState.NotInstalled)
        {
            throw new InvalidOperationException(
                $"Windows service '{AutomaticResumeWindowsServiceExtensions.ServiceName}' is not installed.");
        }

        await RunRequiredAsync(
            "delayed automatic start configuration",
            [
                "config",
                AutomaticResumeWindowsServiceExtensions.ServiceName,
                "start=",
                "delayed-auto"
            ],
            cancellationToken);
    }

    public async Task<WindowsServiceState> GetStatusAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureWindows();
        var result = await processRunner.RunAsync(
            ScExecutable,
            ["query", AutomaticResumeWindowsServiceExtensions.ServiceName],
            cancellationToken);
        if (result.ExitCode == ServiceDoesNotExistExitCode)
        {
            return WindowsServiceState.NotInstalled;
        }

        EnsureSuccess(result, "query");
        var state = ServiceStatePattern().Matches(result.StandardOutput)
            .Select(match => int.TryParse(
                match.Groups[1].Value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var value)
                ? value
                : 0)
            .FirstOrDefault(value => value is >= 1 and <= 7);
        if (state == 0)
        {
            return WindowsServiceState.Unknown;
        }

        return state switch
        {
            1 => WindowsServiceState.Stopped,
            2 => WindowsServiceState.StartPending,
            3 => WindowsServiceState.StopPending,
            4 => WindowsServiceState.Running,
            7 => WindowsServiceState.Paused,
            _ => WindowsServiceState.Unknown
        };
    }

    private async Task RunRequiredAsync(
        string operation,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var result = await processRunner.RunAsync(ScExecutable, arguments, cancellationToken);
        EnsureSuccess(result, operation);
    }

    private void EnsureWindows()
    {
        if (!platform.IsWindows)
        {
            throw new PlatformNotSupportedException(
                "Windows service management commands are supported only on Windows.");
        }
    }

    private static void EnsureSuccess(WindowsServiceProcessResult result, string operation)
    {
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Windows service {operation} failed with exit code {result.ExitCode}. " +
                $"Diagnostic: {Diagnostic(result)}");
        }
    }

    private static string Diagnostic(WindowsServiceProcessResult result)
    {
        var diagnostic = string.IsNullOrWhiteSpace(result.StandardError)
            ? result.StandardOutput
            : result.StandardError;
        return string.IsNullOrWhiteSpace(diagnostic) ? "No output was returned." : diagnostic.Trim();
    }

    private static string QuoteServiceExecutablePath(string executablePath) =>
        $"\"{executablePath}\"";

    [GeneratedRegex(@"(?m)^\s*[^:\r\n]+:\s*(\d+)\s+\S+", RegexOptions.CultureInvariant)]
    private static partial Regex ServiceStatePattern();
}
