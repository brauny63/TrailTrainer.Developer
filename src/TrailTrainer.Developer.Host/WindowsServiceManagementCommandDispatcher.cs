namespace TrailTrainer.Developer.Host;

public sealed class WindowsServiceManagementCommandDispatcher
{
    public const int SuccessExitCode = 0;
    public const int OperationFailureExitCode = 1;
    public const int InvalidCommandExitCode = 2;

    private readonly IWindowsServiceManager serviceManager;
    private readonly Func<IOperationalHealthDiagnostics>? healthDiagnosticsFactory;

    public WindowsServiceManagementCommandDispatcher(
        IWindowsServiceManager serviceManager,
        Func<IOperationalHealthDiagnostics>? healthDiagnosticsFactory = null)
    {
        this.serviceManager = serviceManager ?? throw new ArgumentNullException(nameof(serviceManager));
        this.healthDiagnosticsFactory = healthDiagnosticsFactory;
    }

    public static bool HasCommand(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        return arguments.Count > 0;
    }

    public static bool IsHealthCommand(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        return arguments.Count == 1 &&
            arguments[0].Equals("health", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<int> RunAsync(
        IReadOnlyList<string> arguments,
        string? executablePath,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);
        if (arguments.Count != 1 || !IsKnownCommand(arguments[0]))
        {
            await error.WriteLineAsync(
                "Usage: TrailTrainer.Developer.Host install|uninstall|start|stop|status|recovery|delayed-start|setup|provision|deprovision|restart|health");
            return InvalidCommandExitCode;
        }

        try
        {
            switch (arguments[0].ToLowerInvariant())
            {
                case "install":
                    await serviceManager.InstallAsync(
                        RequireExecutablePath(executablePath),
                        cancellationToken);
                    await output.WriteLineAsync(
                        $"Windows service '{AutomaticResumeWindowsServiceExtensions.ServiceName}' installed.");
                    break;
                case "uninstall":
                    await serviceManager.UninstallAsync(cancellationToken);
                    await output.WriteLineAsync(
                        $"Windows service '{AutomaticResumeWindowsServiceExtensions.ServiceName}' is not installed.");
                    break;
                case "start":
                    await serviceManager.StartAsync(cancellationToken);
                    await output.WriteLineAsync(
                        $"Windows service '{AutomaticResumeWindowsServiceExtensions.ServiceName}' started.");
                    break;
                case "stop":
                    await serviceManager.StopAsync(cancellationToken);
                    await output.WriteLineAsync(
                        $"Windows service '{AutomaticResumeWindowsServiceExtensions.ServiceName}' stopped.");
                    break;
                case "status":
                    var state = await serviceManager.GetStatusAsync(cancellationToken);
                    await output.WriteLineAsync(state.ToString());
                    break;
                case "recovery":
                    await serviceManager.ConfigureRecoveryAsync(cancellationToken);
                    await output.WriteLineAsync(
                        $"Windows service '{AutomaticResumeWindowsServiceExtensions.ServiceName}' recovery policy configured.");
                    break;
                case "delayed-start":
                    await serviceManager.ConfigureDelayedStartAsync(cancellationToken);
                    await output.WriteLineAsync(
                        $"Windows service '{AutomaticResumeWindowsServiceExtensions.ServiceName}' delayed automatic start configured.");
                    break;
                case "setup":
                    await ConfigureOperationalSetupAsync(cancellationToken);
                    await output.WriteLineAsync(
                        $"Windows service '{AutomaticResumeWindowsServiceExtensions.ServiceName}' operational setup configured.");
                    break;
                case "provision":
                    if (await serviceManager.GetStatusAsync(cancellationToken) != WindowsServiceState.NotInstalled)
                    {
                        throw new InvalidOperationException(
                            $"Windows service '{AutomaticResumeWindowsServiceExtensions.ServiceName}' already exists.");
                    }

                    await serviceManager.InstallAsync(
                        RequireExecutablePath(executablePath),
                        cancellationToken);
                    await ConfigureOperationalSetupAsync(cancellationToken);
                    await output.WriteLineAsync(
                        $"Windows service '{AutomaticResumeWindowsServiceExtensions.ServiceName}' provisioned and stopped.");
                    break;
                case "deprovision":
                    await DeprovisionAsync(cancellationToken);
                    await output.WriteLineAsync(
                        $"Windows service '{AutomaticResumeWindowsServiceExtensions.ServiceName}' is not installed.");
                    break;
                case "restart":
                    await RestartAsync(cancellationToken);
                    await output.WriteLineAsync(
                        $"Windows service '{AutomaticResumeWindowsServiceExtensions.ServiceName}' restarted.");
                    break;
                case "health":
                    var diagnostics = healthDiagnosticsFactory?.Invoke()
                        ?? throw new InvalidOperationException("Operational health diagnostics are unavailable.");
                    var health = await diagnostics.EvaluateAsync(cancellationToken);
                    await output.WriteLineAsync($"{health.Status}: {health.Reason}");
                    return health.Status == OperationalHealthStatus.Healthy
                        ? SuccessExitCode
                        : OperationFailureExitCode;
            }

            return SuccessExitCode;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await error.WriteLineAsync(exception.Message);
            return OperationFailureExitCode;
        }
    }

    private async Task ConfigureOperationalSetupAsync(CancellationToken cancellationToken)
    {
        await serviceManager.ConfigureDelayedStartAsync(cancellationToken);
        await serviceManager.ConfigureRecoveryAsync(cancellationToken);
    }

    private async Task DeprovisionAsync(CancellationToken cancellationToken)
    {
        var state = await serviceManager.GetStatusAsync(cancellationToken);
        switch (state)
        {
            case WindowsServiceState.NotInstalled:
                return;
            case WindowsServiceState.Stopped:
                break;
            case WindowsServiceState.Running:
            case WindowsServiceState.Paused:
                await serviceManager.StopAsync(cancellationToken);
                break;
            case WindowsServiceState.StartPending:
            case WindowsServiceState.StopPending:
            case WindowsServiceState.Unknown:
                throw new InvalidOperationException(
                    $"Windows service '{AutomaticResumeWindowsServiceExtensions.ServiceName}' cannot be safely " +
                    $"deprovisioned from state '{state}' without waiting or polling.");
            default:
                throw new InvalidOperationException(
                    $"Windows service '{AutomaticResumeWindowsServiceExtensions.ServiceName}' returned unsupported state '{state}'.");
        }

        await serviceManager.UninstallAsync(cancellationToken);
    }

    private async Task RestartAsync(CancellationToken cancellationToken)
    {
        var state = await serviceManager.GetStatusAsync(cancellationToken);
        switch (state)
        {
            case WindowsServiceState.Running:
                await serviceManager.StopAsync(cancellationToken);
                await serviceManager.StartAsync(cancellationToken);
                return;
            case WindowsServiceState.Stopped:
                await serviceManager.StartAsync(cancellationToken);
                return;
            case WindowsServiceState.NotInstalled:
                throw new InvalidOperationException(
                    $"Windows service '{AutomaticResumeWindowsServiceExtensions.ServiceName}' is not installed.");
            case WindowsServiceState.StartPending:
            case WindowsServiceState.StopPending:
            case WindowsServiceState.Paused:
            case WindowsServiceState.Unknown:
                throw new InvalidOperationException(
                    $"Windows service '{AutomaticResumeWindowsServiceExtensions.ServiceName}' cannot be safely " +
                    $"restarted from state '{state}' without waiting or polling.");
            default:
                throw new InvalidOperationException(
                    $"Windows service '{AutomaticResumeWindowsServiceExtensions.ServiceName}' returned unsupported state '{state}'.");
        }
    }

    private static string RequireExecutablePath(string? executablePath) =>
        string.IsNullOrWhiteSpace(executablePath)
            ? throw new InvalidOperationException("The current executable path is unavailable.")
            : executablePath;

    private static bool IsKnownCommand(string command) =>
        command.Equals("install", StringComparison.OrdinalIgnoreCase) ||
        command.Equals("uninstall", StringComparison.OrdinalIgnoreCase) ||
        command.Equals("start", StringComparison.OrdinalIgnoreCase) ||
        command.Equals("stop", StringComparison.OrdinalIgnoreCase) ||
        command.Equals("status", StringComparison.OrdinalIgnoreCase) ||
        command.Equals("recovery", StringComparison.OrdinalIgnoreCase) ||
        command.Equals("delayed-start", StringComparison.OrdinalIgnoreCase) ||
        command.Equals("setup", StringComparison.OrdinalIgnoreCase) ||
        command.Equals("provision", StringComparison.OrdinalIgnoreCase) ||
        command.Equals("deprovision", StringComparison.OrdinalIgnoreCase) ||
        command.Equals("restart", StringComparison.OrdinalIgnoreCase) ||
        command.Equals("health", StringComparison.OrdinalIgnoreCase);
}
