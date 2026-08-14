namespace TrailTrainer.Developer.Core;

public sealed record PersistedLifecycleResumeTarget
{
    public PersistedLifecycleResumeTarget(
        string taskId,
        DeveloperLifecyclePersistedState persistedState)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);
        ArgumentNullException.ThrowIfNull(persistedState);
        if (!string.Equals(taskId, persistedState.TaskId, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Resume target TaskId must exactly match the persisted state's TaskId.",
                nameof(taskId));
        }

        TaskId = taskId;
        PersistedState = persistedState;
    }

    public string TaskId { get; }
    public DeveloperLifecyclePersistedState PersistedState { get; }
}
