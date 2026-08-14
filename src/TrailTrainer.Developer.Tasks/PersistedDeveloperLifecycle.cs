using TrailTrainer.Developer.Core;

namespace TrailTrainer.Developer.Tasks;

public sealed class PersistedDeveloperLifecycle : IPersistedDeveloperLifecycle
{
    private readonly IDeveloperLifecycleOrchestrator lifecycleOrchestrator;
    private readonly IDeveloperLifecycleResumer lifecycleResumer;
    private readonly IDeveloperLifecycleStateStore stateStore;
    private readonly IUtcClock clock;

    public PersistedDeveloperLifecycle(
        IDeveloperLifecycleOrchestrator lifecycleOrchestrator,
        IDeveloperLifecycleResumer lifecycleResumer,
        IDeveloperLifecycleStateStore stateStore,
        IUtcClock clock)
    {
        this.lifecycleOrchestrator = lifecycleOrchestrator ?? throw new ArgumentNullException(nameof(lifecycleOrchestrator));
        this.lifecycleResumer = lifecycleResumer ?? throw new ArgumentNullException(nameof(lifecycleResumer));
        this.stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<PersistedDeveloperLifecycleStartResult> StartAsync(
        PersistedDeveloperLifecycleStartRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var lifecycle = await lifecycleOrchestrator.ExecuteAsync(
            request.DeveloperTaskFilePath,
            request.RepositoryDirectoryPath,
            request.ExpectedRepositoryName,
            request.CommitMessage,
            request.GitRemoteName,
            request.SetUpstream,
            request.GitHubRepository,
            request.PullRequestBaseBranch,
            request.PullRequestBody,
            request.PullRequestDraft,
            request.MergeMethod,
            request.MergeCommitTitle,
            request.MergeCommitMessage,
            request.DeleteRemoteBranch,
            cancellationToken);

        if (lifecycle.State != DeveloperLifecycleState.Pending)
        {
            return new PersistedDeveloperLifecycleStartResult(lifecycle);
        }

        var context = new DeveloperLifecycleResumeContext(
            request.RepositoryDirectoryPath,
            request.GitHubRepository,
            lifecycle.Workflow.PullRequest.PullRequest.Number,
            lifecycle.Workflow.Completion.Completion.BranchName,
            request.PullRequestBaseBranch,
            request.GitRemoteName);
        var persistedState = new DeveloperLifecyclePersistedState(
            request.TaskId,
            request.TaskFilePath,
            context,
            clock.UtcNow);
        await stateStore.SaveAsync(persistedState, cancellationToken);
        return new PersistedDeveloperLifecycleStartResult(lifecycle, persistedState);
    }

    public async Task<PersistedDeveloperLifecycleResumeResult> ResumeAsync(
        PersistedDeveloperLifecycleResumeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var persistedState = await stateStore.LoadAsync(request.TaskId, cancellationToken);
        if (persistedState is null)
        {
            return new PersistedDeveloperLifecycleResumeResult(
                PersistedDeveloperLifecycleResumeState.NotFound,
                request.TaskId);
        }

        if (!string.Equals(persistedState.TaskId, request.TaskId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The loaded lifecycle state's TaskId does not match the requested TaskId.");
        }

        var lifecycle = await lifecycleResumer.ResumeAsync(
            persistedState.ResumeContext,
            request.MergeMethod,
            request.MergeCommitTitle,
            request.MergeCommitMessage,
            request.DeleteRemoteBranch,
            cancellationToken);

        var state = lifecycle.State switch
        {
            DeveloperLifecycleState.Pending => PersistedDeveloperLifecycleResumeState.Pending,
            DeveloperLifecycleState.Failed => PersistedDeveloperLifecycleResumeState.Failed,
            DeveloperLifecycleState.Completed => PersistedDeveloperLifecycleResumeState.Completed,
            _ => throw new InvalidOperationException("The resumed lifecycle returned an unsupported state.")
        };

        if (state == PersistedDeveloperLifecycleResumeState.Completed)
        {
            await stateStore.DeleteAsync(request.TaskId, cancellationToken);
        }

        return new PersistedDeveloperLifecycleResumeResult(
            state,
            request.TaskId,
            persistedState,
            lifecycle);
    }
}
