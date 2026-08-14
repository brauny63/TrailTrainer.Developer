namespace TrailTrainer.Developer.Core;

public sealed record GitHubRepositoryIdentity
{
    public GitHubRepositoryIdentity(string owner, string repository)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(repository);
        Owner = owner;
        Repository = repository;
    }

    public string Owner { get; }
    public string Repository { get; }
}
