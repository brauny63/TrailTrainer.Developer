using TrailTrainer.Developer.Core;
using TrailTrainer.Developer.GitHub;

namespace TrailTrainer.Developer.Host;

public sealed class GitHubRepositoryProbeCommand(IGitHubRepositoryProbe probe)
{
    public async Task<int> RunAsync(
        string owner,
        string repository,
        bool checkOpenPullRequests,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await probe.ProbeAsync(
                new GitHubRepositoryIdentity(owner, repository),
                checkOpenPullRequests,
                cancellationToken);
            await output.WriteLineAsync(
                $"GitHub probe succeeded for {owner}/{repository}" +
                (checkOpenPullRequests ? " including open Pull Request listing." : "."));
            return 0;
        }
        catch (Exception exception) when (exception is GitHubApiException or HttpRequestException or InvalidDataException)
        {
            await error.WriteLineAsync($"GitHub probe failed for {owner}/{repository}: {exception.Message}");
            return 1;
        }
    }
}
