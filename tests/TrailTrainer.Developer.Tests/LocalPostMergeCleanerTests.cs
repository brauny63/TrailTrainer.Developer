using TrailTrainer.Developer.Core;
using TrailTrainer.Developer.Git;

namespace TrailTrainer.Developer.Tests;

public sealed class LocalPostMergeCleanerTests
{
    private readonly LocalPostMergeCleaner cleaner = new();

    public static TheoryData<string?, string, string, string> InvalidTextInputs => new()
    {
        { null, "feature", "main", "remote" },
        { "", "feature", "main", "remote" },
        { "   ", "feature", "main", "remote" },
        { "directory", "", "main", "remote" },
        { "directory", "   ", "main", "remote" },
        { "directory", "feature", "", "remote" },
        { "directory", "feature", "   ", "remote" },
        { "directory", "feature", "main", "" },
        { "directory", "feature", "main", "   " }
    };

    [Theory]
    [MemberData(nameof(InvalidTextInputs))]
    public async Task CleanupAsync_InvalidTextInputRejectedBeforeStatus(
        string? directory,
        string feature,
        string @base,
        string remote)
    {
        var status = new RecordingStatusProvider(GitRepositoryStatus.NotRepository);

        await Assert.ThrowsAnyAsync<ArgumentException>(() => new LocalPostMergeCleaner(status).CleanupAsync(
            directory!, Repository(), 42, SuccessfulMerge(), feature, @base, remote, false));

        Assert.Equal(0, status.CallCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task CleanupAsync_InvalidPullRequestNumberRejectedBeforeStatus(int number)
    {
        var status = new RecordingStatusProvider(GitRepositoryStatus.NotRepository);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            new LocalPostMergeCleaner(status).CleanupAsync(
                "directory", Repository(), number, SuccessfulMerge(), "feature", "main", "remote", false));

        Assert.Equal(0, status.CallCount);
    }

    [Fact]
    public async Task CleanupAsync_InvalidMergeConfirmationRejectedBeforeStatus()
    {
        var status = new RecordingStatusProvider(GitRepositoryStatus.NotRepository);
        var service = new LocalPostMergeCleaner(status);

        await Assert.ThrowsAsync<ArgumentNullException>(() => service.CleanupAsync(
            "directory", Repository(), 42, null!, "feature", "main", "remote", false));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CleanupAsync(
            "directory", Repository(), 42,
            new PullRequestMergeResult(42, false, null, PullRequestMergeMethod.Merge),
            "feature", "main", "remote", false));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CleanupAsync(
            "directory", Repository(), 42,
            new PullRequestMergeResult(41, true, "merge-sha", PullRequestMergeMethod.Merge),
            "feature", "main", "remote", false));

        Assert.Equal(0, status.CallCount);
    }

    [Fact]
    public async Task CleanupAsync_EqualFeatureAndBaseRejectedBeforeStatus()
    {
        var status = new RecordingStatusProvider(GitRepositoryStatus.NotRepository);

        await Assert.ThrowsAsync<ArgumentException>(() => new LocalPostMergeCleaner(status).CleanupAsync(
            "directory", Repository(), 42, SuccessfulMerge(), "main", "main", "remote", false));

        Assert.Equal(0, status.CallCount);
    }

    [Fact]
    public async Task CleanupAsync_NonRepositoryRejected()
    {
        using var directory = TemporaryDirectory.Create();

        await Assert.ThrowsAsync<InvalidOperationException>(() => cleaner.CleanupAsync(
            directory.Path, Repository(), 42, SuccessfulMerge(), "feature", "main", "remote", false));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CleanupAsync_DirtyOrUntrackedRepositoryRejectedWithoutSwitch(bool tracked)
    {
        using var fixture = CleanupRepository.Create();
        var path = Path.Combine(fixture.Repository.Path, tracked ? "initial.txt" : "untracked.txt");
        File.WriteAllText(path, "dirty");

        await Assert.ThrowsAsync<InvalidOperationException>(() => cleaner.CleanupAsync(
            fixture.Repository.Path, Repository(), 42, SuccessfulMerge(),
            fixture.FeatureBranch, "main", fixture.RemoteName, false));

        Assert.Equal(fixture.FeatureBranch, fixture.CurrentBranch());
    }

    [Fact]
    public async Task CleanupAsync_DetachedHeadRejected()
    {
        using var fixture = CleanupRepository.Create();
        fixture.Repository.RunGit("checkout", "--detach");

        await Assert.ThrowsAsync<InvalidOperationException>(() => cleaner.CleanupAsync(
            fixture.Repository.Path, Repository(), 42, SuccessfulMerge(),
            fixture.FeatureBranch, "main", fixture.RemoteName, false));
    }

    [Fact]
    public async Task CleanupAsync_FromSubdirectory_SwitchesUpdatesAndDeletesMergedLocalBranch()
    {
        using var fixture = CleanupRepository.Create();
        var nested = Path.Combine(fixture.Repository.Path, "nested", "directory");
        Directory.CreateDirectory(nested);

        var result = await cleaner.CleanupAsync(
            nested, Repository(), 42, SuccessfulMerge(),
            fixture.FeatureBranch, "main", fixture.RemoteName, false);

        Assert.Equal(Path.GetFullPath(fixture.Repository.Path), result.RepositoryRoot,
            OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        Assert.Equal("main", result.BaseBranch);
        Assert.Equal(fixture.FeatureBranch, result.FeatureBranch);
        Assert.True(result.LocalBranchDeleted);
        Assert.False(result.RemoteBranchDeleted);
        Assert.Equal("main", fixture.CurrentBranch());
        Assert.False(fixture.LocalBranchExists(fixture.FeatureBranch));
        Assert.True(fixture.RemoteBranchExists(fixture.FeatureBranch));
    }

    [Fact]
    public async Task CleanupAsync_AlreadyOnBaseAndMissingLocalBranch_IsTolerated()
    {
        using var fixture = CleanupRepository.Create();
        fixture.Repository.RunGit("switch", "main");
        fixture.Repository.RunGit("branch", "-d", fixture.FeatureBranch);

        var result = await cleaner.CleanupAsync(
            fixture.Repository.Path, Repository(), 42, SuccessfulMerge(),
            fixture.FeatureBranch, "main", fixture.RemoteName, false);

        Assert.False(result.LocalBranchDeleted);
        Assert.False(result.RemoteBranchDeleted);
        Assert.Equal("main", fixture.CurrentBranch());
    }

    [Fact]
    public async Task CleanupAsync_MissingRemoteRejectedBeforeSwitch()
    {
        using var fixture = CleanupRepository.Create();

        await Assert.ThrowsAsync<InvalidOperationException>(() => cleaner.CleanupAsync(
            fixture.Repository.Path, Repository(), 42, SuccessfulMerge(),
            fixture.FeatureBranch, "main", "missing-remote", false));

        Assert.Equal(fixture.FeatureBranch, fixture.CurrentBranch());
        Assert.True(fixture.LocalBranchExists(fixture.FeatureBranch));
    }

    [Fact]
    public async Task CleanupAsync_MissingBaseRejectedBeforeMutation()
    {
        using var fixture = CleanupRepository.Create();

        await Assert.ThrowsAsync<InvalidOperationException>(() => cleaner.CleanupAsync(
            fixture.Repository.Path, Repository(), 42, SuccessfulMerge(),
            fixture.FeatureBranch, "missing-base", fixture.RemoteName, false));

        Assert.Equal(fixture.FeatureBranch, fixture.CurrentBranch());
    }

    [Fact]
    public async Task CleanupAsync_PullFailurePreventsLocalDeletion()
    {
        using var fixture = CleanupRepository.Create();
        fixture.Repository.RunGit("--git-dir", fixture.Remote.Path, "branch", "-D", "main");

        await Assert.ThrowsAsync<InvalidOperationException>(() => cleaner.CleanupAsync(
            fixture.Repository.Path, Repository(), 42, SuccessfulMerge(),
            fixture.FeatureBranch, "main", fixture.RemoteName, false));

        Assert.Equal("main", fixture.CurrentBranch());
        Assert.True(fixture.LocalBranchExists(fixture.FeatureBranch));
    }

    [Fact]
    public async Task CleanupAsync_UnmergedLocalBranchIsNotForceDeletedAndPreventsRemoteDeletion()
    {
        using var fixture = CleanupRepository.Create(featureMerged: false);

        await Assert.ThrowsAsync<InvalidOperationException>(() => cleaner.CleanupAsync(
            fixture.Repository.Path, Repository(), 42, SuccessfulMerge(),
            fixture.FeatureBranch, "main", fixture.RemoteName, true));

        Assert.True(fixture.LocalBranchExists(fixture.FeatureBranch));
        Assert.True(fixture.RemoteBranchExists(fixture.FeatureBranch));
    }

    [Fact]
    public async Task CleanupAsync_RequestedRemoteDeletionDeletesOnlyFeatureFromSuppliedRemote()
    {
        using var fixture = CleanupRepository.Create();
        fixture.Repository.RunGit("branch", "other-branch", "main");
        fixture.Repository.RunGit("push", fixture.RemoteName, "other-branch");

        var result = await cleaner.CleanupAsync(
            fixture.Repository.Path, Repository(), 42, SuccessfulMerge(),
            fixture.FeatureBranch, "main", fixture.RemoteName, true);

        Assert.True(result.LocalBranchDeleted);
        Assert.True(result.RemoteBranchDeleted);
        Assert.False(fixture.RemoteBranchExists(fixture.FeatureBranch));
        Assert.True(fixture.RemoteBranchExists("other-branch"));
        Assert.True(fixture.RemoteBranchExists("main"));
    }

    [Fact]
    public async Task CleanupAsync_MissingRemoteFeatureBranchIsTolerated()
    {
        using var fixture = CleanupRepository.Create(pushFeature: false);

        var result = await cleaner.CleanupAsync(
            fixture.Repository.Path, Repository(), 42, SuccessfulMerge(),
            fixture.FeatureBranch, "main", fixture.RemoteName, true);

        Assert.True(result.LocalBranchDeleted);
        Assert.False(result.RemoteBranchDeleted);
    }

    [Fact]
    public async Task CleanupAsync_RemoteDeleteFailurePropagatesAfterLocalDeletion()
    {
        using var fixture = CleanupRepository.Create();
        fixture.Repository.RunGit("--git-dir", fixture.Remote.Path, "config", "receive.denyDeletes", "true");

        await Assert.ThrowsAsync<InvalidOperationException>(() => cleaner.CleanupAsync(
            fixture.Repository.Path, Repository(), 42, SuccessfulMerge(),
            fixture.FeatureBranch, "main", fixture.RemoteName, true));

        Assert.False(fixture.LocalBranchExists(fixture.FeatureBranch));
        Assert.True(fixture.RemoteBranchExists(fixture.FeatureBranch));
    }

    [Fact]
    public async Task CleanupAsync_CancellationFromStatusStopsBeforeMutationAndUsesSameToken()
    {
        using var fixture = CleanupRepository.Create();
        using var source = new CancellationTokenSource();
        source.Cancel();
        var status = new RecordingStatusProvider(GitRepositoryStatus.NotRepository);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new LocalPostMergeCleaner(status).CleanupAsync(
                fixture.Repository.Path, Repository(), 42, SuccessfulMerge(),
                fixture.FeatureBranch, "main", fixture.RemoteName, true, source.Token));

        Assert.Equal(source.Token, status.CancellationToken);
        Assert.Equal(fixture.FeatureBranch, fixture.CurrentBranch());
    }

    private static GitHubRepositoryIdentity Repository() => new("owner", "repository");

    private static PullRequestMergeResult SuccessfulMerge() =>
        new(42, true, "merge-sha", PullRequestMergeMethod.Merge);

    private sealed class RecordingStatusProvider(GitRepositoryStatus result) : IGitRepositoryStatusProvider
    {
        public int CallCount { get; private set; }
        public CancellationToken CancellationToken { get; private set; }

        public Task<GitRepositoryStatus> GetStatusAsync(
            string directoryPath,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            CancellationToken = cancellationToken;
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(result);
        }
    }

    private sealed class CleanupRepository : IDisposable
    {
        private CleanupRepository(TemporaryGitRepository repository, TemporaryDirectory remote)
        {
            Repository = repository;
            Remote = remote;
        }

        public TemporaryGitRepository Repository { get; }
        public TemporaryDirectory Remote { get; }
        public string RemoteName => "cleanup-remote";
        public string FeatureBranch => "feature/cleanup";

        public static CleanupRepository Create(bool featureMerged = true, bool pushFeature = true)
        {
            var repository = TemporaryGitRepository.Create();
            var remote = TemporaryDirectory.Create("cleanup bare remote");
            var fixture = new CleanupRepository(repository, remote);
            try
            {
                repository.CommitFile("initial.txt");
                repository.RunGit("branch", "-M", "main");
                repository.RunGit("init", "--bare", remote.Path);
                repository.RunGit("remote", "add", fixture.RemoteName, remote.Path);
                repository.RunGit("push", fixture.RemoteName, "main");
                repository.RunGit("switch", "-c", fixture.FeatureBranch);
                repository.CommitFile("feature.txt");
                if (pushFeature)
                {
                    repository.RunGit("push", fixture.RemoteName, fixture.FeatureBranch);
                }

                if (featureMerged)
                {
                    repository.RunGit("switch", "main");
                    repository.RunGit("merge", "--no-ff", "-m", "Merge feature", fixture.FeatureBranch);
                    repository.RunGit("push", fixture.RemoteName, "main");
                    repository.RunGit("switch", fixture.FeatureBranch);
                }

                return fixture;
            }
            catch
            {
                fixture.Dispose();
                throw;
            }
        }

        public string CurrentBranch() => Repository.RunGit("branch", "--show-current");

        public bool LocalBranchExists(string branch) =>
            Repository.RunGit("for-each-ref", "--format=%(refname)", $"refs/heads/{branch}").Length > 0;

        public bool RemoteBranchExists(string branch) =>
            Repository.RunGit("--git-dir", Remote.Path, "for-each-ref", "--format=%(refname)",
                $"refs/heads/{branch}").Length > 0;

        public void Dispose()
        {
            Repository.Dispose();
            Remote.Dispose();
        }
    }
}
