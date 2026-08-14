namespace TrailTrainer.Developer.Core;

public sealed record DeveloperLifecycleResumeContext
{
    public DeveloperLifecycleResumeContext(
        string repositoryDirectory,
        GitHubRepositoryIdentity repository,
        int pullRequestNumber,
        string featureBranch,
        string baseBranch,
        string gitRemoteName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryDirectory);
        ArgumentNullException.ThrowIfNull(repository);
        if (pullRequestNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pullRequestNumber));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(featureBranch);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseBranch);
        ArgumentException.ThrowIfNullOrWhiteSpace(gitRemoteName);
        if (string.Equals(featureBranch, baseBranch, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The feature branch and base branch must differ.",
                nameof(featureBranch));
        }

        RepositoryDirectory = repositoryDirectory;
        Repository = repository;
        PullRequestNumber = pullRequestNumber;
        FeatureBranch = featureBranch;
        BaseBranch = baseBranch;
        GitRemoteName = gitRemoteName;
    }

    public string RepositoryDirectory { get; }
    public GitHubRepositoryIdentity Repository { get; }
    public int PullRequestNumber { get; }
    public string FeatureBranch { get; }
    public string BaseBranch { get; }
    public string GitRemoteName { get; }
}
