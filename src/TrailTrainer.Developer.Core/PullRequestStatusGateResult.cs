namespace TrailTrainer.Developer.Core;

public sealed record PullRequestStatusGateResult
{
    public PullRequestStatusGateResult(
        int pullRequestNumber,
        string headSha,
        PullRequestGateState state,
        IEnumerable<PullRequestCheck> checks)
    {
        if (pullRequestNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pullRequestNumber));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(headSha);
        ArgumentNullException.ThrowIfNull(checks);
        PullRequestNumber = pullRequestNumber;
        HeadSha = headSha;
        State = state;
        Checks = Array.AsReadOnly(checks.ToArray());
    }

    public int PullRequestNumber { get; }
    public string HeadSha { get; }
    public PullRequestGateState State { get; }
    public IReadOnlyList<PullRequestCheck> Checks { get; }
}
