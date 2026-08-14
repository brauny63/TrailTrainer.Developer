using TrailTrainer.Developer.Core;
using TrailTrainer.Developer.Tasks;

namespace TrailTrainer.Developer.Tests;

public sealed class PullRequestMergeGateTests
{
    [Fact]
    public async Task MergeAsync_SuccessfulGate_DelegatesExactlyOnceAndReturnsExactResults()
    {
        using var source = new CancellationTokenSource();
        var repository = new GitHubRepositoryIdentity("ExactOwner", "ExactRepository");
        var gateResult = GateResult(PullRequestGateState.Successful, "gate-authoritative-sha");
        var mergeResult = new PullRequestMergeResult(42, true, "merge-sha", PullRequestMergeMethod.Squash);
        var statusGate = new FakeStatusGate(gateResult);
        var merger = new FakeMerger(mergeResult);
        var gate = new PullRequestMergeGate(statusGate, merger);

        var result = await gate.MergeAsync(
            repository,
            42,
            PullRequestMergeMethod.Squash,
            "Exact title",
            "Exact message",
            source.Token);

        Assert.Same(gateResult, result.StatusGate);
        Assert.Same(mergeResult, result.Merge);
        Assert.Equal(1, statusGate.CallCount);
        Assert.Equal(1, merger.CallCount);
        Assert.Same(repository, statusGate.Repository);
        Assert.Same(repository, merger.Repository);
        Assert.Equal(42, statusGate.PullRequestNumber);
        Assert.Equal(42, merger.PullRequestNumber);
        Assert.Equal("gate-authoritative-sha", merger.ExpectedHeadSha);
        Assert.Equal(PullRequestMergeMethod.Squash, merger.Method);
        Assert.Equal("Exact title", merger.CommitTitle);
        Assert.Equal("Exact message", merger.CommitMessage);
        Assert.Equal(source.Token, statusGate.CancellationToken);
        Assert.Equal(source.Token, merger.CancellationToken);
    }

    [Theory]
    [InlineData(PullRequestGateState.Pending, "pending")]
    [InlineData(PullRequestGateState.Failed, "failed")]
    public async Task MergeAsync_NonSuccessfulGate_PreventsMergeWithClearDiagnostic(
        PullRequestGateState state,
        string diagnostic)
    {
        var statusGate = new FakeStatusGate(GateResult(state));
        var merger = new FakeMerger(new PullRequestMergeResult(
            42, true, "unused", PullRequestMergeMethod.Merge));
        var gate = new PullRequestMergeGate(statusGate, merger);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            gate.MergeAsync(Repository(), 42, PullRequestMergeMethod.Merge));

        Assert.Contains(diagnostic, exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, statusGate.CallCount);
        Assert.Equal(0, merger.CallCount);
    }

    [Fact]
    public async Task MergeAsync_StatusGateFailure_PreventsMerger()
    {
        var expected = new InvalidDataException("gate failure");
        var statusGate = new FakeStatusGate(GateResult(PullRequestGateState.Successful))
        {
            Exception = expected
        };
        var merger = new FakeMerger(new PullRequestMergeResult(
            42, true, "unused", PullRequestMergeMethod.Merge));

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            new PullRequestMergeGate(statusGate, merger).MergeAsync(
                Repository(), 42, PullRequestMergeMethod.Merge));

        Assert.Same(expected, exception);
        Assert.Equal(1, statusGate.CallCount);
        Assert.Equal(0, merger.CallCount);
    }

    [Fact]
    public async Task MergeAsync_MergerFailure_PropagatesWithoutReevaluationOrRetry()
    {
        var expected = new HttpRequestException("stale head");
        var statusGate = new FakeStatusGate(GateResult(PullRequestGateState.Successful));
        var merger = new FakeMerger(new PullRequestMergeResult(
            42, true, "unused", PullRequestMergeMethod.Rebase))
        {
            Exception = expected
        };

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
            new PullRequestMergeGate(statusGate, merger).MergeAsync(
                Repository(), 42, PullRequestMergeMethod.Rebase));

        Assert.Same(expected, exception);
        Assert.Equal(1, statusGate.CallCount);
        Assert.Equal(1, merger.CallCount);
    }

    [Fact]
    public async Task MergeAsync_NullOptionalValues_AreDelegatedUnchanged()
    {
        var statusGate = new FakeStatusGate(GateResult(PullRequestGateState.Successful));
        var merger = new FakeMerger(new PullRequestMergeResult(
            42, false, null, PullRequestMergeMethod.Merge));

        await new PullRequestMergeGate(statusGate, merger).MergeAsync(
            Repository(), 42, PullRequestMergeMethod.Merge, null, null);

        Assert.Null(merger.CommitTitle);
        Assert.Null(merger.CommitMessage);
    }

    private static GitHubRepositoryIdentity Repository() => new("owner", "repository");

    private static PullRequestStatusGateResult GateResult(
        PullRequestGateState state,
        string sha = "head-sha") => new(42, sha, state, []);

    private sealed class FakeStatusGate(PullRequestStatusGateResult result) : IPullRequestStatusGate
    {
        public Exception? Exception { get; init; }
        public int CallCount { get; private set; }
        public GitHubRepositoryIdentity? Repository { get; private set; }
        public int PullRequestNumber { get; private set; }
        public CancellationToken CancellationToken { get; private set; }

        public Task<PullRequestStatusGateResult> EvaluateAsync(
            GitHubRepositoryIdentity repository,
            int pullRequestNumber,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            Repository = repository;
            PullRequestNumber = pullRequestNumber;
            CancellationToken = cancellationToken;
            return Exception is null
                ? Task.FromResult(result)
                : Task.FromException<PullRequestStatusGateResult>(Exception);
        }
    }

    private sealed class FakeMerger(PullRequestMergeResult result) : IPullRequestMerger
    {
        public Exception? Exception { get; init; }
        public int CallCount { get; private set; }
        public GitHubRepositoryIdentity? Repository { get; private set; }
        public int PullRequestNumber { get; private set; }
        public string? ExpectedHeadSha { get; private set; }
        public PullRequestMergeMethod Method { get; private set; }
        public string? CommitTitle { get; private set; }
        public string? CommitMessage { get; private set; }
        public CancellationToken CancellationToken { get; private set; }

        public Task<PullRequestMergeResult> MergeAsync(
            GitHubRepositoryIdentity repository,
            int pullRequestNumber,
            string expectedHeadSha,
            PullRequestMergeMethod method,
            string? commitTitle = null,
            string? commitMessage = null,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            Repository = repository;
            PullRequestNumber = pullRequestNumber;
            ExpectedHeadSha = expectedHeadSha;
            Method = method;
            CommitTitle = commitTitle;
            CommitMessage = commitMessage;
            CancellationToken = cancellationToken;
            return Exception is null
                ? Task.FromResult(result)
                : Task.FromException<PullRequestMergeResult>(Exception);
        }
    }
}
