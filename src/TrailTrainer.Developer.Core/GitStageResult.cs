namespace TrailTrainer.Developer.Core;

public sealed record GitStageResult(
    string RepositoryRoot,
    bool HasStagedChanges);
