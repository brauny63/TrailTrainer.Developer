namespace TrailTrainer.Developer.Core;

public sealed record PostMergeCleanupResult(
    string RepositoryRoot,
    string BaseBranch,
    string FeatureBranch,
    bool LocalBranchDeleted,
    bool RemoteBranchDeleted);
