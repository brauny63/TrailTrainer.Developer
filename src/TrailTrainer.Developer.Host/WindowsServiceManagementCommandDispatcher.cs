namespace TrailTrainer.Developer.Host;

public sealed class WindowsServiceManagementCommandDispatcher
{
    public const int SuccessExitCode = 0;
    public const int OperationFailureExitCode = 1;
    public const int InvalidCommandExitCode = 2;

    private readonly IWindowsServiceManager serviceManager;

    public WindowsServiceManagementCommandDispatcher(IWindowsServiceManager serviceManager)
    {
        this.serviceManager = serviceManager ?? throw new ArgumentNullException(nameof(serviceManager));
    }

    public static bool HasCommand(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        return arguments.Count > 0;
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
                "Usage: TrailTrainer.Developer.Host install|uninstall|start|stop|status|recovery");
            return InvalidCommandExitCode;
        }

        try
        {
            switch (arguments[0].ToLowerInvariant())
            {
                case "install":
                    if (string.IsNullOrWhiteSpace(executablePath))
                    {
                        throw new InvalidOperationException("The current executable path is unavailable.");
                    }

                    await serviceManager.InstallAsync(executablePath, cancellationToken);
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

    private static bool IsKnownCommand(string command) =>
        command.Equals("install", StringComparison.OrdinalIgnoreCase) ||
        command.Equals("uninstall", StringComparison.OrdinalIgnoreCase) ||
        command.Equals("start", StringComparison.OrdinalIgnoreCase) ||
        command.Equals("stop", StringComparison.OrdinalIgnoreCase) ||
        command.Equals("status", StringComparison.OrdinalIgnoreCase) ||
        command.Equals("recovery", StringComparison.OrdinalIgnoreCase);
}
