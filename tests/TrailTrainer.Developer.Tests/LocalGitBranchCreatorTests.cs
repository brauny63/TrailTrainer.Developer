using TrailTrainer.Developer.Git;

namespace TrailTrainer.Developer.Tests;

public sealed class LocalGitBranchCreatorTests
{
    private readonly LocalGitBranchCreator branchCreator = new();

    [Fact]
    public async Task CreateAsync_FromNestedDirectory_CreatesAndSwitchesBranch()
    {
        using var repository = CreateRepositoryWithCommit();
        var nestedDirectory = System.IO.Path.Combine(repository.Path, "nested directory");
        Directory.CreateDirectory(nestedDirectory);

        var result = await branchCreator.CreateAsync(nestedDirectory, "feature/test-branch");

        Assert.Equal(
            System.IO.Path.GetFullPath(repository.Path),
            result.RepositoryRoot,
            OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        Assert.Equal("feature/test-branch", result.BranchName);
        Assert.Equal("feature/test-branch", repository.RunGit("branch", "--show-current"));
    }

    [Fact]
    public async Task CreateAsync_ExistingBranch_ThrowsAndDoesNotSwitch()
    {
        using var repository = CreateRepositoryWithCommit();
        var originalBranch = repository.RunGit("branch", "--show-current");
        repository.RunGit("branch", "existing-branch");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => branchCreator.CreateAsync(repository.Path, "existing-branch"));

        Assert.Contains("existing-branch", exception.Message, StringComparison.Ordinal);
        Assert.Equal(originalBranch, repository.RunGit("branch", "--show-current"));
    }

    [Fact]
    public async Task CreateAsync_NonRepository_Throws()
    {
        using var directory = TemporaryDirectory.Create();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => branchCreator.CreateAsync(directory.Path, "feature/test-branch"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateAsync_EmptyOrWhitespaceBranchName_Throws(string branchName)
    {
        using var repository = CreateRepositoryWithCommit();

        await Assert.ThrowsAsync<ArgumentException>(
            () => branchCreator.CreateAsync(repository.Path, branchName));
    }

    private static TemporaryGitRepository CreateRepositoryWithCommit()
    {
        var repository = TemporaryGitRepository.Create();
        repository.CommitFile("initial.txt");
        return repository;
    }
}
