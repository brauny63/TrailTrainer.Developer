namespace TrailTrainer.Developer.Core;

public sealed record AutomaticResumeCandidateResult
{
    public AutomaticResumeCandidateResult(
        AutomaticResumeCandidateState state,
        DeveloperLifecyclePersistedState? persistedState = null,
        PersistedLifecycleResumeTarget? resumeTarget = null)
    {
        if (!Enum.IsDefined(state))
        {
            throw new ArgumentOutOfRangeException(nameof(state));
        }

        if (state == AutomaticResumeCandidateState.NotFound)
        {
            if (persistedState is not null || resumeTarget is not null)
            {
                throw new ArgumentException(
                    "A NotFound candidate cannot contain persisted state or a resume target.");
            }
        }
        else
        {
            if (persistedState is null || resumeTarget is null)
            {
                throw new ArgumentException(
                    "A Found candidate requires persisted state and a resume target.");
            }

            if (!ReferenceEquals(persistedState, resumeTarget.PersistedState))
            {
                throw new ArgumentException(
                    "The resume target must contain the exact selected persisted state object.",
                    nameof(resumeTarget));
            }

            if (!string.Equals(resumeTarget.TaskId, persistedState.TaskId, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "The resume target TaskId must exactly match the selected persisted state.",
                    nameof(resumeTarget));
            }
        }

        State = state;
        PersistedState = persistedState;
        ResumeTarget = resumeTarget;
    }

    public AutomaticResumeCandidateState State { get; }
    public DeveloperLifecyclePersistedState? PersistedState { get; }
    public PersistedLifecycleResumeTarget? ResumeTarget { get; }
}
