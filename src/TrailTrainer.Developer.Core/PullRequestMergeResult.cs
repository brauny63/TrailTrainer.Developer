namespace TrailTrainer.Developer.Core;

public sealed record PullRequestMergeResult
{
    public PullRequestMergeResult(
        int pullRequestNumber,
        bool merged,
        string? mergeCommitSha,
        PullRequestMergeMethod method)
    {
        if (pullRequestNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pullRequestNumber));
        }

        if (!Enum.IsDefined(method))
        {
            throw new ArgumentOutOfRangeException(nameof(method));
        }

        if (merged && string.IsNullOrWhiteSpace(mergeCommitSha))
        {
            throw new ArgumentException(
                "A successful merge must include a merge commit SHA.",
                nameof(mergeCommitSha));
        }

        PullRequestNumber = pullRequestNumber;
        Merged = merged;
        MergeCommitSha = mergeCommitSha;
        Method = method;
    }

    public int PullRequestNumber { get; }
    public bool Merged { get; }
    public string? MergeCommitSha { get; }
    public PullRequestMergeMethod Method { get; }
}
