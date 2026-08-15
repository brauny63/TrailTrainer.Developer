namespace TrailTrainer.Developer.Core;

public sealed record AutomaticPersistedLifecycleResumeRequest
{
    public AutomaticPersistedLifecycleResumeRequest(
        PullRequestMergeMethod mergeMethod,
        string? mergeCommitTitle,
        string? mergeCommitMessage,
        bool deleteRemoteBranch)
    {
        MergeMethod = mergeMethod;
        MergeCommitTitle = mergeCommitTitle;
        MergeCommitMessage = mergeCommitMessage;
        DeleteRemoteBranch = deleteRemoteBranch;
    }

    public PullRequestMergeMethod MergeMethod { get; }
    public string? MergeCommitTitle { get; }
    public string? MergeCommitMessage { get; }
    public bool DeleteRemoteBranch { get; }
}
