using TrailTrainer.Developer.Git;

namespace TrailTrainer.Developer.Tests;

public sealed class LocalGitPusherTests
{
    private readonly LocalGitPusher pusher = new();

    [Fact]
    public async Task PushAsync_WithUpstream_FromNestedDirectoryPushesCurrentBranchAndReturnsDetails()
    {
        using var repository = TemporaryGitRepository.Create();
        using var remote = TemporaryDirectory.Create("bare remote");
        repository.CommitFile("initial.txt");
        repository.RunGit("init", "--bare", remote.Path);
        repository.RunGit("remote", "add", "test-remote", remote.Path);
        var branchName = repository.RunGit("branch", "--show-current");
        var nestedDirectory = Path.Combine(repository.Path, "nested directory");
        Directory.CreateDirectory(nestedDirectory);

        var result = await pusher.PushAsync(nestedDirectory, "test-remote", setUpstream: true);

        Assert.Equal(Path.GetFullPath(repository.Path), result.RepositoryRoot,
            OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        Assert.Equal("test-remote", result.RemoteName);
        Assert.Equal(branchName, result.BranchName);
        Assert.True(result.SetUpstream);
        Assert.NotEmpty(repository.RunGit(
            "--git-dir", remote.Path, "show-ref", "--verify", $"refs/heads/{branchName}"));
        Assert.Equal(
            $"test-remote/{branchName}",
            repository.RunGit("for-each-ref", "--format=%(upstream:short)", $"refs/heads/{branchName}"));
    }

    [Fact]
    public async Task PushAsync_WithoutUpstreamPushesBranchWithoutCreatingTrackingConfiguration()
    {
        using var repository = TemporaryGitRepository.Create();
        using var remote = TemporaryDirectory.Create("bare remote");
        repository.CommitFile("initial.txt");
        repository.RunGit("init", "--bare", remote.Path);
        repository.RunGit("remote", "add", "test-remote", remote.Path);
        var branchName = repository.RunGit("branch", "--show-current");

        var result = await pusher.PushAsync(repository.Path, "test-remote", setUpstream: false);

        Assert.False(result.SetUpstream);
        Assert.NotEmpty(repository.RunGit(
            "--git-dir", remote.Path, "show-ref", "--verify", $"refs/heads/{branchName}"));
        Assert.Empty(repository.RunGit(
            "for-each-ref", "--format=%(upstream:short)", $"refs/heads/{branchName}"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task PushAsync_MissingEmptyOrWhitespaceRemoteName_Throws(string? remoteName)
    {
        using var repository = TemporaryGitRepository.Create();

        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => pusher.PushAsync(repository.Path, remoteName!, setUpstream: false));
    }

    [Fact]
    public async Task PushAsync_MissingRemote_Throws()
    {
        using var repository = TemporaryGitRepository.Create();
        repository.CommitFile("initial.txt");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => pusher.PushAsync(repository.Path, "missing-remote", setUpstream: false));

        Assert.Contains("missing-remote", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PushAsync_NonRepository_Throws()
    {
        using var directory = TemporaryDirectory.Create();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => pusher.PushAsync(directory.Path, "origin", setUpstream: false));
    }

    [Fact]
    public async Task PushAsync_DetachedHead_Throws()
    {
        using var repository = TemporaryGitRepository.Create();
        using var remote = TemporaryDirectory.Create("bare remote");
        repository.CommitFile("initial.txt");
        repository.RunGit("init", "--bare", remote.Path);
        repository.RunGit("remote", "add", "test-remote", remote.Path);
        repository.RunGit("checkout", "--detach");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => pusher.PushAsync(repository.Path, "test-remote", setUpstream: false));
    }
}
