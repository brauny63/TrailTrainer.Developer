using TrailTrainer.Developer.Core;
using TrailTrainer.Developer.Tasks;

namespace TrailTrainer.Developer.Tests;

public sealed class DelayedAutomaticResumeExecutorTests
{
    [Fact]
    public void Request_ValidatesAndPreservesValues()
    {
        var runRequest = RunRequest();
        var exactDelay = TimeSpan.FromMinutes(7);

        Assert.Throws<ArgumentNullException>(() => new DelayedAutomaticResumeRequest(null!, exactDelay));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DelayedAutomaticResumeRequest(runRequest, TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DelayedAutomaticResumeRequest(runRequest, TimeSpan.FromTicks(-1)));
        var request = new DelayedAutomaticResumeRequest(runRequest, exactDelay);

        Assert.Same(runRequest, request.RunRequest);
        Assert.Equal(exactDelay, request.ResumeDelay);
    }

    [Fact]
    public void Result_RejectsNullInitialRunAndUnsupportedState()
    {
        Assert.Throws<ArgumentNullException>(() => new DelayedAutomaticResumeResult(
            DelayedAutomaticResumeState.Finished, null!, null, false));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DelayedAutomaticResumeResult(
            (DelayedAutomaticResumeState)99,
            RunResult(AutomaticResumeRunState.Finished),
            null,
            false));
    }

    [Theory]
    [InlineData(DelayedAutomaticResumeState.Finished, AutomaticResumeRunState.Finished)]
    [InlineData(DelayedAutomaticResumeState.Failed, AutomaticResumeRunState.Failed)]
    [InlineData(DelayedAutomaticResumeState.ImmediateWorkRemaining, AutomaticResumeRunState.LimitReached)]
    [InlineData(DelayedAutomaticResumeState.ResumeLater, AutomaticResumeRunState.ResumeLater)]
    public void Result_NonDelayedStatesEnforceInvariantsAndPreserveInitialIdentity(
        DelayedAutomaticResumeState state,
        AutomaticResumeRunState initialState)
    {
        var initial = RunResult(initialState);
        var result = new DelayedAutomaticResumeResult(state, initial, null, false);

        Assert.Same(initial, result.InitialRun);
        Assert.Null(result.DelayedRun);
        Assert.False(result.DelayExecuted);
        Assert.Throws<ArgumentException>(() => new DelayedAutomaticResumeResult(
            state, initial, RunResult(AutomaticResumeRunState.Finished), false));
        Assert.Throws<ArgumentException>(() => new DelayedAutomaticResumeResult(
            state, initial, null, true));
    }

    [Theory]
    [InlineData(AutomaticResumeRunState.Finished)]
    [InlineData(AutomaticResumeRunState.ResumeLater)]
    [InlineData(AutomaticResumeRunState.Failed)]
    [InlineData(AutomaticResumeRunState.LimitReached)]
    public void Result_DelayedRunCompletedAllowsAnyDelayedRunStateAndPreservesIdentities(
        AutomaticResumeRunState delayedState)
    {
        var initial = RunResult(AutomaticResumeRunState.ResumeLater);
        var delayed = RunResult(delayedState);
        var result = new DelayedAutomaticResumeResult(
            DelayedAutomaticResumeState.DelayedRunCompleted, initial, delayed, true);

        Assert.Same(initial, result.InitialRun);
        Assert.Same(delayed, result.DelayedRun);
        Assert.True(result.DelayExecuted);
        Assert.Throws<ArgumentException>(() => new DelayedAutomaticResumeResult(
            DelayedAutomaticResumeState.DelayedRunCompleted, initial, null, true));
        Assert.Throws<ArgumentException>(() => new DelayedAutomaticResumeResult(
            DelayedAutomaticResumeState.DelayedRunCompleted, initial, delayed, false));
        Assert.Throws<ArgumentException>(() => new DelayedAutomaticResumeResult(
            DelayedAutomaticResumeState.DelayedRunCompleted,
            RunResult(AutomaticResumeRunState.Finished),
            delayed,
            true));
    }

    [Theory]
    [InlineData(AutomaticResumeRunState.Finished, DelayedAutomaticResumeState.Finished)]
    [InlineData(AutomaticResumeRunState.Failed, DelayedAutomaticResumeState.Failed)]
    [InlineData(AutomaticResumeRunState.LimitReached, DelayedAutomaticResumeState.ImmediateWorkRemaining)]
    public async Task ExecuteAsync_InitialTerminalStateRunsOnceWithoutDelay(
        AutomaticResumeRunState initialState,
        DelayedAutomaticResumeState expectedState)
    {
        using var source = new CancellationTokenSource();
        var initial = RunResult(initialState);
        var fixture = new Fixture([initial]);
        var request = Request();

        var result = await fixture.Service.ExecuteAsync(request, source.Token);

        Assert.Equal(expectedState, result.State);
        Assert.Same(initial, result.InitialRun);
        Assert.Null(result.DelayedRun);
        Assert.False(result.DelayExecuted);
        Assert.Equal(["run"], fixture.Calls);
        Assert.Equal(1, fixture.Orchestrator.CallCount);
        Assert.Equal(0, fixture.Delay.CallCount);
        Assert.Same(request.RunRequest, Assert.Single(fixture.Orchestrator.Requests));
        Assert.Equal(source.Token, Assert.Single(fixture.Orchestrator.Tokens));
    }

    [Theory]
    [InlineData(AutomaticResumeRunState.Finished)]
    [InlineData(AutomaticResumeRunState.ResumeLater)]
    [InlineData(AutomaticResumeRunState.Failed)]
    [InlineData(AutomaticResumeRunState.LimitReached)]
    public async Task ExecuteAsync_ResumeLaterDelaysOnceThenPreservesSecondResultWithoutFurtherAction(
        AutomaticResumeRunState delayedState)
    {
        using var source = new CancellationTokenSource();
        var initial = RunResult(AutomaticResumeRunState.ResumeLater);
        var delayed = RunResult(delayedState);
        var fixture = new Fixture([initial, delayed]);
        var request = Request();

        var result = await fixture.Service.ExecuteAsync(request, source.Token);

        Assert.Equal(DelayedAutomaticResumeState.DelayedRunCompleted, result.State);
        Assert.True(result.DelayExecuted);
        Assert.Same(initial, result.InitialRun);
        Assert.Same(delayed, result.DelayedRun);
        Assert.Equal(["run", "delay", "run"], fixture.Calls);
        Assert.Equal(2, fixture.Orchestrator.CallCount);
        Assert.Equal(1, fixture.Delay.CallCount);
        Assert.All(fixture.Orchestrator.Requests, actual => Assert.Same(request.RunRequest, actual));
        Assert.All(fixture.Orchestrator.Tokens, actual => Assert.Equal(source.Token, actual));
        Assert.Equal(request.ResumeDelay, fixture.Delay.Delay);
        Assert.Equal(source.Token, fixture.Delay.Token);
    }

    [Fact]
    public async Task ExecuteAsync_NullRequestRejectedBeforeDependencies()
    {
        var fixture = new Fixture([]);

        await Assert.ThrowsAsync<ArgumentNullException>(() => fixture.Service.ExecuteAsync(null!));

        Assert.Empty(fixture.Calls);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task ExecuteAsync_RunExceptionPropagatesWithoutRetryOrLaterOperation(int failingCall)
    {
        var expected = new IOException("run failed");
        var fixture = new Fixture([
            RunResult(AutomaticResumeRunState.ResumeLater),
            RunResult(AutomaticResumeRunState.Finished)]);
        fixture.Orchestrator.Exception = expected;
        fixture.Orchestrator.ExceptionCall = failingCall;

        var exception = await Assert.ThrowsAsync<IOException>(() => fixture.Service.ExecuteAsync(Request()));

        Assert.Same(expected, exception);
        Assert.Equal(failingCall, fixture.Orchestrator.CallCount);
        Assert.Equal(failingCall == 1 ? 0 : 1, fixture.Delay.CallCount);
    }

    [Fact]
    public async Task ExecuteAsync_DelayExceptionPropagatesAndPreventsSecondRun()
    {
        var expected = new InvalidOperationException("delay failed");
        var fixture = new Fixture([RunResult(AutomaticResumeRunState.ResumeLater)]);
        fixture.Delay.Exception = expected;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Service.ExecuteAsync(Request()));

        Assert.Same(expected, exception);
        Assert.Equal(["run", "delay"], fixture.Calls);
        Assert.Equal(1, fixture.Orchestrator.CallCount);
        Assert.Equal(1, fixture.Delay.CallCount);
    }

    [Fact]
    public async Task ExecuteAsync_PreCancelledFirstRunPreventsDelay()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        var fixture = new Fixture([]);
        fixture.Orchestrator.HonorCancellation = true;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            fixture.Service.ExecuteAsync(Request(), source.Token));

        Assert.Equal(["run"], fixture.Calls);
        Assert.Equal(0, fixture.Delay.CallCount);
    }

    [Fact]
    public async Task ExecuteAsync_CancellationDuringDelayPreventsSecondRun()
    {
        using var source = new CancellationTokenSource();
        var fixture = new Fixture([RunResult(AutomaticResumeRunState.ResumeLater)]);
        fixture.Delay.CancelSource = source;
        fixture.Delay.HonorCancellation = true;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            fixture.Service.ExecuteAsync(Request(), source.Token));

        Assert.Equal(["run", "delay"], fixture.Calls);
        Assert.Equal(1, fixture.Orchestrator.CallCount);
    }

    [Fact]
    public async Task ExecuteAsync_CancellationDuringSecondRunPropagatesWithoutRetry()
    {
        using var source = new CancellationTokenSource();
        var fixture = new Fixture([
            RunResult(AutomaticResumeRunState.ResumeLater),
            RunResult(AutomaticResumeRunState.Finished)]);
        fixture.Delay.CancelSource = source;
        fixture.Orchestrator.HonorCancellation = true;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            fixture.Service.ExecuteAsync(Request(), source.Token));

        Assert.Equal(["run", "delay", "run"], fixture.Calls);
        Assert.Equal(2, fixture.Orchestrator.CallCount);
    }

    [Fact]
    public async Task SystemAsyncDelay_CompletesAndHonorsCancellation()
    {
        var delay = new SystemAsyncDelay();

        await delay.DelayAsync(TimeSpan.FromMilliseconds(1));
        using var source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            delay.DelayAsync(TimeSpan.FromMinutes(1), source.Token));
    }

    [Fact]
    public void Executor_DependsExactlyOnOrchestratorAndDelay()
    {
        var parameters = Assert.Single(typeof(DelayedAutomaticResumeExecutor).GetConstructors()).GetParameters();

        Assert.Equal(
            [typeof(IAutomaticResumeRunOrchestrator), typeof(IAsyncDelay)],
            parameters.Select(parameter => parameter.ParameterType));
    }

    private static DelayedAutomaticResumeRequest Request() =>
        new(RunRequest(), TimeSpan.FromMinutes(5));

    private static AutomaticResumeRunRequest RunRequest() => new(
        new AutomaticResumeBatchRunRequest(
            new AutomaticResumeBatchStepRequest(PullRequestMergeMethod.Squash, "title", "message", true),
            2),
        2);

    private static AutomaticResumeRunResult RunResult(AutomaticResumeRunState state)
    {
        var decisionState = state switch
        {
            AutomaticResumeRunState.Finished => AutomaticResumeSchedulingDecisionState.Finished,
            AutomaticResumeRunState.ResumeLater => AutomaticResumeSchedulingDecisionState.ResumeLater,
            AutomaticResumeRunState.Failed => AutomaticResumeSchedulingDecisionState.StopFailed,
            _ => AutomaticResumeSchedulingDecisionState.ContinueImmediately
        };
        var batchState = state switch
        {
            AutomaticResumeRunState.Finished => AutomaticResumeBatchRunState.Completed,
            AutomaticResumeRunState.ResumeLater => AutomaticResumeBatchRunState.Pending,
            AutomaticResumeRunState.Failed => AutomaticResumeBatchRunState.Failed,
            _ => AutomaticResumeBatchRunState.LimitReached
        };
        var batch = BatchRun(batchState);
        var decision = decisionState switch
        {
            AutomaticResumeSchedulingDecisionState.Finished => new AutomaticResumeSchedulingDecision(
                decisionState, batch, false, false),
            AutomaticResumeSchedulingDecisionState.ResumeLater => new AutomaticResumeSchedulingDecision(
                decisionState, batch, true, false),
            AutomaticResumeSchedulingDecisionState.StopFailed => new AutomaticResumeSchedulingDecision(
                decisionState, batch, false, false),
            _ => new AutomaticResumeSchedulingDecision(decisionState, batch, true, true)
        };
        return new AutomaticResumeRunResult(
            state,
            [batch],
            [decision],
            decision.ShouldRunAgain,
            decision.Immediate);
    }

    private static AutomaticResumeBatchRunResult BatchRun(AutomaticResumeBatchRunState state)
    {
        var stepState = state switch
        {
            AutomaticResumeBatchRunState.Empty => AutomaticResumeBatchStepState.Empty,
            AutomaticResumeBatchRunState.Pending => AutomaticResumeBatchStepState.Pending,
            AutomaticResumeBatchRunState.Failed => AutomaticResumeBatchStepState.Failed,
            _ => AutomaticResumeBatchStepState.Completed
        };
        var moreWork = state == AutomaticResumeBatchRunState.LimitReached;
        return new AutomaticResumeBatchRunResult(state, [BatchStep(stepState, moreWork)], moreWork);
    }

    private static AutomaticResumeBatchStepResult BatchStep(
        AutomaticResumeBatchStepState state,
        bool moreWork)
    {
        var resumeState = state switch
        {
            AutomaticResumeBatchStepState.Empty => AutomaticPersistedLifecycleResumeState.NotFound,
            AutomaticResumeBatchStepState.Pending => AutomaticPersistedLifecycleResumeState.Pending,
            AutomaticResumeBatchStepState.Failed => AutomaticPersistedLifecycleResumeState.Failed,
            _ => AutomaticPersistedLifecycleResumeState.Completed
        };
        return new AutomaticResumeBatchStepResult(state, PersistedResume(resumeState), moreWork);
    }

    private static AutomaticPersistedLifecycleResumeResult PersistedResume(
        AutomaticPersistedLifecycleResumeState state)
    {
        if (state == AutomaticPersistedLifecycleResumeState.NotFound)
        {
            return new AutomaticPersistedLifecycleResumeResult(
                state, new AutomaticResumeCandidateResult(AutomaticResumeCandidateState.NotFound));
        }

        var persisted = PersistedState();
        var candidate = new AutomaticResumeCandidateResult(
            AutomaticResumeCandidateState.Found,
            persisted,
            new PersistedLifecycleResumeTarget(persisted.TaskId, persisted));
        var persistedState = state switch
        {
            AutomaticPersistedLifecycleResumeState.Pending => PersistedDeveloperLifecycleResumeState.Pending,
            AutomaticPersistedLifecycleResumeState.Failed => PersistedDeveloperLifecycleResumeState.Failed,
            _ => PersistedDeveloperLifecycleResumeState.Completed
        };
        var lifecycleState = persistedState switch
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
        var status = new PullRequestStatusGateResult(30, "head", gateState, []);
        DeveloperLifecycleResumeResult lifecycle;
        if (lifecycleState == DeveloperLifecycleState.Completed)
        {
            lifecycle = new DeveloperLifecycleResumeResult(
                lifecycleState,
                persisted.ResumeContext,
                status,
                new PullRequestGatedMergeResult(
                    status,
                    new PullRequestMergeResult(30, true, "merge", PullRequestMergeMethod.Squash)),
                new PostMergeCleanupResult("repository", "main", "feature/delayed", true, true));
        }
        else
        {
            lifecycle = new DeveloperLifecycleResumeResult(lifecycleState, persisted.ResumeContext, status);
        }

        return new AutomaticPersistedLifecycleResumeResult(
            state,
            candidate,
            new PersistedDeveloperLifecycleResumeResult(
                persistedState, persisted.TaskId, persisted, lifecycle));
    }

    private static DeveloperLifecyclePersistedState PersistedState() => new(
        "DEV-0030",
        null,
        new DeveloperLifecycleResumeContext(
            "repository",
            new GitHubRepositoryIdentity("owner", "repository"),
            30,
            "feature/delayed",
            "main",
            "origin"),
        new DateTimeOffset(2026, 8, 15, 12, 0, 0, TimeSpan.Zero));

    private sealed class Fixture
    {
        public Fixture(IReadOnlyList<AutomaticResumeRunResult> results)
        {
            Orchestrator = new FakeOrchestrator(Calls, results);
            Delay = new FakeDelay(Calls);
            Service = new DelayedAutomaticResumeExecutor(Orchestrator, Delay);
        }

        public List<string> Calls { get; } = [];
        public FakeOrchestrator Orchestrator { get; }
        public FakeDelay Delay { get; }
        public DelayedAutomaticResumeExecutor Service { get; }
    }

    private sealed class FakeOrchestrator(
        IList<string> calls,
        IReadOnlyList<AutomaticResumeRunResult> results) : IAutomaticResumeRunOrchestrator
    {
        public Exception? Exception { get; set; }
        public int? ExceptionCall { get; set; }
        public bool HonorCancellation { get; set; }
        public int CallCount { get; private set; }
        public List<AutomaticResumeRunRequest> Requests { get; } = [];
        public List<CancellationToken> Tokens { get; } = [];

        public Task<AutomaticResumeRunResult> RunAsync(
            AutomaticResumeRunRequest request,
            CancellationToken cancellationToken = default)
        {
            calls.Add("run");
            CallCount++;
            Requests.Add(request);
            Tokens.Add(cancellationToken);
            if (HonorCancellation)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            return ExceptionCall == CallCount
                ? Task.FromException<AutomaticResumeRunResult>(Exception!)
                : Task.FromResult(results[CallCount - 1]);
        }
    }

    private sealed class FakeDelay(IList<string> calls) : IAsyncDelay
    {
        public Exception? Exception { get; set; }
        public bool HonorCancellation { get; set; }
        public CancellationTokenSource? CancelSource { get; set; }
        public int CallCount { get; private set; }
        public TimeSpan Delay { get; private set; }
        public CancellationToken Token { get; private set; }

        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken = default)
        {
            calls.Add("delay");
            CallCount++;
            Delay = delay;
            Token = cancellationToken;
            CancelSource?.Cancel();
            if (HonorCancellation)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            return Exception is null ? Task.CompletedTask : Task.FromException(Exception);
        }
    }
}
