namespace TrailTrainer.Developer.Core;

public sealed record GitRepositoryStatus(
    bool IsRepository,
    string? RepositoryRoot,
    string? CurrentBranch,
    bool HasUncommittedChanges)
{
    public static GitRepositoryStatus NotRepository { get; } = new(
        IsRepository: false,
        RepositoryRoot: null,
        CurrentBranch: null,
        HasUncommittedChanges: false);
}
