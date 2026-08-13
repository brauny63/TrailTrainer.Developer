namespace TrailTrainer.Developer.Core;

public sealed record GitCommitResult(
    string RepositoryRoot,
    string CommitSha,
    string CommitMessage);
