namespace TrailTrainer.Developer.Core;

public sealed record PersistedDeveloperLifecycleResumeResult
{
    public PersistedDeveloperLifecycleResumeResult(
        PersistedDeveloperLifecycleResumeState state,
        string taskId,
        DeveloperLifecyclePersistedState? persistedState = null,
        DeveloperLifecycleResumeResult? lifecycle = null,
        DeveloperLifecycleResult? recoveredWorkflow = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);
        switch (state)
        {
            case PersistedDeveloperLifecycleResumeState.NotFound when
                persistedState is not null || lifecycle is not null || recoveredWorkflow is not null:
                throw new ArgumentException("A NotFound result cannot contain persisted or lifecycle state.");
            case PersistedDeveloperLifecycleResumeState.Pending when
                persistedState is null || (lifecycle?.State != DeveloperLifecycleState.Pending && recoveredWorkflow?.State != DeveloperLifecycleState.Pending):
                throw new ArgumentException("A Pending result requires persisted state and a Pending lifecycle.");
            case PersistedDeveloperLifecycleResumeState.Failed when
                persistedState is null || (lifecycle?.State != DeveloperLifecycleState.Failed && recoveredWorkflow?.State != DeveloperLifecycleState.Failed):
                throw new ArgumentException("A Failed result requires persisted state and a Failed lifecycle.");
            case PersistedDeveloperLifecycleResumeState.Completed when
                persistedState is null || (lifecycle?.State != DeveloperLifecycleState.Completed && recoveredWorkflow?.State != DeveloperLifecycleState.Completed):
                throw new ArgumentException("A Completed result requires retained persisted state and a Completed lifecycle.");
            case PersistedDeveloperLifecycleResumeState.NotFound:
            case PersistedDeveloperLifecycleResumeState.Pending:
            case PersistedDeveloperLifecycleResumeState.Failed:
            case PersistedDeveloperLifecycleResumeState.Completed:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(state));
        }

        State = state;
        TaskId = taskId;
        PersistedState = persistedState;
        Lifecycle = lifecycle;
        RecoveredWorkflow = recoveredWorkflow;
    }

    public PersistedDeveloperLifecycleResumeState State { get; }
    public string TaskId { get; }
    public DeveloperLifecyclePersistedState? PersistedState { get; }
    public DeveloperLifecycleResumeResult? Lifecycle { get; }
    public DeveloperLifecycleResult? RecoveredWorkflow { get; }
}
