using TrailTrainer.Developer.Git;

namespace TrailTrainer.Developer.Tests;

public sealed class LocalGitCommitterTests
{
    private readonly LocalGitCommitter committer = new();

    [Fact]
    public async Task CommitAsync_StagedChanges_CreatesCommitAndReturnsItsDetails()
    {
        using var repository = TemporaryGitRepository.Create();
        repository.CommitFile("initial.txt");
        File.WriteAllText(Path.Combine(repository.Path, "staged.txt"), "staged content");
        repository.RunGit("add", "--", "staged.txt");

        var result = await committer.CommitAsync(repository.Path, "Create staged file");

        Assert.Equal(Path.GetFullPath(repository.Path), result.RepositoryRoot,
            OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        Assert.Equal(repository.RunGit("rev-parse", "HEAD"), result.CommitSha);
        Assert.Equal("Create staged file", result.CommitMessage);
        Assert.Equal("Create staged file", repository.RunGit("log", "-1", "--pretty=%B"));
    }

    [Fact]
    public async Task CommitAsync_DoesNotIncludeUntrackedUnstagedFile()
    {
        using var repository = TemporaryGitRepository.Create();
        repository.CommitFile("initial.txt");
        File.WriteAllText(Path.Combine(repository.Path, "staged.txt"), "staged content");
        File.WriteAllText(Path.Combine(repository.Path, "unstaged.txt"), "unstaged content");
        repository.RunGit("add", "--", "staged.txt");

        await committer.CommitAsync(repository.Path, "Commit staged only");

        Assert.Equal("staged.txt", repository.RunGit("show", "--pretty=format:", "--name-only", "HEAD"));
        Assert.Contains("?? unstaged.txt", repository.RunGit("status", "--porcelain"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CommitAsync_FromNestedDirectory_CreatesCommit()
    {
        using var repository = TemporaryGitRepository.Create();
        repository.CommitFile("initial.txt");
        var nestedDirectory = Path.Combine(repository.Path, "nested directory");
        Directory.CreateDirectory(nestedDirectory);
        File.WriteAllText(Path.Combine(nestedDirectory, "staged.txt"), "content");
        repository.RunGit("add", "--all");

        var result = await committer.CommitAsync(nestedDirectory, "Commit from nested directory");

        Assert.Equal(repository.RunGit("rev-parse", "HEAD"), result.CommitSha);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CommitAsync_EmptyOrWhitespaceMessage_Throws(string commitMessage)
    {
        using var repository = TemporaryGitRepository.Create();

        await Assert.ThrowsAsync<ArgumentException>(
            () => committer.CommitAsync(repository.Path, commitMessage));
    }

    [Fact]
    public async Task CommitAsync_NoStagedChanges_ThrowsWithoutCreatingCommit()
    {
        using var repository = TemporaryGitRepository.Create();
        repository.CommitFile("initial.txt");
        var originalHead = repository.RunGit("rev-parse", "HEAD");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => committer.CommitAsync(repository.Path, "Must not be created"));

        Assert.Equal(originalHead, repository.RunGit("rev-parse", "HEAD"));
    }

    [Fact]
    public async Task CommitAsync_NonRepository_Throws()
    {
        using var directory = TemporaryDirectory.Create();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => committer.CommitAsync(directory.Path, "Commit message"));
    }
}
