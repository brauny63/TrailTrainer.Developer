using TrailTrainer.Developer.Core;
using TrailTrainer.Developer.Tasks;

namespace TrailTrainer.Developer.Tests;

public sealed class RepeatedDelayedAutomaticResumeExecutorTests
{
    [Fact]
    public void Request_ValidatesAndPreservesValues()
    {
        var runRequest = RunRequest();
        var exactDelay = TimeSpan.FromMinutes(9);

        Assert.Throws<ArgumentNullException>(() => new RepeatedDelayedAutomaticResumeRequest(null!, exactDelay, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RepeatedDelayedAutomaticResumeRequest(runRequest, TimeSpan.Zero, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RepeatedDelayedAutomaticResumeRequest(runRequest, TimeSpan.FromTicks(-1), 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RepeatedDelayedAutomaticResumeRequest(runRequest, exactDelay, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RepeatedDelayedAutomaticResumeRequest(runRequest, exactDelay, -1));
        var request = new RepeatedDelayedAutomaticResumeRequest(runRequest, exactDelay, 1);

        Assert.Same(runRequest, request.RunRequest);
        Assert.Equal(exactDelay, request.ResumeDelay);
        Assert.Equal(1, request.MaximumRuns);
    }

    [Fact]
    public void Result_RejectsEmptyRunsInvalidDelayCountAndUnsupportedState()
    {
        var finished = RunResult(AutomaticResumeRunState.Finished);

        Assert.Throws<ArgumentException>(() => new RepeatedDelayedAutomaticResumeResult(
            RepeatedDelayedAutomaticResumeState.Finished, [], 0, false, false));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RepeatedDelayedAutomaticResumeResult(
            RepeatedDelayedAutomaticResumeState.Finished, [finished], -1, false, false));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RepeatedDelayedAutomaticResumeResult(
            RepeatedDelayedAutomaticResumeState.Finished, [finished], 1, false, false));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RepeatedDelayedAutomaticResumeResult(
            (RepeatedDelayedAutomaticResumeState)99, [finished], 0, false, false));
    }

    [Theory]
    [InlineData(RepeatedDelayedAutomaticResumeState.Finished, AutomaticResumeRunState.Finished, false, false)]
    [InlineData(RepeatedDelayedAutomaticResumeState.Failed, AutomaticResumeRunState.Failed, false, false)]
    [InlineData(RepeatedDelayedAutomaticResumeState.ImmediateWorkRemaining, AutomaticResumeRunState.LimitReached, true, true)]
    [InlineData(RepeatedDelayedAutomaticResumeState.RunLimitReached, AutomaticResumeRunState.ResumeLater, true, false)]
    public void Result_EnforcesTerminalStateFlagsAndPreservesExactRuns(
        RepeatedDelayedAutomaticResumeState state,
        AutomaticResumeRunState finalState,
        bool shouldRunAgain,
        bool immediate)
    {
        var first = RunResult(AutomaticResumeRunState.ResumeLater);
        var final = RunResult(finalState);
        var runs = finalState == AutomaticResumeRunState.ResumeLater ? [first] : new[] { first, final };
        var delayCount = runs.Length - 1;
        var result = new RepeatedDelayedAutomaticResumeResult(
            state, runs, delayCount, shouldRunAgain, immediate);

        Assert.Equal(runs, result.Runs);
        Assert.Same(runs[0], result.Runs[0]);
        Assert.Same(runs[^1], result.Runs[^1]);
        Assert.Throws<ArgumentException>(() => new RepeatedDelayedAutomaticResumeResult(
            state, runs, delayCount, !shouldRunAgain, immediate));
        Assert.Throws<ArgumentException>(() => new RepeatedDelayedAutomaticResumeResult(
            state, runs, delayCount, shouldRunAgain, !immediate));
    }

    [Fact]
    public void Result_ExposesReadOnlySnapshotAndRejectsInvalidRunOrdering()
    {
        var first = RunResult(AutomaticResumeRunState.ResumeLater);
        var final = RunResult(AutomaticResumeRunState.Finished);
        var source = new List<AutomaticResumeRunResult> { first, final };
        var result = new RepeatedDelayedAutomaticResumeResult(
            RepeatedDelayedAutomaticResumeState.Finished, source, 1, false, false);
        source.Clear();

        Assert.Equal([first, final], result.Runs);
        Assert.Throws<NotSupportedException>(() =>
            Assert.IsAssignableFrom<IList<AutomaticResumeRunResult>>(result.Runs).Add(first));
        Assert.Throws<ArgumentException>(() => new RepeatedDelayedAutomaticResumeResult(
            RepeatedDelayedAutomaticResumeState.Finished,
            [RunResult(AutomaticResumeRunState.Failed), final],
            1,
            false,
            false));
    }

    [Fact]
    public void Result_DelayedWorkRemainingSupportsValidResumableModel()
    {
        var run = RunResult(AutomaticResumeRunState.ResumeLater);

        var result = new RepeatedDelayedAutomaticResumeResult(
            RepeatedDelayedAutomaticResumeState.DelayedWorkRemaining,
            [run],
            0,
            true,
            false);

        Assert.Same(run, Assert.Single(result.Runs));
    }

    [Theory]
    [InlineData(AutomaticResumeRunState.Finished, RepeatedDelayedAutomaticResumeState.Finished, false, false)]
    [InlineData(AutomaticResumeRunState.Failed, RepeatedDelayedAutomaticResumeState.Failed, false, false)]
    [InlineData(AutomaticResumeRunState.LimitReached, RepeatedDelayedAutomaticResumeState.ImmediateWorkRemaining, true, true)]
    public async Task ExecuteAsync_FirstTerminalRunStopsWithoutDelay(
        AutomaticResumeRunState runState,
        RepeatedDelayedAutomaticResumeState expectedState,
        bool shouldRunAgain,
        bool immediate)
    {
        var run = RunResult(runState);
        var fixture = new Fixture([run]);

        var result = await fixture.Service.ExecuteAsync(Request(5));

        Assert.Equal(expectedState, result.State);
        Assert.Equal(shouldRunAgain, result.ShouldRunAgain);
        Assert.Equal(immediate, result.Immediate);
        Assert.Same(run, Assert.Single(result.Runs));
        Assert.Equal(0, result.DelayCount);
        Assert.Equal(["run"], fixture.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_RepeatedResumeLaterThenFinishedPreservesOrderAndDelegation()
    {
        using var source = new CancellationTokenSource();
        var first = RunResult(AutomaticResumeRunState.ResumeLater);
        var second = RunResult(AutomaticResumeRunState.ResumeLater);
        var third = RunResult(AutomaticResumeRunState.Finished);
        var fixture = new Fixture([first, second, third]);
        var request = Request(5);

        var result = await fixture.Service.ExecuteAsync(request, source.Token);

        Assert.Equal(RepeatedDelayedAutomaticResumeState.Finished, result.State);
        Assert.Equal([first, second, third], result.Runs);
        Assert.Equal(2, result.DelayCount);
        Assert.Equal(["run", "delay", "run", "delay", "run"], fixture.Calls);
        Assert.All(fixture.Orchestrator.Requests, actual => Assert.Same(request.RunRequest, actual));
        Assert.All(fixture.Orchestrator.Tokens, actual => Assert.Equal(source.Token, actual));
        Assert.All(fixture.Delay.Delays, actual => Assert.Equal(request.ResumeDelay, actual));
        Assert.All(fixture.Delay.Tokens, actual => Assert.Equal(source.Token, actual));
        Assert.Equal(1, fixture.Orchestrator.MaximumConcurrentCalls);
    }

    [Theory]
    [InlineData(AutomaticResumeRunState.Finished, RepeatedDelayedAutomaticResumeState.Finished)]
    [InlineData(AutomaticResumeRunState.Failed, RepeatedDelayedAutomaticResumeState.Failed)]
    [InlineData(AutomaticResumeRunState.LimitReached, RepeatedDelayedAutomaticResumeState.ImmediateWorkRemaining)]
    public async Task ExecuteAsync_LaterTerminalStateStopsWithoutFollowingDelay(
        AutomaticResumeRunState finalState,
        RepeatedDelayedAutomaticResumeState expectedState)
    {
        var fixture = new Fixture([
            RunResult(AutomaticResumeRunState.ResumeLater),
            RunResult(finalState),
            RunResult(AutomaticResumeRunState.Finished)]);

        var result = await fixture.Service.ExecuteAsync(Request(5));

        Assert.Equal(expectedState, result.State);
        Assert.Equal(2, fixture.Orchestrator.CallCount);
        Assert.Equal(1, fixture.Delay.CallCount);
        Assert.Equal(["run", "delay", "run"], fixture.Calls);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    public async Task ExecuteAsync_AllResumeLaterStopsExactlyAtRunLimit(int maximumRuns)
    {
        var runs = Enumerable.Range(0, maximumRuns)
            .Select(_ => RunResult(AutomaticResumeRunState.ResumeLater))
            .ToArray();
        var fixture = new Fixture(runs);

        var result = await fixture.Service.ExecuteAsync(Request(maximumRuns));

        Assert.Equal(RepeatedDelayedAutomaticResumeState.RunLimitReached, result.State);
        Assert.True(result.ShouldRunAgain);
        Assert.False(result.Immediate);
        Assert.Equal(maximumRuns, fixture.Orchestrator.CallCount);
        Assert.Equal(maximumRuns - 1, fixture.Delay.CallCount);
        Assert.Equal(maximumRuns - 1, result.DelayCount);
        Assert.Same(runs[^1], result.Runs[^1]);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task ExecuteAsync_RunExceptionPropagatesAndStopsWithoutRetry(int failingCall)
    {
        var expected = new IOException("run failed");
        var fixture = new Fixture([
            RunResult(AutomaticResumeRunState.ResumeLater),
            RunResult(AutomaticResumeRunState.ResumeLater)]);
        fixture.Orchestrator.Exception = expected;
        fixture.Orchestrator.ExceptionCall = failingCall;

        var exception = await Assert.ThrowsAsync<IOException>(() => fixture.Service.ExecuteAsync(Request(5)));

        Assert.Same(expected, exception);
        Assert.Equal(failingCall, fixture.Orchestrator.CallCount);
        Assert.Equal(failingCall - 1, fixture.Delay.CallCount);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task ExecuteAsync_DelayExceptionPropagatesAndPreventsLaterRun(int failingCall)
    {
        var expected = new InvalidOperationException("delay failed");
        var fixture = new Fixture([
            RunResult(AutomaticResumeRunState.ResumeLater),
            RunResult(AutomaticResumeRunState.ResumeLater)]);
        fixture.Delay.Exception = expected;
        fixture.Delay.ExceptionCall = failingCall;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Service.ExecuteAsync(Request(5)));

        Assert.Same(expected, exception);
        Assert.Equal(failingCall, fixture.Delay.CallCount);
        Assert.Equal(failingCall, fixture.Orchestrator.CallCount);
    }

    [Fact]
    public async Task ExecuteAsync_PreCancelledRunPreventsDelay()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        var fixture = new Fixture([]);
        fixture.Orchestrator.HonorCancellation = true;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            fixture.Service.ExecuteAsync(Request(3), source.Token));

        Assert.Equal(["run"], fixture.Calls);
        Assert.Equal(0, fixture.Delay.CallCount);
    }

    [Fact]
    public async Task ExecuteAsync_DelayCancellationPreventsLaterRun()
    {
        using var source = new CancellationTokenSource();
        var fixture = new Fixture([RunResult(AutomaticResumeRunState.ResumeLater)]);
        fixture.Delay.CancelSource = source;
        fixture.Delay.HonorCancellation = true;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            fixture.Service.ExecuteAsync(Request(3), source.Token));

        Assert.Equal(["run", "delay"], fixture.Calls);
        Assert.Equal(1, fixture.Orchestrator.CallCount);
    }

    [Fact]
    public async Task ExecuteAsync_LaterRunCancellationPreventsLaterOperations()
    {
        using var source = new CancellationTokenSource();
        var fixture = new Fixture([
            RunResult(AutomaticResumeRunState.ResumeLater),
            RunResult(AutomaticResumeRunState.ResumeLater)]);
        fixture.Delay.CancelSource = source;
        fixture.Orchestrator.HonorCancellation = true;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            fixture.Service.ExecuteAsync(Request(4), source.Token));

        Assert.Equal(["run", "delay", "run"], fixture.Calls);
        Assert.Equal(2, fixture.Orchestrator.CallCount);
        Assert.Equal(1, fixture.Delay.CallCount);
    }

    [Fact]
    public void Executor_DependsExactlyOnOrchestratorAndDelay()
    {
        var parameters = Assert.Single(typeof(RepeatedDelayedAutomaticResumeExecutor).GetConstructors())
            .GetParameters();

        Assert.Equal(
            [typeof(IAutomaticResumeRunOrchestrator), typeof(IAsyncDelay)],
            parameters.Select(parameter => parameter.ParameterType));
    }

    private static RepeatedDelayedAutomaticResumeRequest Request(int maximumRuns) =>
        new(RunRequest(), TimeSpan.FromMinutes(6), maximumRuns);

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
            state, [batch], [decision], decision.ShouldRunAgain, decision.Immediate);
    }

    private static AutomaticResumeBatchRunResult BatchRun(AutomaticResumeBatchRunState state)
    {
        var stepState = state switch
        {
            AutomaticResumeBatchRunState.Pending => AutomaticResumeBatchStepState.Pending,
            AutomaticResumeBatchRunState.Failed => AutomaticResumeBatchStepState.Failed,
            _ => AutomaticResumeBatchStepState.Completed
        };
        var moreWork = state == AutomaticResumeBatchRunState.LimitReached;
        return new AutomaticResumeBatchRunResult(state, [BatchStep(stepState, moreWork)], moreWork);
    }

    private static AutomaticResumeBatchStepResult BatchStep(AutomaticResumeBatchStepState state, bool moreWork)
    {
        var resumeState = state switch
        {
            AutomaticResumeBatchStepState.Pending => AutomaticPersistedLifecycleResumeState.Pending,
            AutomaticResumeBatchStepState.Failed => AutomaticPersistedLifecycleResumeState.Failed,
            _ => AutomaticPersistedLifecycleResumeState.Completed
        };
        return new AutomaticResumeBatchStepResult(state, PersistedResume(resumeState), moreWork);
    }

    private static AutomaticPersistedLifecycleResumeResult PersistedResume(
        AutomaticPersistedLifecycleResumeState state)
    {
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
        var status = new PullRequestStatusGateResult(31, "head", gateState, []);
        DeveloperLifecycleResumeResult lifecycle;
        if (lifecycleState == DeveloperLifecycleState.Completed)
        {
            lifecycle = new DeveloperLifecycleResumeResult(
                lifecycleState,
                persisted.ResumeContext,
                status,
                new PullRequestGatedMergeResult(
                    status,
                    new PullRequestMergeResult(31, true, "merge", PullRequestMergeMethod.Squash)),
                new PostMergeCleanupResult("repository", "main", "feature/repeated", true, true));
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
        "DEV-0031",
        null,
        new DeveloperLifecycleResumeContext(
            "repository",
            new GitHubRepositoryIdentity("owner", "repository"),
            31,
            "feature/repeated",
            "main",
            "origin"),
        new DateTimeOffset(2026, 8, 15, 12, 0, 0, TimeSpan.Zero));

    private sealed class Fixture
    {
        public Fixture(IReadOnlyList<AutomaticResumeRunResult> results)
        {
            Orchestrator = new FakeOrchestrator(Calls, results);
            Delay = new FakeDelay(Calls);
            Service = new RepeatedDelayedAutomaticResumeExecutor(Orchestrator, Delay);
        }

        public List<string> Calls { get; } = [];
        public FakeOrchestrator Orchestrator { get; }
        public FakeDelay Delay { get; }
        public RepeatedDelayedAutomaticResumeExecutor Service { get; }
    }

    private sealed class FakeOrchestrator(
        IList<string> calls,
        IReadOnlyList<AutomaticResumeRunResult> results) : IAutomaticResumeRunOrchestrator
    {
        private int activeCalls;

        public Exception? Exception { get; set; }
        public int? ExceptionCall { get; set; }
        public bool HonorCancellation { get; set; }
        public int CallCount { get; private set; }
        public int MaximumConcurrentCalls { get; private set; }
        public List<AutomaticResumeRunRequest> Requests { get; } = [];
        public List<CancellationToken> Tokens { get; } = [];

        public async Task<AutomaticResumeRunResult> RunAsync(
            AutomaticResumeRunRequest request,
            CancellationToken cancellationToken = default)
        {
            calls.Add("run");
            CallCount++;
            Requests.Add(request);
            Tokens.Add(cancellationToken);
            activeCalls++;
            MaximumConcurrentCalls = Math.Max(MaximumConcurrentCalls, activeCalls);
            try
            {
                if (HonorCancellation)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                if (ExceptionCall == CallCount)
                {
                    throw Exception!;
                }

                await Task.Yield();
                return results[CallCount - 1];
            }
            finally
            {
                activeCalls--;
            }
        }
    }

    private sealed class FakeDelay(IList<string> calls) : IAsyncDelay
    {
        public Exception? Exception { get; set; }
        public int? ExceptionCall { get; set; }
        public bool HonorCancellation { get; set; }
        public CancellationTokenSource? CancelSource { get; set; }
        public int CallCount { get; private set; }
        public List<TimeSpan> Delays { get; } = [];
        public List<CancellationToken> Tokens { get; } = [];

        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken = default)
        {
            calls.Add("delay");
            CallCount++;
            Delays.Add(delay);
            Tokens.Add(cancellationToken);
            CancelSource?.Cancel();
            if (HonorCancellation)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            return ExceptionCall == CallCount ? Task.FromException(Exception!) : Task.CompletedTask;
        }
    }
}
