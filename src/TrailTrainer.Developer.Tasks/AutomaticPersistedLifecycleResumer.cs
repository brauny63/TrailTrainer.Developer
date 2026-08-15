using TrailTrainer.Developer.Core;

namespace TrailTrainer.Developer.Tasks;

public sealed class AutomaticPersistedLifecycleResumer : IAutomaticPersistedLifecycleResumer
{
    private readonly IAutomaticResumeCandidateSelector candidateSelector;
    private readonly IPersistedDeveloperLifecycle persistedLifecycle;

    public AutomaticPersistedLifecycleResumer(
        IAutomaticResumeCandidateSelector candidateSelector,
        IPersistedDeveloperLifecycle persistedLifecycle)
    {
        this.candidateSelector = candidateSelector ?? throw new ArgumentNullException(nameof(candidateSelector));
        this.persistedLifecycle = persistedLifecycle ?? throw new ArgumentNullException(nameof(persistedLifecycle));
    }

    public async Task<AutomaticPersistedLifecycleResumeResult> ResumeAsync(
        AutomaticPersistedLifecycleResumeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var candidate = await candidateSelector.SelectAsync(cancellationToken);
        if (candidate.State == AutomaticResumeCandidateState.NotFound)
        {
            return new AutomaticPersistedLifecycleResumeResult(
                AutomaticPersistedLifecycleResumeState.NotFound,
                candidate);
        }

        if (candidate.State != AutomaticResumeCandidateState.Found || candidate.ResumeTarget is null)
        {
            throw new InvalidOperationException("The automatic resume candidate selector returned an inconsistent result.");
        }

        var resumeRequest = new PersistedDeveloperLifecycleResumeRequest(
            candidate.ResumeTarget.TaskId,
            request.MergeMethod,
            request.MergeCommitTitle,
            request.MergeCommitMessage,
            request.DeleteRemoteBranch);
        var resume = await persistedLifecycle.ResumeAsync(resumeRequest, cancellationToken);
        var state = resume.State switch
        {
            PersistedDeveloperLifecycleResumeState.Pending => AutomaticPersistedLifecycleResumeState.Pending,
            PersistedDeveloperLifecycleResumeState.Failed => AutomaticPersistedLifecycleResumeState.Failed,
            PersistedDeveloperLifecycleResumeState.Completed => AutomaticPersistedLifecycleResumeState.Completed,
            PersistedDeveloperLifecycleResumeState.NotFound => throw new InvalidOperationException(
                "The automatic resume candidate was not found when resume began."),
            _ => throw new InvalidOperationException("The persisted lifecycle resume returned an unsupported state.")
        };

        return new AutomaticPersistedLifecycleResumeResult(state, candidate, resume);
    }
}
