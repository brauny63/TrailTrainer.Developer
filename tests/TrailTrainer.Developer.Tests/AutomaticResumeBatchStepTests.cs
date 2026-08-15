using TrailTrainer.Developer.Core;
using TrailTrainer.Developer.Tasks;

namespace TrailTrainer.Developer.Tests;

public sealed class AutomaticResumeBatchStepTests
{
    [Fact]
    public void Request_PreservesValuesExactlyAndMatchesDev0025MergeBehavior()
    {
        var request = new AutomaticResumeBatchStepRequest(
            (PullRequestMergeMethod)99, "Exact Title", "Exact Message", true);
        var nullOptionals = new AutomaticResumeBatchStepRequest(
            PullRequestMergeMethod.Merge, null, null, false);

        Assert.Equal((PullRequestMergeMethod)99, request.MergeMethod);
        Assert.Equal("Exact Title", request.MergeCommitTitle);
        Assert.Equal("Exact Message", request.MergeCommitMessage);
        Assert.True(request.DeleteRemoteBranch);
        Assert.Null(nullOptionals.MergeCommitTitle);
        Assert.Null(nullOptionals.MergeCommitMessage);
        Assert.False(nullOptionals.DeleteRemoteBranch);
    }

    [Fact]
    public void Result_RejectsNullResumeAndUnsupportedState()
    {
        Assert.Throws<ArgumentNullException>(() => new AutomaticResumeBatchStepResult(
            AutomaticResumeBatchStepState.Empty, null!, false));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AutomaticResumeBatchStepResult(
            (AutomaticResumeBatchStepState)99, ResumeResult(AutomaticPersistedLifecycleResumeState.NotFound), false));
    }

    [Fact]
    public void Result_EmptyRequiresNotFoundAndNoMoreWorkAndPreservesIdentity()
    {
        var notFound = ResumeResult(AutomaticPersistedLifecycleResumeState.NotFound);

        Assert.Throws<ArgumentException>(() => new AutomaticResumeBatchStepResult(
            AutomaticResumeBatchStepState.Empty,
            ResumeResult(AutomaticPersistedLifecycleResumeState.Pending),
            false));
        Assert.Throws<ArgumentException>(() => new AutomaticResumeBatchStepResult(
            AutomaticResumeBatchStepState.Empty, notFound, true));
        var result = new AutomaticResumeBatchStepResult(
            AutomaticResumeBatchStepState.Empty, notFound, false);

        Assert.Same(notFound, result.Resume);
        Assert.False(result.MoreWork);
    }

    [Theory]
    [InlineData(AutomaticResumeBatchStepState.Pending, AutomaticPersistedLifecycleResumeState.Pending)]
    [InlineData(AutomaticResumeBatchStepState.Failed, AutomaticPersistedLifecycleResumeState.Failed)]
    [InlineData(AutomaticResumeBatchStepState.Completed, AutomaticPersistedLifecycleResumeState.Completed)]
    public void Result_NonEmptyRequiresMatchingResumeAndPreservesIdentity(
        AutomaticResumeBatchStepState state,
        AutomaticPersistedLifecycleResumeState resumeState)
    {
        var resume = ResumeResult(resumeState);

        Assert.Throws<ArgumentException>(() => new AutomaticResumeBatchStepResult(
            state, ResumeResult(AutomaticPersistedLifecycleResumeState.NotFound), false));
        var withoutMoreWork = new AutomaticResumeBatchStepResult(state, resume, false);
        var withMoreWork = new AutomaticResumeBatchStepResult(state, resume, true);

        Assert.Same(resume, withoutMoreWork.Resume);
        Assert.Same(resume, withMoreWork.Resume);
        Assert.False(withoutMoreWork.MoreWork);
        Assert.True(withMoreWork.MoreWork);
    }

    [Fact]
    public async Task ExecuteAsync_NullRequestRejectedBeforeResume()
    {
        var fixture = new Fixture();

        await Assert.ThrowsAsync<ArgumentNullException>(() => fixture.Service.ExecuteAsync(null!));

        Assert.Empty(fixture.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_DelegatesExactOptionsAndTokenToDev0025Once()
    {
        using var source = new CancellationTokenSource();
        var fixture = new Fixture();
        var request = Request();

        await fixture.Service.ExecuteAsync(request, source.Token);

        Assert.Equal(1, fixture.Resumer.CallCount);
        var delegated = Assert.IsType<AutomaticPersistedLifecycleResumeRequest>(fixture.Resumer.Request);
        Assert.Equal(request.MergeMethod, delegated.MergeMethod);
        Assert.Equal(request.MergeCommitTitle, delegated.MergeCommitTitle);
        Assert.Equal(request.MergeCommitMessage, delegated.MergeCommitMessage);
        Assert.Equal(request.DeleteRemoteBranch, delegated.DeleteRemoteBranch);
        Assert.Equal(source.Token, fixture.Resumer.CancellationToken);
    }

    [Fact]
    public async Task ExecuteAsync_Dev0025FailurePropagatesAndPreventsDiscoveryWithoutRetry()
    {
        var fixture = new Fixture();
        var expected = new IOException("resume failed");
        fixture.Resumer.Exception = expected;

        var exception = await Assert.ThrowsAsync<IOException>(() => fixture.Service.ExecuteAsync(Request()));

        Assert.Same(expected, exception);
        Assert.Equal(["resume"], fixture.Calls);
        Assert.Equal(1, fixture.Resumer.CallCount);
        Assert.Equal(0, fixture.Discovery.CallCount);
    }

    [Fact]
    public async Task ExecuteAsync_NotFoundMapsToEmptyWithoutDiscoveryAndPreservesResume()
    {
        var fixture = new Fixture
        {
            ResumeResult = ResumeResult(AutomaticPersistedLifecycleResumeState.NotFound)
        };

        var result = await fixture.Service.ExecuteAsync(Request());

        Assert.Equal(AutomaticResumeBatchStepState.Empty, result.State);
        Assert.False(result.MoreWork);
        Assert.Same(fixture.ResumeResult, result.Resume);
        Assert.Equal(["resume"], fixture.Calls);
        Assert.Equal(0, fixture.Discovery.CallCount);
    }

    [Theory]
    [InlineData(AutomaticPersistedLifecycleResumeState.Pending, AutomaticResumeBatchStepState.Pending, false)]
    [InlineData(AutomaticPersistedLifecycleResumeState.Pending, AutomaticResumeBatchStepState.Pending, true)]
    [InlineData(AutomaticPersistedLifecycleResumeState.Failed, AutomaticResumeBatchStepState.Failed, false)]
    [InlineData(AutomaticPersistedLifecycleResumeState.Failed, AutomaticResumeBatchStepState.Failed, true)]
    [InlineData(AutomaticPersistedLifecycleResumeState.Completed, AutomaticResumeBatchStepState.Completed, false)]
    [InlineData(AutomaticPersistedLifecycleResumeState.Completed, AutomaticResumeBatchStepState.Completed, true)]
    public async Task ExecuteAsync_MapsOutcomeAndDiscoversOnceAfterResume(
        AutomaticPersistedLifecycleResumeState resumeState,
        AutomaticResumeBatchStepState expectedState,
        bool hasStates)
    {
        using var source = new CancellationTokenSource();
        var fixture = new Fixture
        {
            ResumeResult = ResumeResult(resumeState),
            DiscoveredStates = hasStates ? [State("remaining")] : []
        };

        var result = await fixture.Service.ExecuteAsync(Request(), source.Token);

        Assert.Equal(expectedState, result.State);
        Assert.Equal(hasStates, result.MoreWork);
        Assert.Same(fixture.ResumeResult, result.Resume);
        Assert.Equal(["resume", "discover"], fixture.Calls);
        Assert.Equal(1, fixture.Resumer.CallCount);
        Assert.Equal(1, fixture.Discovery.CallCount);
        Assert.Equal(source.Token, fixture.Discovery.CancellationToken);
    }

    [Fact]
    public async Task ExecuteAsync_DiscoveryFailurePropagatesWithoutSecondResumeOrDiscovery()
    {
        var fixture = new Fixture
        {
            ResumeResult = ResumeResult(AutomaticPersistedLifecycleResumeState.Completed)
        };
        var expected = new InvalidDataException("discovery failed");
        fixture.Discovery.Exception = expected;

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            fixture.Service.ExecuteAsync(Request()));

        Assert.Same(expected, exception);
        Assert.Equal(["resume", "discover"], fixture.Calls);
        Assert.Equal(1, fixture.Resumer.CallCount);
        Assert.Equal(1, fixture.Discovery.CallCount);
    }

    [Fact]
    public async Task ExecuteAsync_NullDiscoveryOutputFailsClearlyWithoutRetry()
    {
        var fixture = new Fixture
        {
            ResumeResult = ResumeResult(AutomaticPersistedLifecycleResumeState.Pending),
            DiscoveredStates = null!
        };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Service.ExecuteAsync(Request()));

        Assert.Contains("null collection", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(["resume", "discover"], fixture.Calls);
        Assert.Equal(1, fixture.Resumer.CallCount);
        Assert.Equal(1, fixture.Discovery.CallCount);
    }

    [Fact]
    public async Task ExecuteAsync_PreCancelledDev0025CancellationPreventsDiscovery()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        var fixture = new Fixture();
        fixture.Resumer.HonorCancellation = true;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            fixture.Service.ExecuteAsync(Request(), source.Token));

        Assert.Equal(["resume"], fixture.Calls);
        Assert.Equal(0, fixture.Discovery.CallCount);
    }

    [Fact]
    public async Task ExecuteAsync_DiscoveryCancellationPropagatesAsCancellation()
    {
        using var source = new CancellationTokenSource();
        var fixture = new Fixture
        {
            ResumeResult = ResumeResult(AutomaticPersistedLifecycleResumeState.Failed)
        };
        fixture.Discovery.HonorCancellation = true;
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            fixture.Service.ExecuteAsync(Request(), source.Token));

        Assert.Equal(["resume", "discover"], fixture.Calls);
        Assert.Equal(1, fixture.Discovery.CallCount);
    }

    [Fact]
    public async Task ExecuteAsync_CancellationBetweenResumeAndDiscoveryPropagates()
    {
        using var source = new CancellationTokenSource();
        var fixture = new Fixture
        {
            ResumeResult = ResumeResult(AutomaticPersistedLifecycleResumeState.Completed)
        };
        fixture.Resumer.AfterCall = source.Cancel;
        fixture.Discovery.HonorCancellation = true;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            fixture.Service.ExecuteAsync(Request(), source.Token));

        Assert.Equal(["resume", "discover"], fixture.Calls);
        Assert.Equal(1, fixture.Resumer.CallCount);
        Assert.Equal(1, fixture.Discovery.CallCount);
    }

    private static AutomaticResumeBatchStepRequest Request() => new(
        PullRequestMergeMethod.Squash, "Exact batch title", "Exact batch message", true);

    private static AutomaticPersistedLifecycleResumeResult ResumeResult(
        AutomaticPersistedLifecycleResumeState state)
    {
        if (state == AutomaticPersistedLifecycleResumeState.NotFound)
        {
            return new AutomaticPersistedLifecycleResumeResult(state, NotFoundCandidate());
        }

        var persistedState = State("DEV-0026");
        var candidate = FoundCandidate(persistedState);
        var persistedResumeState = state switch
        {
            AutomaticPersistedLifecycleResumeState.Pending => PersistedDeveloperLifecycleResumeState.Pending,
            AutomaticPersistedLifecycleResumeState.Failed => PersistedDeveloperLifecycleResumeState.Failed,
            _ => PersistedDeveloperLifecycleResumeState.Completed
        };
        var resume = new PersistedDeveloperLifecycleResumeResult(
            persistedResumeState,
            persistedState.TaskId,
            persistedState,
            LifecycleResume(persistedResumeState, persistedState.ResumeContext));
        return new AutomaticPersistedLifecycleResumeResult(state, candidate, resume);
    }

    private static AutomaticResumeCandidateResult NotFoundCandidate() =>
        new(AutomaticResumeCandidateState.NotFound);

    private static AutomaticResumeCandidateResult FoundCandidate(DeveloperLifecyclePersistedState state) =>
        new(
            AutomaticResumeCandidateState.Found,
            state,
            new PersistedLifecycleResumeTarget(state.TaskId, state));

    private static DeveloperLifecyclePersistedState State(string taskId) => new(
        taskId,
        null,
        new DeveloperLifecycleResumeContext(
            "repository",
            new GitHubRepositoryIdentity("owner", "repository"),
            26,
            "feature/batch-step",
            "main",
            "origin"),
        new DateTimeOffset(2026, 8, 15, 12, 0, 0, TimeSpan.Zero));

    private static DeveloperLifecycleResumeResult LifecycleResume(
        PersistedDeveloperLifecycleResumeState state,
        DeveloperLifecycleResumeContext context)
    {
        var lifecycleState = state switch
        {
            PersistedDeveloperLifecycleResumeState.Pending => DeveloperLifecycleState.Pending,
            PersistedDeveloperLifecycleResumeState.Failed => DeveloperLifecycleState.Failed,
            _ => DeveloperLifecycleState.Completed
        };
        var gateState = lifecycleState switch
        {
            DeveloperLifecycleState.Pending => PullRequestGateState.Pending,
            DeveloperLifecycleState.Failed => PullRequestGateState.Failed,
            _ => PullRequestGateState.Successful
        };
        var status = new PullRequestStatusGateResult(26, "head-sha", gateState, []);
        if (lifecycleState != DeveloperLifecycleState.Completed)
        {
            return new DeveloperLifecycleResumeResult(lifecycleState, context, status);
        }

        var merge = new PullRequestGatedMergeResult(
            status,
            new PullRequestMergeResult(26, true, "merge-sha", PullRequestMergeMethod.Squash));
        var cleanup = new PostMergeCleanupResult("repository", "main", "feature/batch-step", true, true);
        return new DeveloperLifecycleResumeResult(lifecycleState, context, status, merge, cleanup);
    }

    private sealed class Fixture
    {
        public Fixture()
        {
            Resumer = new FakeResumer(Calls, () => ResumeResult);
            Discovery = new FakeDiscovery(Calls, () => DiscoveredStates);
            Service = new AutomaticResumeBatchStep(Resumer, Discovery);
        }

        public List<string> Calls { get; } = [];
        public AutomaticPersistedLifecycleResumeResult ResumeResult { get; set; } =
            AutomaticResumeBatchStepTests.ResumeResult(AutomaticPersistedLifecycleResumeState.NotFound);
        public IReadOnlyList<DeveloperLifecyclePersistedState> DiscoveredStates { get; set; } = [];
        public FakeResumer Resumer { get; }
        public FakeDiscovery Discovery { get; }
        public AutomaticResumeBatchStep Service { get; }
    }

    private sealed class FakeResumer(
        IList<string> calls,
        Func<AutomaticPersistedLifecycleResumeResult> result) : IAutomaticPersistedLifecycleResumer
    {
        public Exception? Exception { get; set; }
        public bool HonorCancellation { get; set; }
        public Action? AfterCall { get; set; }
        public int CallCount { get; private set; }
        public AutomaticPersistedLifecycleResumeRequest? Request { get; private set; }
        public CancellationToken CancellationToken { get; private set; }

        public Task<AutomaticPersistedLifecycleResumeResult> ResumeAsync(
            AutomaticPersistedLifecycleResumeRequest request,
            CancellationToken cancellationToken = default)
        {
            calls.Add("resume");
            CallCount++;
            Request = request;
            CancellationToken = cancellationToken;
            if (HonorCancellation)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            AfterCall?.Invoke();
            return Exception is null
                ? Task.FromResult(result())
                : Task.FromException<AutomaticPersistedLifecycleResumeResult>(Exception);
        }
    }

    private sealed class FakeDiscovery(
        IList<string> calls,
        Func<IReadOnlyList<DeveloperLifecyclePersistedState>> states) : IDeveloperLifecycleStateDiscovery
    {
        public Exception? Exception { get; set; }
        public bool HonorCancellation { get; set; }
        public int CallCount { get; private set; }
        public CancellationToken CancellationToken { get; private set; }

        public Task<IReadOnlyList<DeveloperLifecyclePersistedState>> ListAsync(
            CancellationToken cancellationToken = default)
        {
            calls.Add("discover");
            CallCount++;
            CancellationToken = cancellationToken;
            if (HonorCancellation)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            return Exception is null
                ? Task.FromResult(states())
                : Task.FromException<IReadOnlyList<DeveloperLifecyclePersistedState>>(Exception);
        }
    }
}
