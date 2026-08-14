namespace TrailTrainer.Developer.Core;

public sealed record PersistedDeveloperLifecycleStartResult
{
    public PersistedDeveloperLifecycleStartResult(
        DeveloperLifecycleResult lifecycle,
        DeveloperLifecyclePersistedState? persistedState = null)
    {
        ArgumentNullException.ThrowIfNull(lifecycle);
        if (lifecycle.State == DeveloperLifecycleState.Pending && persistedState is null)
        {
            throw new ArgumentException("A Pending lifecycle requires persisted resume state.", nameof(persistedState));
        }

        if (lifecycle.State != DeveloperLifecycleState.Pending && persistedState is not null)
        {
            throw new ArgumentException("Only a Pending lifecycle may contain persisted resume state.", nameof(persistedState));
        }

        Lifecycle = lifecycle;
        PersistedState = persistedState;
    }

    public DeveloperLifecycleResult Lifecycle { get; }
    public DeveloperLifecyclePersistedState? PersistedState { get; }
}
