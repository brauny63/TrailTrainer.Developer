namespace TrailTrainer.Developer.Core;

public sealed record PersistedLifecycleSelectionRequest
{
    public PersistedLifecycleSelectionRequest(
        PersistedLifecycleSelectionMode mode,
        string? taskId = null)
    {
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }

        if (mode == PersistedLifecycleSelectionMode.ExactTaskId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(taskId);
        }
        else if (taskId is not null)
        {
            throw new ArgumentException(
                "TaskId must be null for Oldest and Newest selection.",
                nameof(taskId));
        }

        Mode = mode;
        TaskId = taskId;
    }

    public PersistedLifecycleSelectionMode Mode { get; }
    public string? TaskId { get; }
}
