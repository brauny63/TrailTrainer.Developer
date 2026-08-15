namespace TrailTrainer.Developer.Core;

public sealed record AutomaticPersistedLifecycleResumeResult
{
    public AutomaticPersistedLifecycleResumeResult(
        AutomaticPersistedLifecycleResumeState state,
        AutomaticResumeCandidateResult candidate,
        PersistedDeveloperLifecycleResumeResult? resume = null)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        switch (state)
        {
            case AutomaticPersistedLifecycleResumeState.NotFound when
                candidate.State != AutomaticResumeCandidateState.NotFound || resume is not null:
                throw new ArgumentException(
                    "A NotFound result requires a NotFound candidate and no resume result.");
            case AutomaticPersistedLifecycleResumeState.Pending when
                candidate.State != AutomaticResumeCandidateState.Found ||
                resume?.State != PersistedDeveloperLifecycleResumeState.Pending:
                throw new ArgumentException(
                    "A Pending result requires a Found candidate and Pending resume result.");
            case AutomaticPersistedLifecycleResumeState.Failed when
                candidate.State != AutomaticResumeCandidateState.Found ||
                resume?.State != PersistedDeveloperLifecycleResumeState.Failed:
                throw new ArgumentException(
                    "A Failed result requires a Found candidate and Failed resume result.");
            case AutomaticPersistedLifecycleResumeState.Completed when
                candidate.State != AutomaticResumeCandidateState.Found ||
                resume?.State != PersistedDeveloperLifecycleResumeState.Completed:
                throw new ArgumentException(
                    "A Completed result requires a Found candidate and Completed resume result.");
            case AutomaticPersistedLifecycleResumeState.NotFound:
            case AutomaticPersistedLifecycleResumeState.Pending:
            case AutomaticPersistedLifecycleResumeState.Failed:
            case AutomaticPersistedLifecycleResumeState.Completed:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(state));
        }

        State = state;
        Candidate = candidate;
        Resume = resume;
    }

    public AutomaticPersistedLifecycleResumeState State { get; }
    public AutomaticResumeCandidateResult Candidate { get; }
    public PersistedDeveloperLifecycleResumeResult? Resume { get; }
}
