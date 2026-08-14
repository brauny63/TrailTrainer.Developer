namespace TrailTrainer.Developer.Core;

public interface IDeveloperLifecycleResumer
{
    Task<DeveloperLifecycleResumeResult> ResumeAsync(
        DeveloperLifecycleResumeContext context,
        PullRequestMergeMethod mergeMethod,
        string? mergeCommitTitle,
        string? mergeCommitMessage,
        bool deleteRemoteBranch,
        CancellationToken cancellationToken = default);
}
