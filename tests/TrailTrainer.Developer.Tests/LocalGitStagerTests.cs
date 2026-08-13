using TrailTrainer.Developer.Git;

namespace TrailTrainer.Developer.Tests;

public sealed class LocalGitStagerTests
{
    private readonly LocalGitStager stager = new();

    [Fact]
    public async Task StageAllAsync_ModifiedUntrackedAndDeletedFiles_StagesAllChanges()
    {
        using var repository = TemporaryGitRepository.Create();
        repository.CommitFile("modified.txt");
        repository.CommitFile("deleted.txt");
        File.WriteAllText(Path.Combine(repository.Path, "modified.txt"), "modified content");
        File.WriteAllText(Path.Combine(repository.Path, "untracked.txt"), "untracked content");
        File.Delete(Path.Combine(repository.Path, "deleted.txt"));

        var result = await stager.StageAllAsync(repository.Path);

        Assert.True(result.HasStagedChanges);
        var stagedFiles = repository.RunGit("diff", "--cached", "--name-status");
        Assert.Contains("M\tmodified.txt", stagedFiles, StringComparison.Ordinal);
        Assert.Contains("A\tuntracked.txt", stagedFiles, StringComparison.Ordinal);
        Assert.Contains("D\tdeleted.txt", stagedFiles, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StageAllAsync_CleanRepository_ReturnsNoStagedChanges()
    {
        using var repository = TemporaryGitRepository.Create();
        repository.CommitFile("initial.txt");

        var result = await stager.StageAllAsync(repository.Path);

        Assert.False(result.HasStagedChanges);
    }

    [Fact]
    public async Task StageAllAsync_FromNestedDirectory_ReturnsRootAndStagesChanges()
    {
        using var repository = TemporaryGitRepository.Create();
        repository.CommitFile("initial.txt");
        var nestedDirectory = Path.Combine(repository.Path, "nested directory");
        Directory.CreateDirectory(nestedDirectory);
        File.WriteAllText(Path.Combine(nestedDirectory, "new.txt"), "content");

        var result = await stager.StageAllAsync(nestedDirectory);

        Assert.Equal(
            Path.GetFullPath(repository.Path),
            result.RepositoryRoot,
            OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        Assert.True(result.HasStagedChanges);
    }

    [Fact]
    public async Task StageAllAsync_NonRepository_Throws()
    {
        using var directory = TemporaryDirectory.Create();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => stager.StageAllAsync(directory.Path));
    }
}
