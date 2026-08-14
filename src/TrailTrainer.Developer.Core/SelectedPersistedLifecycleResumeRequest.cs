namespace TrailTrainer.Developer.Core;

public sealed record SelectedPersistedLifecycleResumeRequest
{
    public SelectedPersistedLifecycleResumeRequest(
        PersistedLifecycleSelectionRequest selection,
        PullRequestMergeMethod mergeMethod,
        string? mergeCommitTitle,
        string? mergeCommitMessage,
        bool deleteRemoteBranch)
    {
        ArgumentNullException.ThrowIfNull(selection);
        Selection = selection;
        MergeMethod = mergeMethod;
        MergeCommitTitle = mergeCommitTitle;
        MergeCommitMessage = mergeCommitMessage;
        DeleteRemoteBranch = deleteRemoteBranch;
    }

    public PersistedLifecycleSelectionRequest Selection { get; }
    public PullRequestMergeMethod MergeMethod { get; }
    public string? MergeCommitTitle { get; }
    public string? MergeCommitMessage { get; }
    public bool DeleteRemoteBranch { get; }
}
