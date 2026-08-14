namespace TrailTrainer.Developer.Core;

public sealed record GitPushResult(
    string RepositoryRoot,
    string RemoteName,
    string BranchName,
    bool SetUpstream);
