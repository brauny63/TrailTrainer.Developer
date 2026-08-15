using TrailTrainer.Developer.Core;

namespace TrailTrainer.Developer.Tasks;

public sealed class AutomaticResumeBatchStep : IAutomaticResumeBatchStep
{
    private readonly IAutomaticPersistedLifecycleResumer resumer;
    private readonly IDeveloperLifecycleStateDiscovery discovery;

    public AutomaticResumeBatchStep(
        IAutomaticPersistedLifecycleResumer resumer,
        IDeveloperLifecycleStateDiscovery discovery)
    {
        this.resumer = resumer ?? throw new ArgumentNullException(nameof(resumer));
        this.discovery = discovery ?? throw new ArgumentNullException(nameof(discovery));
    }

    public async Task<AutomaticResumeBatchStepResult> ExecuteAsync(
        AutomaticResumeBatchStepRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var resumeRequest = new AutomaticPersistedLifecycleResumeRequest(
            request.MergeMethod,
            request.MergeCommitTitle,
            request.MergeCommitMessage,
            request.DeleteRemoteBranch);
        var resume = await resumer.ResumeAsync(resumeRequest, cancellationToken);
        if (resume.State == AutomaticPersistedLifecycleResumeState.NotFound)
        {
            return new AutomaticResumeBatchStepResult(
                AutomaticResumeBatchStepState.Empty,
                resume,
                false);
        }

        var state = resume.State switch
        {
            AutomaticPersistedLifecycleResumeState.Pending => AutomaticResumeBatchStepState.Pending,
            AutomaticPersistedLifecycleResumeState.Failed => AutomaticResumeBatchStepState.Failed,
            AutomaticPersistedLifecycleResumeState.Completed => AutomaticResumeBatchStepState.Completed,
            _ => throw new InvalidOperationException("The automatic persisted lifecycle resume returned an unsupported state.")
        };
        var states = await discovery.ListAsync(cancellationToken)
            ?? throw new InvalidOperationException("Lifecycle state discovery returned a null collection.");

        return new AutomaticResumeBatchStepResult(state, resume, states.Count > 0);
    }
}
