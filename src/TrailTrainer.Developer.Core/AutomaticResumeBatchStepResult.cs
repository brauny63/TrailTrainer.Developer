namespace TrailTrainer.Developer.Core;

public sealed record AutomaticResumeBatchStepResult
{
    public AutomaticResumeBatchStepResult(
        AutomaticResumeBatchStepState state,
        AutomaticPersistedLifecycleResumeResult resume,
        bool moreWork)
    {
        ArgumentNullException.ThrowIfNull(resume);
        switch (state)
        {
            case AutomaticResumeBatchStepState.Empty when
                resume.State != AutomaticPersistedLifecycleResumeState.NotFound || moreWork:
                throw new ArgumentException(
                    "An Empty batch step requires a NotFound resume result and no more work.");
            case AutomaticResumeBatchStepState.Pending when
                resume.State != AutomaticPersistedLifecycleResumeState.Pending:
                throw new ArgumentException("A Pending batch step requires a Pending resume result.");
            case AutomaticResumeBatchStepState.Failed when
                resume.State != AutomaticPersistedLifecycleResumeState.Failed:
                throw new ArgumentException("A Failed batch step requires a Failed resume result.");
            case AutomaticResumeBatchStepState.Completed when
                resume.State != AutomaticPersistedLifecycleResumeState.Completed:
                throw new ArgumentException("A Completed batch step requires a Completed resume result.");
            case AutomaticResumeBatchStepState.Empty:
            case AutomaticResumeBatchStepState.Pending:
            case AutomaticResumeBatchStepState.Failed:
            case AutomaticResumeBatchStepState.Completed:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(state));
        }

        State = state;
        Resume = resume;
        MoreWork = moreWork;
    }

    public AutomaticResumeBatchStepState State { get; }
    public AutomaticPersistedLifecycleResumeResult Resume { get; }
    public bool MoreWork { get; }
}
