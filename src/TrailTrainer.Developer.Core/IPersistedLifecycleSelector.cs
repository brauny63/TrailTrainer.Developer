namespace TrailTrainer.Developer.Core;

public interface IPersistedLifecycleSelector
{
    Task<PersistedLifecycleSelectionResult> SelectAsync(
        PersistedLifecycleSelectionRequest request,
        CancellationToken cancellationToken = default);
}
