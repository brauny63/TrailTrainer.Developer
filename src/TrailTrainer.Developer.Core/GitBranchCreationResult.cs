namespace TrailTrainer.Developer.Core;

public sealed record GitBranchCreationResult(
    string RepositoryRoot,
    string BranchName);
