namespace TrailTrainer.Developer.Core;

public interface IPullRequestStatusGate
{
    Task<PullRequestStatusGateResult> EvaluateAsync(
        GitHubRepositoryIdentity repository,
        int pullRequestNumber,
        CancellationToken cancellationToken = default);
}
