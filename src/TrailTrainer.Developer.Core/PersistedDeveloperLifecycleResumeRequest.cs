namespace TrailTrainer.Developer.Core;

public sealed record PersistedDeveloperLifecycleResumeRequest
{
    public PersistedDeveloperLifecycleResumeRequest(
        string taskId,
        PullRequestMergeMethod mergeMethod,
        string? mergeCommitTitle,
        string? mergeCommitMessage,
        bool deleteRemoteBranch)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);
        TaskId = taskId;
        MergeMethod = mergeMethod;
        MergeCommitTitle = mergeCommitTitle;
        MergeCommitMessage = mergeCommitMessage;
        DeleteRemoteBranch = deleteRemoteBranch;
    }

    public string TaskId { get; }
    public PullRequestMergeMethod MergeMethod { get; }
    public string? MergeCommitTitle { get; }
    public string? MergeCommitMessage { get; }
    public bool DeleteRemoteBranch { get; }
}
