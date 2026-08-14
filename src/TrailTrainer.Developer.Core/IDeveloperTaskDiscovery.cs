namespace TrailTrainer.Developer.Core;

public interface IDeveloperTaskDiscovery
{
    Task<IReadOnlyList<DeveloperTaskDescriptor>> DiscoverAsync(
        string repositoryRootPath,
        CancellationToken cancellationToken = default);
}
