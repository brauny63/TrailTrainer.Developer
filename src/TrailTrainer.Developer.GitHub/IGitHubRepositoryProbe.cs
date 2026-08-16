using TrailTrainer.Developer.Core;

namespace TrailTrainer.Developer.GitHub;

public interface IGitHubRepositoryProbe
{
    Task ProbeAsync(
        GitHubRepositoryIdentity repository,
        bool checkOpenPullRequests = false,
        CancellationToken cancellationToken = default);
}
