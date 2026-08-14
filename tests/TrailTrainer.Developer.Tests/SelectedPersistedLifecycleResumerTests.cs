using TrailTrainer.Developer.Core;
using TrailTrainer.Developer.Tasks;

namespace TrailTrainer.Developer.Tests;

public sealed class SelectedPersistedLifecycleResumerTests
{
    [Fact]
    public void Request_RequiresSelectionAndPreservesResumeOptionsExactly()
    {
        Assert.Throws<ArgumentNullException>(() => new SelectedPersistedLifecycleResumeRequest(
            null!, PullRequestMergeMethod.Merge, null, null, false));
        var selection = SelectionRequest();

        var request = new SelectedPersistedLifecycleResumeRequest(
            selection,
            PullRequestMergeMethod.Rebase,
            "Exact Title",
            "Exact Message",
            true);

        Assert.Same(selection, request.Selection);
        Assert.Equal(PullRequestMergeMethod.Rebase, request.MergeMethod);
        Assert.Equal("Exact Title", request.MergeCommitTitle);
        Assert.Equal("Exact Message", request.MergeCommitMessage);
        Assert.True(request.DeleteRemoteBranch);
    }

    [Fact]
    public void Request_PreservesNullOptionalValuesAndMatchesDev0020EnumValidation()
    {
        var request = new SelectedPersistedLifecycleResumeRequest(
            SelectionRequest(),
            (PullRequestMergeMethod)99,
            null,
            null,
            false);

        Assert.Equal((PullRequestMergeMethod)99, request.MergeMethod);
        Assert.Null(request.MergeCommitTitle);
        Assert.Null(request.MergeCommitMessage);
        Assert.False(request.DeleteRemoteBranch);
    }

    [Fact]
    public void Result_UnsupportedStateRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SelectedPersistedLifecycleResumeResult(
            (SelectedPersistedLifecycleResumeState)99,
            NotFoundSelection()));
    }

    [Fact]
    public void Result_NotFoundInvariantsAndIdentityEnforced()
    {
        var notFound = NotFoundSelection();
        var found = FoundSelection();

        Assert.Throws<ArgumentException>(() => new SelectedPersistedLifecycleResumeResult(
            SelectedPersistedLifecycleResumeState.NotFound,
            found));
        Assert.Throws<ArgumentException>(() => new SelectedPersistedLifecycleResumeResult(
            SelectedPersistedLifecycleResumeState.NotFound,
            notFound,
            ResumeResult(PersistedDeveloperLifecycleResumeState.Pending)));
        var result = new SelectedPersistedLifecycleResumeResult(
            SelectedPersistedLifecycleResumeState.NotFound,
            notFound);

        Assert.Same(notFound, result.Selection);
        Assert.Null(result.Resume);
    }

    [Theory]
    [InlineData(SelectedPersistedLifecycleResumeState.Pending, PersistedDeveloperLifecycleResumeState.Pending)]
    [InlineData(SelectedPersistedLifecycleResumeState.Failed, PersistedDeveloperLifecycleResumeState.Failed)]
    [InlineData(SelectedPersistedLifecycleResumeState.Completed, PersistedDeveloperLifecycleResumeState.Completed)]
    public void Result_NonNotFoundInvariantsAndIdentitiesEnforced(
        SelectedPersistedLifecycleResumeState state,
        PersistedDeveloperLifecycleResumeState resumeState)
    {
        var found = FoundSelection();
        var notFound = NotFoundSelection();
        var resume = ResumeResult(resumeState);
        var wrongResume = ResumeResult(resumeState == PersistedDeveloperLifecycleResumeState.Pending
            ? PersistedDeveloperLifecycleResumeState.Failed
            : PersistedDeveloperLifecycleResumeState.Pending);

        Assert.Throws<ArgumentException>(() => new SelectedPersistedLifecycleResumeResult(
            state, notFound, resume));
        Assert.Throws<ArgumentException>(() => new SelectedPersistedLifecycleResumeResult(
            state, found));
        Assert.Throws<ArgumentException>(() => new SelectedPersistedLifecycleResumeResult(
            state, found, wrongResume));
        var result = new SelectedPersistedLifecycleResumeResult(state, found, resume);

        Assert.Same(found, result.Selection);
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
    public async Task ResumeAsync_SelectionNotFoundReturnsExactSelectionWithoutResume()
    {
        using var source = new CancellationTokenSource();
        var fixture = new Fixture { SelectionResult = NotFoundSelection() };
        var request = Request();

        var result = await fixture.Service.ResumeAsync(request, source.Token);

        Assert.Equal(SelectedPersistedLifecycleResumeState.NotFound, result.State);
        Assert.Same(fixture.SelectionResult, result.Selection);
        Assert.Null(result.Resume);
        Assert.Equal(["select"], fixture.Calls);
        Assert.Equal(1, fixture.Selector.CallCount);
        Assert.Same(request.Selection, fixture.Selector.Request);
        Assert.Equal(source.Token, fixture.Selector.CancellationToken);
        Assert.Equal(0, fixture.PersistedLifecycle.ResumeCount);
    }

    [Fact]
    public async Task ResumeAsync_SelectorFailurePropagatesAndPreventsResumeWithoutRetry()
    {
        var fixture = new Fixture();
        var expected = new InvalidDataException("selection failed");
        fixture.Selector.Exception = expected;

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            fixture.Service.ResumeAsync(Request()));

        Assert.Same(expected, exception);
        Assert.Equal(["select"], fixture.Calls);
        Assert.Equal(1, fixture.Selector.CallCount);
        Assert.Equal(0, fixture.PersistedLifecycle.ResumeCount);
    }

    [Fact]
    public async Task ResumeAsync_FoundConstructsExactDev0020RequestAndRunsAfterSelectionOnce()
    {
        using var source = new CancellationTokenSource();
        var selectedState = State("Selected-Exact-Task");
        var fixture = new Fixture
        {
            SelectionResult = new PersistedLifecycleSelectionResult(
                PersistedLifecycleSelectionState.Found,
                selectedState),
            ResumeResult = ResumeResult(PersistedDeveloperLifecycleResumeState.Pending, selectedState)
        };
        var request = Request();

        await fixture.Service.ResumeAsync(request, source.Token);

        Assert.Equal(["select", "resume"], fixture.Calls);
        Assert.Equal(1, fixture.Selector.CallCount);
        Assert.Equal(1, fixture.PersistedLifecycle.ResumeCount);
        var delegated = Assert.IsType<PersistedDeveloperLifecycleResumeRequest>(
            fixture.PersistedLifecycle.Request);
        Assert.Equal("Selected-Exact-Task", delegated.TaskId);
        Assert.Equal(request.MergeMethod, delegated.MergeMethod);
        Assert.Equal(request.MergeCommitTitle, delegated.MergeCommitTitle);
        Assert.Equal(request.MergeCommitMessage, delegated.MergeCommitMessage);
        Assert.Equal(request.DeleteRemoteBranch, delegated.DeleteRemoteBranch);
        Assert.Equal(source.Token, fixture.PersistedLifecycle.CancellationToken);
    }

    [Theory]
    [InlineData(PersistedDeveloperLifecycleResumeState.Pending, SelectedPersistedLifecycleResumeState.Pending)]
    [InlineData(PersistedDeveloperLifecycleResumeState.Failed, SelectedPersistedLifecycleResumeState.Failed)]
    [InlineData(PersistedDeveloperLifecycleResumeState.Completed, SelectedPersistedLifecycleResumeState.Completed)]
    public async Task ResumeAsync_MapsResumeOutcomesAndPreservesExactResults(
        PersistedDeveloperLifecycleResumeState resumeState,
        SelectedPersistedLifecycleResumeState expectedState)
    {
        var selected = State("DEV-0023");
        var fixture = new Fixture
        {
            SelectionResult = new PersistedLifecycleSelectionResult(
                PersistedLifecycleSelectionState.Found,
                selected),
            ResumeResult = ResumeResult(resumeState, selected)
        };

        var result = await fixture.Service.ResumeAsync(Request());

        Assert.Equal(expectedState, result.State);
        Assert.Same(fixture.SelectionResult, result.Selection);
        Assert.Same(fixture.ResumeResult, result.Resume);
        Assert.Equal(["select", "resume"], fixture.Calls);
    }

    [Fact]
    public async Task ResumeAsync_NotFoundAfterFoundSelectionThrowsRaceWithoutRetry()
    {
        var fixture = new Fixture
        {
            SelectionResult = FoundSelection(),
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
        var fixture = new Fixture { SelectionResult = FoundSelection() };
        var expected = new InvalidOperationException("resume failed");
        fixture.PersistedLifecycle.Exception = expected;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Service.ResumeAsync(Request()));

        Assert.Same(expected, exception);
        Assert.Equal(["select", "resume"], fixture.Calls);
        Assert.Equal(1, fixture.Selector.CallCount);
        Assert.Equal(1, fixture.PersistedLifecycle.ResumeCount);
    }

    [Fact]
    public async Task ResumeAsync_PreCancelledSelectorCancellationPreventsResume()
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
    public async Task ResumeAsync_ResumeCancellationPropagatesWithoutRetry()
    {
        var fixture = new Fixture { SelectionResult = FoundSelection() };
        fixture.PersistedLifecycle.Exception = new OperationCanceledException();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            fixture.Service.ResumeAsync(Request()));

        Assert.Equal(["select", "resume"], fixture.Calls);
        Assert.Equal(1, fixture.PersistedLifecycle.ResumeCount);
    }

    private static SelectedPersistedLifecycleResumeRequest Request() => new(
        SelectionRequest(),
        PullRequestMergeMethod.Squash,
        "Exact resume title",
        "Exact resume message",
        true);

    private static PersistedLifecycleSelectionRequest SelectionRequest() =>
        new(PersistedLifecycleSelectionMode.Oldest);

    private static PersistedLifecycleSelectionResult FoundSelection() => new(
        PersistedLifecycleSelectionState.Found,
        State("DEV-0023"));

    private static PersistedLifecycleSelectionResult NotFoundSelection() =>
        new(PersistedLifecycleSelectionState.NotFound);

    private static DeveloperLifecyclePersistedState State(string taskId) => new(
        taskId,
        null,
        new DeveloperLifecycleResumeContext(
            "repository",
            new GitHubRepositoryIdentity("owner", "repository"),
            23,
            "feature/selected-resume",
            "main",
            "origin"),
        new DateTimeOffset(2026, 8, 14, 12, 0, 0, TimeSpan.Zero));

    private static PersistedDeveloperLifecycleResumeResult ResumeResult(
        PersistedDeveloperLifecycleResumeState state,
        DeveloperLifecyclePersistedState? persistedState = null)
    {
        if (state == PersistedDeveloperLifecycleResumeState.NotFound)
        {
            return new PersistedDeveloperLifecycleResumeResult(state, "DEV-0023");
        }

        persistedState ??= State("DEV-0023");
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
        var status = new PullRequestStatusGateResult(23, "head-sha", gateState, []);
        if (lifecycleState != DeveloperLifecycleState.Completed)
        {
            return new DeveloperLifecycleResumeResult(lifecycleState, context, status);
        }

        var merge = new PullRequestGatedMergeResult(
            status,
            new PullRequestMergeResult(23, true, "merge-sha", PullRequestMergeMethod.Squash));
        var cleanup = new PostMergeCleanupResult(
            "repository", "main", "feature/selected-resume", true, true);
        return new DeveloperLifecycleResumeResult(lifecycleState, context, status, merge, cleanup);
    }

    private sealed class Fixture
    {
        public Fixture()
        {
            Selector = new FakeSelector(Calls, () => SelectionResult);
            PersistedLifecycle = new FakePersistedLifecycle(Calls, () => ResumeResult);
            Service = new SelectedPersistedLifecycleResumer(Selector, PersistedLifecycle);
        }

        public List<string> Calls { get; } = [];
        public PersistedLifecycleSelectionResult SelectionResult { get; set; } = NotFoundSelection();
        public PersistedDeveloperLifecycleResumeResult ResumeResult { get; set; } =
            SelectedPersistedLifecycleResumerTests.ResumeResult(PersistedDeveloperLifecycleResumeState.Pending);
        public FakeSelector Selector { get; }
        public FakePersistedLifecycle PersistedLifecycle { get; }
        public SelectedPersistedLifecycleResumer Service { get; }
    }

    private sealed class FakeSelector(
        IList<string> calls,
        Func<PersistedLifecycleSelectionResult> result) : IPersistedLifecycleSelector
    {
        public Exception? Exception { get; set; }
        public bool HonorCancellation { get; set; }
        public int CallCount { get; private set; }
        public PersistedLifecycleSelectionRequest? Request { get; private set; }
        public CancellationToken CancellationToken { get; private set; }

        public Task<PersistedLifecycleSelectionResult> SelectAsync(
            PersistedLifecycleSelectionRequest request,
            CancellationToken cancellationToken = default)
        {
            calls.Add("select");
            CallCount++;
            Request = request;
            CancellationToken = cancellationToken;
            if (HonorCancellation)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            return Exception is null
                ? Task.FromResult(result())
                : Task.FromException<PersistedLifecycleSelectionResult>(Exception);
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
            throw new InvalidOperationException("Selected resume must not invoke Start.");

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
