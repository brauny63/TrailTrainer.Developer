using TrailTrainer.Developer.Core;

namespace TrailTrainer.Developer.Tasks;

public sealed class SelectedPersistedLifecycleResumer : ISelectedPersistedLifecycleResumer
{
    private readonly IPersistedLifecycleSelector selector;
    private readonly IPersistedDeveloperLifecycle persistedLifecycle;

    public SelectedPersistedLifecycleResumer(
        IPersistedLifecycleSelector selector,
        IPersistedDeveloperLifecycle persistedLifecycle)
    {
        this.selector = selector ?? throw new ArgumentNullException(nameof(selector));
        this.persistedLifecycle = persistedLifecycle ?? throw new ArgumentNullException(nameof(persistedLifecycle));
    }

    public async Task<SelectedPersistedLifecycleResumeResult> ResumeAsync(
        SelectedPersistedLifecycleResumeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var selection = await selector.SelectAsync(request.Selection, cancellationToken);
        if (selection.State == PersistedLifecycleSelectionState.NotFound)
        {
            return new SelectedPersistedLifecycleResumeResult(
                SelectedPersistedLifecycleResumeState.NotFound,
                selection);
        }

        if (selection.State != PersistedLifecycleSelectionState.Found || selection.PersistedState is null)
        {
            throw new InvalidOperationException("The persisted lifecycle selector returned an inconsistent result.");
        }

        var resumeRequest = new PersistedDeveloperLifecycleResumeRequest(
            selection.PersistedState.TaskId,
            request.MergeMethod,
            request.MergeCommitTitle,
            request.MergeCommitMessage,
            request.DeleteRemoteBranch);
        var resume = await persistedLifecycle.ResumeAsync(resumeRequest, cancellationToken);
        var state = resume.State switch
        {
            PersistedDeveloperLifecycleResumeState.Pending => SelectedPersistedLifecycleResumeState.Pending,
            PersistedDeveloperLifecycleResumeState.Failed => SelectedPersistedLifecycleResumeState.Failed,
            PersistedDeveloperLifecycleResumeState.Completed => SelectedPersistedLifecycleResumeState.Completed,
            PersistedDeveloperLifecycleResumeState.NotFound => throw new InvalidOperationException(
                "The selected persisted lifecycle state was not found when resume began."),
            _ => throw new InvalidOperationException("The persisted lifecycle resume returned an unsupported state.")
        };

        return new SelectedPersistedLifecycleResumeResult(state, selection, resume);
    }
}
