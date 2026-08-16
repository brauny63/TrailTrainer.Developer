namespace TrailTrainer.Developer.Host;

public enum OperationalHealthStatus
{
    Healthy,
    Degraded,
    Unhealthy
}

public sealed record OperationalHealthResult(OperationalHealthStatus Status, string Reason);

public interface IOperationalHealthDiagnostics
{
    Task<OperationalHealthResult> EvaluateAsync(CancellationToken cancellationToken = default);
}

public interface IProductionRuntimeHealthValidator
{
    Task ValidateAsync(CancellationToken cancellationToken = default);
}

public sealed class OperationalHealthDiagnostics : IOperationalHealthDiagnostics
{
    private readonly IWindowsServiceManager serviceManager;
    private readonly IProductionRuntimeHealthValidator runtimeValidator;

    public OperationalHealthDiagnostics(
        IWindowsServiceManager serviceManager,
        IProductionRuntimeHealthValidator runtimeValidator)
    {
        this.serviceManager = serviceManager ?? throw new ArgumentNullException(nameof(serviceManager));
        this.runtimeValidator = runtimeValidator ?? throw new ArgumentNullException(nameof(runtimeValidator));
    }

    public async Task<OperationalHealthResult> EvaluateAsync(
        CancellationToken cancellationToken = default)
    {
        WindowsServiceState serviceState;
        try
        {
            serviceState = await serviceManager.GetStatusAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return Unhealthy($"Service status failed: {exception.Message}");
        }

        if (serviceState is WindowsServiceState.NotInstalled)
        {
            return Unhealthy("Service is not installed.");
        }

        if (serviceState is WindowsServiceState.StartPending or
            WindowsServiceState.StopPending or
            WindowsServiceState.Unknown)
        {
            return Unhealthy($"Service state is {serviceState}.");
        }

        try
        {
            await runtimeValidator.ValidateAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return Unhealthy($"Production runtime validation failed: {exception.Message}");
        }

        return serviceState switch
        {
            WindowsServiceState.Running => new(
                OperationalHealthStatus.Healthy,
                "Service is running; production runtime dependencies resolve."),
            WindowsServiceState.Stopped => new(
                OperationalHealthStatus.Degraded,
                "Service is stopped; production runtime dependencies resolve."),
            WindowsServiceState.Paused => new(
                OperationalHealthStatus.Degraded,
                "Service is paused; production runtime dependencies resolve."),
            _ => Unhealthy($"Service state is {serviceState}.")
        };
    }

    private static OperationalHealthResult Unhealthy(string reason) =>
        new(OperationalHealthStatus.Unhealthy, reason);
}
