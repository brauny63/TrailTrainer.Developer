namespace TrailTrainer.Developer.Core;

public interface IDeveloperLifecycleStateStore
{
    Task SaveAsync(
        DeveloperLifecyclePersistedState state,
        CancellationToken cancellationToken = default);

    Task<DeveloperLifecyclePersistedState?> LoadAsync(
        string taskId,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string taskId,
        CancellationToken cancellationToken = default);
}
