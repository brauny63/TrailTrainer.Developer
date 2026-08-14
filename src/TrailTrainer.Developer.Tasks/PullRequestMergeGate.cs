using TrailTrainer.Developer.Core;

namespace TrailTrainer.Developer.Tasks;

public sealed class PullRequestMergeGate : IPullRequestMergeGate
{
    private readonly IPullRequestStatusGate statusGate;
    private readonly IPullRequestMerger merger;

    public PullRequestMergeGate(IPullRequestStatusGate statusGate, IPullRequestMerger merger)
    {
        this.statusGate = statusGate ?? throw new ArgumentNullException(nameof(statusGate));
        this.merger = merger ?? throw new ArgumentNullException(nameof(merger));
    }

    public async Task<PullRequestGatedMergeResult> MergeAsync(
        GitHubRepositoryIdentity repository,
        int pullRequestNumber,
        PullRequestMergeMethod method,
        string? commitTitle = null,
        string? commitMessage = null,
        CancellationToken cancellationToken = default)
    {
        var gateResult = await statusGate.EvaluateAsync(
            repository,
            pullRequestNumber,
            cancellationToken);

        switch (gateResult.State)
        {
            case PullRequestGateState.Pending:
                throw new InvalidOperationException(
                    $"Pull Request #{pullRequestNumber} cannot be merged because its CI/status checks are still pending.");
            case PullRequestGateState.Failed:
                throw new InvalidOperationException(
                    $"Pull Request #{pullRequestNumber} cannot be merged because its CI/status checks failed.");
            case PullRequestGateState.Successful:
                break;
            default:
                throw new InvalidOperationException("The Pull Request status gate returned an unsupported state.");
        }

        var mergeResult = await merger.MergeAsync(
            repository,
            pullRequestNumber,
            gateResult.HeadSha,
            method,
            commitTitle,
            commitMessage,
            cancellationToken);

        return new PullRequestGatedMergeResult(gateResult, mergeResult);
    }
}
