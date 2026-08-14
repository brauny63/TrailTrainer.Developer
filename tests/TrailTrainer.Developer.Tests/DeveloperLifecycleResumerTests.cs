using TrailTrainer.Developer.Core;
using TrailTrainer.Developer.Tasks;

namespace TrailTrainer.Developer.Tests;

public sealed class DeveloperLifecycleResumerTests
{
    [Fact]
    public void ResumeContext_ValidInputIsPreserved()
    {
        var repository = new GitHubRepositoryIdentity("Owner", "Repository");

        var context = new DeveloperLifecycleResumeContext(
            "Exact/directory", repository, 82, "Feature/Exact", "Main", "Exact-Remote");

        Assert.Equal("Exact/directory", context.RepositoryDirectory);
        Assert.Same(repository, context.Repository);
        Assert.Equal(82, context.PullRequestNumber);
        Assert.Equal("Feature/Exact", context.FeatureBranch);
        Assert.Equal("Main", context.BaseBranch);
        Assert.Equal("Exact-Remote", context.GitRemoteName);
    }

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
    public void ResumeContext_InvalidTextRejected(
        string? directory,
        string feature,
        string @base,
        string remote)
    {
        Assert.ThrowsAny<ArgumentException>(() => new DeveloperLifecycleResumeContext(
            directory!, Repository(), 82, feature, @base, remote));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ResumeContext_InvalidPullRequestNumberRejected(int number)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DeveloperLifecycleResumeContext(
            "directory", Repository(), number, "feature", "main", "remote"));
    }

    [Fact]
    public void ResumeContext_NullRepositoryAndEqualBranchesRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new DeveloperLifecycleResumeContext(
            "directory", null!, 82, "feature", "main", "remote"));
        Assert.Throws<ArgumentException>(() => new DeveloperLifecycleResumeContext(
            "directory", Repository(), 82, "main", "main", "remote"));
    }

    [Theory]
    [InlineData(PullRequestGateState.Pending, DeveloperLifecycleState.Pending)]
    [InlineData(PullRequestGateState.Failed, DeveloperLifecycleState.Failed)]
    public async Task ResumeAsync_NonSuccessfulStatusReturnsExactResultsWithoutLaterPhases(
        PullRequestGateState gateState,
        DeveloperLifecycleState lifecycleState)
    {
        var fixture = new Fixture(gateState);

        var result = await fixture.ResumeAsync();

        Assert.Equal(lifecycleState, result.State);
        Assert.Same(fixture.Context, result.Context);
        Assert.Same(fixture.StatusResult, result.StatusGate);
        Assert.Null(result.GatedMerge);
        Assert.Null(result.Cleanup);
        Assert.Equal(["status"], fixture.Calls);
        Assert.Same(fixture.Context.Repository, fixture.StatusGate.Repository);
        Assert.Equal(fixture.Context.PullRequestNumber, fixture.StatusGate.PullRequestNumber);
        Assert.Equal(0, fixture.MergeGate.CallCount);
        Assert.Equal(0, fixture.Cleaner.CallCount);
    }

    [Fact]
    public async Task ResumeAsync_CompletedDelegatesExactContextAndInputsInOrder()
    {
        using var source = new CancellationTokenSource();
        var fixture = new Fixture(PullRequestGateState.Successful);

        var result = await fixture.ResumeAsync(source.Token);

        Assert.Equal(DeveloperLifecycleState.Completed, result.State);
        Assert.Same(fixture.Context, result.Context);
        Assert.Same(fixture.StatusResult, result.StatusGate);
        Assert.Same(fixture.GatedMergeResult, result.GatedMerge);
        Assert.Same(fixture.CleanupResult, result.Cleanup);
        Assert.Equal(["status", "merge", "cleanup"], fixture.Calls);

        Assert.Equal(1, fixture.StatusGate.CallCount);
        Assert.Equal(1, fixture.MergeGate.CallCount);
        Assert.Same(fixture.Context.Repository, fixture.MergeGate.Repository);
        Assert.Equal(82, fixture.MergeGate.PullRequestNumber);
        Assert.Equal(PullRequestMergeMethod.Squash, fixture.MergeGate.Method);
        Assert.Equal("Exact title", fixture.MergeGate.CommitTitle);
        Assert.Equal("Exact message", fixture.MergeGate.CommitMessage);

        Assert.Equal(1, fixture.Cleaner.CallCount);
        Assert.Equal("Exact/directory", fixture.Cleaner.RepositoryDirectory);
        Assert.Same(fixture.Context.Repository, fixture.Cleaner.Repository);
        Assert.Equal(82, fixture.Cleaner.PullRequestNumber);
        Assert.Same(fixture.GatedMergeResult.Merge, fixture.Cleaner.MergeResult);
        Assert.Equal("Feature/Exact", fixture.Cleaner.FeatureBranch);
        Assert.Equal("Main", fixture.Cleaner.BaseBranch);
        Assert.Equal("Exact-Remote", fixture.Cleaner.RemoteName);
        Assert.True(fixture.Cleaner.DeleteRemoteBranch);

        Assert.Equal(source.Token, fixture.StatusGate.CancellationToken);
        Assert.Equal(source.Token, fixture.MergeGate.CancellationToken);
        Assert.Equal(source.Token, fixture.Cleaner.CancellationToken);
    }

    [Fact]
    public async Task ResumeAsync_NullContextRejectedBeforeDependencies()
    {
        var fixture = new Fixture(PullRequestGateState.Successful);

        await Assert.ThrowsAsync<ArgumentNullException>(() => fixture.Resumer.ResumeAsync(
            null!, PullRequestMergeMethod.Merge, null, null, false));

        Assert.Empty(fixture.Calls);
    }

    [Fact]
    public async Task ResumeAsync_StatusFailurePreventsMergeAndCleanup()
    {
        var fixture = new Fixture(PullRequestGateState.Successful);
        var expected = new HttpRequestException("status failed");
        fixture.StatusGate.Exception = expected;

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => fixture.ResumeAsync());

        Assert.Same(expected, exception);
        Assert.Equal(["status"], fixture.Calls);
        Assert.Equal(0, fixture.MergeGate.CallCount);
        Assert.Equal(0, fixture.Cleaner.CallCount);
    }

    [Fact]
    public async Task ResumeAsync_MergeGateFailurePreventsCleanupWithoutRetry()
    {
        var fixture = new Fixture(PullRequestGateState.Successful);
        var expected = new InvalidOperationException("fresh gate changed");
        fixture.MergeGate.Exception = expected;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.ResumeAsync());

        Assert.Same(expected, exception);
        Assert.Equal(["status", "merge"], fixture.Calls);
        Assert.Equal(1, fixture.StatusGate.CallCount);
        Assert.Equal(1, fixture.MergeGate.CallCount);
        Assert.Equal(0, fixture.Cleaner.CallCount);
    }

    [Fact]
    public async Task ResumeAsync_InconsistentNonMergedResultPreventsCleanup()
    {
        var fixture = new Fixture(PullRequestGateState.Successful);
        fixture.MergeGate.Result = new PullRequestGatedMergeResult(
            fixture.StatusResult,
            new PullRequestMergeResult(82, false, null, PullRequestMergeMethod.Squash));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.ResumeAsync());

        Assert.Contains("confirmed successful", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(["status", "merge"], fixture.Calls);
        Assert.Equal(0, fixture.Cleaner.CallCount);
    }

    [Fact]
    public async Task ResumeAsync_CleanupFailurePropagatesWithoutRemergeOrRetry()
    {
        var fixture = new Fixture(PullRequestGateState.Successful);
        var expected = new InvalidOperationException("cleanup failed");
        fixture.Cleaner.Exception = expected;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.ResumeAsync());

        Assert.Same(expected, exception);
        Assert.Equal(["status", "merge", "cleanup"], fixture.Calls);
        Assert.Equal(1, fixture.MergeGate.CallCount);
        Assert.Equal(1, fixture.Cleaner.CallCount);
    }

    [Theory]
    [InlineData("status")]
    [InlineData("merge")]
    [InlineData("cleanup")]
    public async Task ResumeAsync_CancellationAtPhasePreventsLaterPhases(string phase)
    {
        var fixture = new Fixture(PullRequestGateState.Successful);
        fixture.SetException(phase, new OperationCanceledException());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.ResumeAsync());

        var expected = phase switch
        {
            "status" => new[] { "status" },
            "merge" => new[] { "status", "merge" },
            _ => new[] { "status", "merge", "cleanup" }
        };
        Assert.Equal(expected, fixture.Calls);
    }

    [Fact]
    public void ResumeResult_EnforcesPendingAndFailedInvariants()
    {
        var context = Context();
        var pending = Status(PullRequestGateState.Pending);
        var failed = Status(PullRequestGateState.Failed);
        var merge = GatedMerge();
        var cleanup = Cleanup();

        Assert.Throws<ArgumentException>(() => new DeveloperLifecycleResumeResult(
            DeveloperLifecycleState.Pending, context, pending, merge, cleanup));
        Assert.Throws<ArgumentException>(() => new DeveloperLifecycleResumeResult(
            DeveloperLifecycleState.Failed, context, failed, merge, cleanup));
        Assert.Throws<ArgumentException>(() => new DeveloperLifecycleResumeResult(
            DeveloperLifecycleState.Pending, context, failed));
        Assert.Throws<ArgumentException>(() => new DeveloperLifecycleResumeResult(
            DeveloperLifecycleState.Failed, context, pending));
    }

    [Fact]
    public void ResumeResult_CompletedRequiresSuccessfulStatusConfirmedMergeAndCleanup()
    {
        var context = Context();
        var successful = Status(PullRequestGateState.Successful);
        var pending = Status(PullRequestGateState.Pending);
        var merge = GatedMerge();
        var nonMerge = new PullRequestGatedMergeResult(
            successful,
            new PullRequestMergeResult(82, false, null, PullRequestMergeMethod.Squash));
        var cleanup = Cleanup();

        Assert.Throws<ArgumentException>(() => new DeveloperLifecycleResumeResult(
            DeveloperLifecycleState.Completed, context, successful, null, cleanup));
        Assert.Throws<ArgumentException>(() => new DeveloperLifecycleResumeResult(
            DeveloperLifecycleState.Completed, context, successful, merge, null));
        Assert.Throws<ArgumentException>(() => new DeveloperLifecycleResumeResult(
            DeveloperLifecycleState.Completed, context, successful, nonMerge, cleanup));
        Assert.Throws<ArgumentException>(() => new DeveloperLifecycleResumeResult(
            DeveloperLifecycleState.Completed, context, pending, merge, cleanup));
    }

    private static GitHubRepositoryIdentity Repository() => new("Owner", "Repository");
    private static DeveloperLifecycleResumeContext Context() =>
        new("Exact/directory", Repository(), 82, "Feature/Exact", "Main", "Exact-Remote");
    private static PullRequestStatusGateResult Status(PullRequestGateState state) =>
        new(82, "status-head", state, []);
    private static PullRequestGatedMergeResult GatedMerge() => new(
        Status(PullRequestGateState.Successful),
        new PullRequestMergeResult(82, true, "merge-sha", PullRequestMergeMethod.Squash));
    private static PostMergeCleanupResult Cleanup() =>
        new("root", "Main", "Feature/Exact", true, true);

    private sealed class Fixture
    {
        public Fixture(PullRequestGateState state)
        {
            Context = DeveloperLifecycleResumerTests.Context();
            StatusResult = Status(state);
            GatedMergeResult = GatedMerge();
            CleanupResult = Cleanup();
            StatusGate = new FakeStatusGate(StatusResult, Calls);
            MergeGate = new FakeMergeGate(GatedMergeResult, Calls);
            Cleaner = new FakeCleaner(CleanupResult, Calls);
            Resumer = new DeveloperLifecycleResumer(StatusGate, MergeGate, Cleaner);
        }

        public List<string> Calls { get; } = [];
        public DeveloperLifecycleResumeContext Context { get; }
        public PullRequestStatusGateResult StatusResult { get; }
        public PullRequestGatedMergeResult GatedMergeResult { get; }
        public PostMergeCleanupResult CleanupResult { get; }
        public FakeStatusGate StatusGate { get; }
        public FakeMergeGate MergeGate { get; }
        public FakeCleaner Cleaner { get; }
        public DeveloperLifecycleResumer Resumer { get; }

        public Task<DeveloperLifecycleResumeResult> ResumeAsync(CancellationToken token = default) =>
            Resumer.ResumeAsync(
                Context, PullRequestMergeMethod.Squash, "Exact title", "Exact message", true, token);

        public void SetException(string phase, Exception exception)
        {
            if (phase == "status") StatusGate.Exception = exception;
            else if (phase == "merge") MergeGate.Exception = exception;
            else Cleaner.Exception = exception;
        }
    }

    private sealed class FakeStatusGate(PullRequestStatusGateResult result, IList<string> calls)
        : IPullRequestStatusGate
    {
        public Exception? Exception { get; set; }
        public int CallCount { get; private set; }
        public GitHubRepositoryIdentity? Repository { get; private set; }
        public int PullRequestNumber { get; private set; }
        public CancellationToken CancellationToken { get; private set; }

        public Task<PullRequestStatusGateResult> EvaluateAsync(
            GitHubRepositoryIdentity repository, int pullRequestNumber,
            CancellationToken cancellationToken = default)
        {
            calls.Add("status");
            CallCount++;
            Repository = repository;
            PullRequestNumber = pullRequestNumber;
            CancellationToken = cancellationToken;
            return Exception is null ? Task.FromResult(result) : Task.FromException<PullRequestStatusGateResult>(Exception);
        }
    }

    private sealed class FakeMergeGate(PullRequestGatedMergeResult result, IList<string> calls)
        : IPullRequestMergeGate
    {
        public PullRequestGatedMergeResult Result { get; set; } = result;
        public Exception? Exception { get; set; }
        public int CallCount { get; private set; }
        public GitHubRepositoryIdentity? Repository { get; private set; }
        public int PullRequestNumber { get; private set; }
        public PullRequestMergeMethod Method { get; private set; }
        public string? CommitTitle { get; private set; }
        public string? CommitMessage { get; private set; }
        public CancellationToken CancellationToken { get; private set; }

        public Task<PullRequestGatedMergeResult> MergeAsync(
            GitHubRepositoryIdentity repository, int pullRequestNumber,
            PullRequestMergeMethod method, string? commitTitle = null,
            string? commitMessage = null, CancellationToken cancellationToken = default)
        {
            calls.Add("merge");
            CallCount++;
            Repository = repository;
            PullRequestNumber = pullRequestNumber;
            Method = method;
            CommitTitle = commitTitle;
            CommitMessage = commitMessage;
            CancellationToken = cancellationToken;
            return Exception is null ? Task.FromResult(Result) : Task.FromException<PullRequestGatedMergeResult>(Exception);
        }
    }

    private sealed class FakeCleaner(PostMergeCleanupResult result, IList<string> calls) : IPostMergeCleaner
    {
        public Exception? Exception { get; set; }
        public int CallCount { get; private set; }
        public string? RepositoryDirectory { get; private set; }
        public GitHubRepositoryIdentity? Repository { get; private set; }
        public int PullRequestNumber { get; private set; }
        public PullRequestMergeResult? MergeResult { get; private set; }
        public string? FeatureBranch { get; private set; }
        public string? BaseBranch { get; private set; }
        public string? RemoteName { get; private set; }
        public bool DeleteRemoteBranch { get; private set; }
        public CancellationToken CancellationToken { get; private set; }

        public Task<PostMergeCleanupResult> CleanupAsync(
            string repositoryDirectory, GitHubRepositoryIdentity repository,
            int pullRequestNumber, PullRequestMergeResult mergeResult,
            string featureBranch, string baseBranch, string remoteName,
            bool deleteRemoteBranch, CancellationToken cancellationToken = default)
        {
            calls.Add("cleanup");
            CallCount++;
            RepositoryDirectory = repositoryDirectory;
            Repository = repository;
            PullRequestNumber = pullRequestNumber;
            MergeResult = mergeResult;
            FeatureBranch = featureBranch;
            BaseBranch = baseBranch;
            RemoteName = remoteName;
            DeleteRemoteBranch = deleteRemoteBranch;
            CancellationToken = cancellationToken;
            return Exception is null ? Task.FromResult(result) : Task.FromException<PostMergeCleanupResult>(Exception);
        }
    }
}
