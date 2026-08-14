namespace TrailTrainer.Developer.Core;

public interface IDeveloperLifecycleStateDiscovery
{
    Task<IReadOnlyList<DeveloperLifecyclePersistedState>> ListAsync(
        CancellationToken cancellationToken = default);
}
