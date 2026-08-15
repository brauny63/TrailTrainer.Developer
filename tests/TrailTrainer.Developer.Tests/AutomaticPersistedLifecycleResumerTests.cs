using TrailTrainer.Developer.Core;
using TrailTrainer.Developer.Tasks;

namespace TrailTrainer.Developer.Tests;

public sealed class AutomaticPersistedLifecycleResumerTests
{
    [Fact]
    public void Request_PreservesAllValuesExactlyAndMatchesDev0020MergeValidation()
    {
        var request = new AutomaticPersistedLifecycleResumeRequest(
            (PullRequestMergeMethod)99, "Exact Title", "Exact Message", true);
        var nullOptionals = new AutomaticPersistedLifecycleResumeRequest(
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
    public void Result_UnsupportedStateAndNullCandidateRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new AutomaticPersistedLifecycleResumeResult(
            AutomaticPersistedLifecycleResumeState.NotFound, null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AutomaticPersistedLifecycleResumeResult(
            (AutomaticPersistedLifecycleResumeState)99, NotFoundCandidate()));
    }

    [Fact]
    public void Result_NotFoundInvariantsAndIdentityEnforced()
    {
        var notFound = NotFoundCandidate();

        Assert.Throws<ArgumentException>(() => new AutomaticPersistedLifecycleResumeResult(
            AutomaticPersistedLifecycleResumeState.NotFound, FoundCandidate()));
        Assert.Throws<ArgumentException>(() => new AutomaticPersistedLifecycleResumeResult(
            AutomaticPersistedLifecycleResumeState.NotFound,
            notFound,
            ResumeResult(PersistedDeveloperLifecycleResumeState.Pending)));
        var result = new AutomaticPersistedLifecycleResumeResult(
            AutomaticPersistedLifecycleResumeState.NotFound, notFound);

        Assert.Same(notFound, result.Candidate);
        Assert.Null(result.Resume);
    }

    [Theory]
    [InlineData(AutomaticPersistedLifecycleResumeState.Pending, PersistedDeveloperLifecycleResumeState.Pending)]
    [InlineData(AutomaticPersistedLifecycleResumeState.Failed, PersistedDeveloperLifecycleResumeState.Failed)]
    [InlineData(AutomaticPersistedLifecycleResumeState.Completed, PersistedDeveloperLifecycleResumeState.Completed)]
    public void Result_FoundOutcomeInvariantsAndIdentitiesEnforced(
        AutomaticPersistedLifecycleResumeState state,
        PersistedDeveloperLifecycleResumeState resumeState)
    {
        var found = FoundCandidate();
        var resume = ResumeResult(resumeState);
        var wrongResume = ResumeResult(resumeState == PersistedDeveloperLifecycleResumeState.Pending
            ? PersistedDeveloperLifecycleResumeState.Failed
            : PersistedDeveloperLifecycleResumeState.Pending);

        Assert.Throws<ArgumentException>(() => new AutomaticPersistedLifecycleResumeResult(
            state, NotFoundCandidate(), resume));
        Assert.Throws<ArgumentException>(() => new AutomaticPersistedLifecycleResumeResult(state, found));
        Assert.Throws<ArgumentException>(() => new AutomaticPersistedLifecycleResumeResult(
            state, found, wrongResume));
        var result = new AutomaticPersistedLifecycleResumeResult(state, found, resume);

        Assert.Same(found, result.Candidate);
        Assert.Same(resume, result.Resume);
    }

    [Fact]
    public async Task ResumeAsync_NullRequestRejectedBeforeSelection()
    {
        var fixture = new Fixture();

        await Assert.ThrowsAsync<ArgumentNullException>(() => fixture.Service.ResumeAsync(null!));

        Assert.Empty(fixture.Calls);
    }

    [Fact]
    public async Task ResumeAsync_NotFoundSelectsOnceWithExactTokenAndDoesNotResume()
    {
        using var source = new CancellationTokenSource();
        var fixture = new Fixture { CandidateResult = NotFoundCandidate() };

        var result = await fixture.Service.ResumeAsync(Request(), source.Token);

        Assert.Equal(AutomaticPersistedLifecycleResumeState.NotFound, result.State);
        Assert.Same(fixture.CandidateResult, result.Candidate);
        Assert.Null(result.Resume);
        Assert.Equal(["select"], fixture.Calls);
        Assert.Equal(1, fixture.Selector.CallCount);
        Assert.Equal(source.Token, fixture.Selector.CancellationToken);
        Assert.Equal(0, fixture.PersistedLifecycle.ResumeCount);
    }

    [Fact]
    public async Task ResumeAsync_CandidateFailurePropagatesAndPreventsResumeWithoutRetry()
    {
        var fixture = new Fixture();
        var expected = new InvalidDataException("candidate selection failed");
        fixture.Selector.Exception = expected;

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            fixture.Service.ResumeAsync(Request()));

        Assert.Same(expected, exception);
        Assert.Equal(["select"], fixture.Calls);
        Assert.Equal(1, fixture.Selector.CallCount);
        Assert.Equal(0, fixture.PersistedLifecycle.ResumeCount);
    }

    [Fact]
    public async Task ResumeAsync_UsesExactTargetTaskAndDelegatesOptionsAndTokenOnceAfterSelection()
    {
        using var source = new CancellationTokenSource();
        var selected = State("Exact-Target-Task");
        var fixture = new Fixture
        {
            CandidateResult = FoundCandidate(selected),
            ResumeResult = ResumeResult(PersistedDeveloperLifecycleResumeState.Pending, selected)
        };
        var request = Request();

        await fixture.Service.ResumeAsync(request, source.Token);

        Assert.Equal(["select", "resume"], fixture.Calls);
        Assert.Equal(1, fixture.Selector.CallCount);
        Assert.Equal(1, fixture.PersistedLifecycle.ResumeCount);
        var delegated = Assert.IsType<PersistedDeveloperLifecycleResumeRequest>(
            fixture.PersistedLifecycle.Request);
        Assert.Equal("Exact-Target-Task", delegated.TaskId);
        Assert.Equal(request.MergeMethod, delegated.MergeMethod);
        Assert.Equal(request.MergeCommitTitle, delegated.MergeCommitTitle);
        Assert.Equal(request.MergeCommitMessage, delegated.MergeCommitMessage);
        Assert.Equal(request.DeleteRemoteBranch, delegated.DeleteRemoteBranch);
        Assert.Equal(source.Token, fixture.PersistedLifecycle.CancellationToken);
    }

    [Theory]
    [InlineData(PersistedDeveloperLifecycleResumeState.Pending, AutomaticPersistedLifecycleResumeState.Pending)]
    [InlineData(PersistedDeveloperLifecycleResumeState.Failed, AutomaticPersistedLifecycleResumeState.Failed)]
    [InlineData(PersistedDeveloperLifecycleResumeState.Completed, AutomaticPersistedLifecycleResumeState.Completed)]
    public async Task ResumeAsync_MapsOutcomesAndPreservesExactNestedResults(
        PersistedDeveloperLifecycleResumeState resumeState,
        AutomaticPersistedLifecycleResumeState expectedState)
    {
        var selected = State("DEV-0025");
        var fixture = new Fixture
        {
            CandidateResult = FoundCandidate(selected),
            ResumeResult = ResumeResult(resumeState, selected)
        };

        var result = await fixture.Service.ResumeAsync(Request());

        Assert.Equal(expectedState, result.State);
        Assert.Same(fixture.CandidateResult, result.Candidate);
        Assert.Same(fixture.ResumeResult, result.Resume);
        Assert.Equal(["select", "resume"], fixture.Calls);
    }

    [Fact]
    public async Task ResumeAsync_NotFoundAfterFoundFailsAsRaceWithoutReselectOrRetry()
    {
        var fixture = new Fixture
        {
            CandidateResult = FoundCandidate(),
            ResumeResult = ResumeResult(PersistedDeveloperLifecycleResumeState.NotFound)
        };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Service.ResumeAsync(Request()));

        Assert.Contains("not found", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(["select", "resume"], fixture.Calls);
        Assert.Equal(1, fixture.Selector.CallCount);
        Assert.Equal(1, fixture.PersistedLifecycle.ResumeCount);
    }

    [Fact]
    public async Task ResumeAsync_Dev0020FailurePropagatesWithoutRetry()
    {
        var fixture = new Fixture { CandidateResult = FoundCandidate() };
        var expected = new IOException("resume failed");
        fixture.PersistedLifecycle.Exception = expected;

        var exception = await Assert.ThrowsAsync<IOException>(() => fixture.Service.ResumeAsync(Request()));

        Assert.Same(expected, exception);
        Assert.Equal(["select", "resume"], fixture.Calls);
        Assert.Equal(1, fixture.PersistedLifecycle.ResumeCount);
    }

    [Fact]
    public async Task ResumeAsync_PreCancelledSelectionPreventsResume()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        var fixture = new Fixture();
        fixture.Selector.HonorCancellation = true;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            fixture.Service.ResumeAsync(Request(), source.Token));

        Assert.Equal(["select"], fixture.Calls);
        Assert.Equal(0, fixture.PersistedLifecycle.ResumeCount);
    }

    [Fact]
    public async Task ResumeAsync_Dev0020CancellationPropagatesWithoutRetry()
    {
        var fixture = new Fixture { CandidateResult = FoundCandidate() };
        fixture.PersistedLifecycle.Exception = new OperationCanceledException();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Service.ResumeAsync(Request()));

        Assert.Equal(["select", "resume"], fixture.Calls);
        Assert.Equal(1, fixture.PersistedLifecycle.ResumeCount);
    }

    private static AutomaticPersistedLifecycleResumeRequest Request() => new(
        PullRequestMergeMethod.Squash, "Exact resume title", "Exact resume message", true);

    private static AutomaticResumeCandidateResult NotFoundCandidate() =>
        new(AutomaticResumeCandidateState.NotFound);

    private static AutomaticResumeCandidateResult FoundCandidate(
        DeveloperLifecyclePersistedState? state = null)
    {
        state ??= State("DEV-0025");
        return new AutomaticResumeCandidateResult(
            AutomaticResumeCandidateState.Found,
            state,
            new PersistedLifecycleResumeTarget(state.TaskId, state));
    }

    private static DeveloperLifecyclePersistedState State(string taskId) => new(
        taskId,
        null,
        new DeveloperLifecycleResumeContext(
            "repository",
            new GitHubRepositoryIdentity("owner", "repository"),
            25,
            "feature/automatic-resume",
            "main",
            "origin"),
        new DateTimeOffset(2026, 8, 15, 12, 0, 0, TimeSpan.Zero));

    private static PersistedDeveloperLifecycleResumeResult ResumeResult(
        PersistedDeveloperLifecycleResumeState state,
        DeveloperLifecyclePersistedState? persistedState = null)
    {
        if (state == PersistedDeveloperLifecycleResumeState.NotFound)
        {
            return new PersistedDeveloperLifecycleResumeResult(state, "DEV-0025");
        }

        persistedState ??= State("DEV-0025");
        return new PersistedDeveloperLifecycleResumeResult(
            state,
            persistedState.TaskId,
            persistedState,
            LifecycleResume(state, persistedState.ResumeContext));
    }

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
        var status = new PullRequestStatusGateResult(25, "head-sha", gateState, []);
        if (lifecycleState != DeveloperLifecycleState.Completed)
        {
            return new DeveloperLifecycleResumeResult(lifecycleState, context, status);
        }

        var merge = new PullRequestGatedMergeResult(
            status,
            new PullRequestMergeResult(25, true, "merge-sha", PullRequestMergeMethod.Squash));
        var cleanup = new PostMergeCleanupResult(
            "repository", "main", "feature/automatic-resume", true, true);
        return new DeveloperLifecycleResumeResult(lifecycleState, context, status, merge, cleanup);
    }

    private sealed class Fixture
    {
        public Fixture()
        {
            Selector = new FakeSelector(Calls, () => CandidateResult);
            PersistedLifecycle = new FakePersistedLifecycle(Calls, () => ResumeResult);
            Service = new AutomaticPersistedLifecycleResumer(Selector, PersistedLifecycle);
        }

        public List<string> Calls { get; } = [];
        public AutomaticResumeCandidateResult CandidateResult { get; set; } = NotFoundCandidate();
        public PersistedDeveloperLifecycleResumeResult ResumeResult { get; set; } =
            AutomaticPersistedLifecycleResumerTests.ResumeResult(PersistedDeveloperLifecycleResumeState.Pending);
        public FakeSelector Selector { get; }
        public FakePersistedLifecycle PersistedLifecycle { get; }
        public AutomaticPersistedLifecycleResumer Service { get; }
    }

    private sealed class FakeSelector(
        IList<string> calls,
        Func<AutomaticResumeCandidateResult> result) : IAutomaticResumeCandidateSelector
    {
        public Exception? Exception { get; set; }
        public bool HonorCancellation { get; set; }
        public int CallCount { get; private set; }
        public CancellationToken CancellationToken { get; private set; }

        public Task<AutomaticResumeCandidateResult> SelectAsync(
            CancellationToken cancellationToken = default)
        {
            calls.Add("select");
            CallCount++;
            CancellationToken = cancellationToken;
            if (HonorCancellation)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            return Exception is null
                ? Task.FromResult(result())
                : Task.FromException<AutomaticResumeCandidateResult>(Exception);
        }
    }

    private sealed class FakePersistedLifecycle(
        IList<string> calls,
        Func<PersistedDeveloperLifecycleResumeResult> result) : IPersistedDeveloperLifecycle
    {
        public Exception? Exception { get; set; }
        public int ResumeCount { get; private set; }
        public PersistedDeveloperLifecycleResumeRequest? Request { get; private set; }
        public CancellationToken CancellationToken { get; private set; }

        public Task<PersistedDeveloperLifecycleStartResult> StartAsync(
            PersistedDeveloperLifecycleStartRequest request,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Automatic resume must not invoke Start.");

        public Task<PersistedDeveloperLifecycleResumeResult> ResumeAsync(
            PersistedDeveloperLifecycleResumeRequest request,
            CancellationToken cancellationToken = default)
        {
            calls.Add("resume");
            ResumeCount++;
            Request = request;
            CancellationToken = cancellationToken;
            return Exception is null
                ? Task.FromResult(result())
                : Task.FromException<PersistedDeveloperLifecycleResumeResult>(Exception);
        }
    }
}
