using TrailTrainer.Developer.Git;

namespace TrailTrainer.Developer.Tests;

public sealed class LocalGitRepositoryStatusProviderTests
{
    private readonly LocalGitRepositoryStatusProvider provider = new();

    [Fact]
    public async Task GetStatusAsync_InitializedRepository_ReturnsRootAndCleanStatus()
    {
        using var repository = TemporaryGitRepository.Create();
        var nestedDirectory = System.IO.Path.Combine(repository.Path, "nested directory");
        Directory.CreateDirectory(nestedDirectory);

        var status = await provider.GetStatusAsync(nestedDirectory);

        Assert.True(status.IsRepository);
        Assert.Equal(
            System.IO.Path.GetFullPath(repository.Path),
            status.RepositoryRoot,
            OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        Assert.False(status.HasUncommittedChanges);
    }

    [Fact]
    public async Task GetStatusAsync_KnownBranch_ReturnsCurrentBranch()
    {
        using var repository = TemporaryGitRepository.Create();
        repository.RunGit("checkout", "-b", "known-branch");

        var status = await provider.GetStatusAsync(repository.Path);

        Assert.Equal("known-branch", status.CurrentBranch);
    }

    [Fact]
    public async Task GetStatusAsync_UntrackedFile_ReportsUncommittedChanges()
    {
        using var repository = TemporaryGitRepository.Create();
        File.WriteAllText(System.IO.Path.Combine(repository.Path, "untracked.txt"), "content");

        var status = await provider.GetStatusAsync(repository.Path);

        Assert.True(status.HasUncommittedChanges);
    }

    [Fact]
    public async Task GetStatusAsync_NonRepository_ReturnsNotRepository()
    {
        using var directory = TemporaryDirectory.Create();

        var status = await provider.GetStatusAsync(directory.Path);

        Assert.False(status.IsRepository);
        Assert.Null(status.RepositoryRoot);
        Assert.Null(status.CurrentBranch);
        Assert.False(status.HasUncommittedChanges);
    }

}
