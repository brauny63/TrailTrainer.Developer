namespace TrailTrainer.Developer.Core;

public sealed record PersistedLifecycleSelectionResult
{
    public PersistedLifecycleSelectionResult(
        PersistedLifecycleSelectionState state,
        DeveloperLifecyclePersistedState? persistedState = null)
    {
        if (!Enum.IsDefined(state))
        {
            throw new ArgumentOutOfRangeException(nameof(state));
        }

        if (state == PersistedLifecycleSelectionState.Found && persistedState is null)
        {
            throw new ArgumentException("A Found result requires persisted state.", nameof(persistedState));
        }

        if (state == PersistedLifecycleSelectionState.NotFound && persistedState is not null)
        {
            throw new ArgumentException("A NotFound result cannot contain persisted state.", nameof(persistedState));
        }

        State = state;
        PersistedState = persistedState;
    }

    public PersistedLifecycleSelectionState State { get; }
    public DeveloperLifecyclePersistedState? PersistedState { get; }
}
