namespace TrailTrainer.Developer.Host;

public interface IWindowsServiceManager
{
    Task InstallAsync(string executablePath, CancellationToken cancellationToken = default);

    Task UninstallAsync(CancellationToken cancellationToken = default);

    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);

    Task ConfigureRecoveryAsync(CancellationToken cancellationToken = default);

    Task ConfigureDelayedStartAsync(CancellationToken cancellationToken = default);

    Task<WindowsServiceState> GetStatusAsync(CancellationToken cancellationToken = default);
}
