using TrailTrainer.Developer.Tasks;

namespace TrailTrainer.Developer.Tests;

public sealed class DeveloperTaskDiscoveryTests
{
    private readonly DeveloperTaskDiscovery discovery = new();

    [Fact]
    public async Task DiscoverAsync_MatchingFiles_ReturnsAbsoluteDescriptorsInNumericOrder()
    {
        using var repository = TemporaryDirectory.Create();
        var taskDirectory = CreateTaskDirectory(repository.Path);
        File.WriteAllText(Path.Combine(taskDirectory, "DEV-0010-Tenth.md"), string.Empty);
        File.WriteAllText(Path.Combine(taskDirectory, "DEV-0002-Second.md"), string.Empty);

        var descriptors = await discovery.DiscoverAsync(repository.Path);

        Assert.Collection(
            descriptors,
            descriptor =>
            {
                Assert.Equal(2, descriptor.Id.Number);
                Assert.Equal("DEV-0002-Second.md", descriptor.FileName);
                Assert.True(Path.IsPathFullyQualified(descriptor.FilePath));
            },
            descriptor => Assert.Equal(10, descriptor.Id.Number));
    }

    [Fact]
    public async Task DiscoverAsync_NonmatchingMarkdownNonMarkdownAndNestedFiles_IgnoresThem()
    {
        using var repository = TemporaryDirectory.Create();
        var taskDirectory = CreateTaskDirectory(repository.Path);
        File.WriteAllText(Path.Combine(taskDirectory, "task.md"), string.Empty);
        File.WriteAllText(Path.Combine(taskDirectory, "DEV-0001-Task.txt"), string.Empty);
        var nestedDirectory = Directory.CreateDirectory(Path.Combine(taskDirectory, "nested")).FullName;
        File.WriteAllText(Path.Combine(nestedDirectory, "DEV-0001-Nested.md"), string.Empty);

        var descriptors = await discovery.DiscoverAsync(repository.Path);

        Assert.Empty(descriptors);
    }

    [Fact]
    public async Task DiscoverAsync_EmptyTaskDirectory_ReturnsEmptyCollection()
    {
        using var repository = TemporaryDirectory.Create();
        CreateTaskDirectory(repository.Path);

        Assert.Empty(await discovery.DiscoverAsync(repository.Path));
    }

    [Fact]
    public async Task DiscoverAsync_MissingRepositoryRoot_Throws()
    {
        using var directory = TemporaryDirectory.Create();
        var missingPath = Path.Combine(directory.Path, "missing");

        await Assert.ThrowsAsync<DirectoryNotFoundException>(
            () => discovery.DiscoverAsync(missingPath));
    }

    [Fact]
    public async Task DiscoverAsync_MissingTaskDirectory_Throws()
    {
        using var repository = TemporaryDirectory.Create();

        await Assert.ThrowsAsync<DirectoryNotFoundException>(
            () => discovery.DiscoverAsync(repository.Path));
    }

    private static string CreateTaskDirectory(string repositoryRoot)
    {
        var taskDirectory = Path.Combine(repositoryRoot, "docs", "developer-tasks");
        Directory.CreateDirectory(taskDirectory);
        return taskDirectory;
    }
}
