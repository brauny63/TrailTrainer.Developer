using TrailTrainer.Developer.Core;

namespace TrailTrainer.Developer.Tasks;

public sealed class DeveloperLifecycleResumer : IDeveloperLifecycleResumer
{
    private readonly IPullRequestStatusGate statusGate;
    private readonly IPullRequestMergeGate mergeGate;
    private readonly IPostMergeCleaner postMergeCleaner;

    public DeveloperLifecycleResumer(
        IPullRequestStatusGate statusGate,
        IPullRequestMergeGate mergeGate,
        IPostMergeCleaner postMergeCleaner)
    {
        this.statusGate = statusGate ?? throw new ArgumentNullException(nameof(statusGate));
        this.mergeGate = mergeGate ?? throw new ArgumentNullException(nameof(mergeGate));
        this.postMergeCleaner = postMergeCleaner ?? throw new ArgumentNullException(nameof(postMergeCleaner));
    }

    public async Task<DeveloperLifecycleResumeResult> ResumeAsync(
        DeveloperLifecycleResumeContext context,
        PullRequestMergeMethod mergeMethod,
        string? mergeCommitTitle,
        string? mergeCommitMessage,
        bool deleteRemoteBranch,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var statusResult = await statusGate.EvaluateAsync(
            context.Repository,
            context.PullRequestNumber,
            cancellationToken);

        if (statusResult.State == PullRequestGateState.Pending)
        {
            return new DeveloperLifecycleResumeResult(
                DeveloperLifecycleState.Pending,
                context,
                statusResult);
        }

        if (statusResult.State == PullRequestGateState.Failed)
        {
            return new DeveloperLifecycleResumeResult(
                DeveloperLifecycleState.Failed,
                context,
                statusResult);
        }

        if (statusResult.State != PullRequestGateState.Successful)
        {
            throw new InvalidOperationException("The explicit Pull Request status gate returned an unsupported state.");
        }

        var gatedMergeResult = await mergeGate.MergeAsync(
            context.Repository,
            context.PullRequestNumber,
            mergeMethod,
            mergeCommitTitle,
            mergeCommitMessage,
            cancellationToken);
        if (gatedMergeResult?.Merge is null || !gatedMergeResult.Merge.Merged)
        {
            throw new InvalidOperationException(
                "The guarded merge did not return a confirmed successful merge result.");
        }

        var cleanupResult = await postMergeCleaner.CleanupAsync(
            context.RepositoryDirectory,
            context.Repository,
            context.PullRequestNumber,
            gatedMergeResult.Merge,
            context.FeatureBranch,
            context.BaseBranch,
            context.GitRemoteName,
            deleteRemoteBranch,
            cancellationToken);

        return new DeveloperLifecycleResumeResult(
            DeveloperLifecycleState.Completed,
            context,
            statusResult,
            gatedMergeResult,
            cleanupResult);
    }
}
