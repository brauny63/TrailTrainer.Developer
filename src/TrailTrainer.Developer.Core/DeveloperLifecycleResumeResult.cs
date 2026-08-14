namespace TrailTrainer.Developer.Core;

public sealed record DeveloperLifecycleResumeResult
{
    public DeveloperLifecycleResumeResult(
        DeveloperLifecycleState state,
        DeveloperLifecycleResumeContext context,
        PullRequestStatusGateResult statusGate,
        PullRequestGatedMergeResult? gatedMerge = null,
        PostMergeCleanupResult? cleanup = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(statusGate);

        switch (state)
        {
            case DeveloperLifecycleState.Pending when
                statusGate.State != PullRequestGateState.Pending || gatedMerge is not null || cleanup is not null:
                throw new ArgumentException("A Pending resume result requires a Pending status gate and no merge or cleanup result.");
            case DeveloperLifecycleState.Failed when
                statusGate.State != PullRequestGateState.Failed || gatedMerge is not null || cleanup is not null:
                throw new ArgumentException("A Failed resume result requires a Failed status gate and no merge or cleanup result.");
            case DeveloperLifecycleState.Completed when
                statusGate.State != PullRequestGateState.Successful ||
                gatedMerge?.Merge is null ||
                !gatedMerge.Merge.Merged ||
                cleanup is null:
                throw new ArgumentException(
                    "A Completed resume result requires a Successful status gate, confirmed merge, and cleanup result.");
            case DeveloperLifecycleState.Pending:
            case DeveloperLifecycleState.Failed:
            case DeveloperLifecycleState.Completed:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(state));
        }

        State = state;
        Context = context;
        StatusGate = statusGate;
        GatedMerge = gatedMerge;
        Cleanup = cleanup;
    }

    public DeveloperLifecycleState State { get; }
    public DeveloperLifecycleResumeContext Context { get; }
    public PullRequestStatusGateResult StatusGate { get; }
    public PullRequestGatedMergeResult? GatedMerge { get; }
    public PostMergeCleanupResult? Cleanup { get; }
}
