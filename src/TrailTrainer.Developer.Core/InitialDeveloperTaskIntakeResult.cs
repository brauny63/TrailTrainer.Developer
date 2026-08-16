namespace TrailTrainer.Developer.Core;

public sealed record InitialDeveloperTaskIntakeResult
{
    public InitialDeveloperTaskIntakeResult(
        InitialDeveloperTaskIntakeState state,
        DeveloperTaskDescriptor? selectedTask = null,
        PersistedDeveloperLifecycleStartResult? startResult = null)
    {
        if (!Enum.IsDefined(state))
        {
            throw new ArgumentOutOfRangeException(nameof(state));
        }

        if (state == InitialDeveloperTaskIntakeState.Started)
        {
            ArgumentNullException.ThrowIfNull(selectedTask);
            ArgumentNullException.ThrowIfNull(startResult);
        }
        else if (selectedTask is not null || startResult is not null)
        {
            throw new ArgumentException("Only a Started intake may contain task and lifecycle results.");
        }

        State = state;
        SelectedTask = selectedTask;
        StartResult = startResult;
    }

    public InitialDeveloperTaskIntakeState State { get; }
    public DeveloperTaskDescriptor? SelectedTask { get; }
    public PersistedDeveloperLifecycleStartResult? StartResult { get; }
}
