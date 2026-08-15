using TrailTrainer.Developer.Core;
using TrailTrainer.Developer.Tasks;

namespace TrailTrainer.Developer.Tests;

public sealed class AutomaticResumeRunOrchestratorTests
{
    [Fact]
    public void Request_ValidatesAndPreservesValues()
    {
        var batchRequest = BatchRequest();

        Assert.Throws<ArgumentNullException>(() => new AutomaticResumeRunRequest(null!, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AutomaticResumeRunRequest(batchRequest, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AutomaticResumeRunRequest(batchRequest, -1));
        var request = new AutomaticResumeRunRequest(batchRequest, 1);

        Assert.Same(batchRequest, request.BatchRunRequest);
        Assert.Equal(1, request.MaximumBatchRuns);
    }

    [Fact]
    public void Result_RejectsUnsupportedStateEmptyCollectionsAndCountMismatch()
    {
        var batch = BatchRun(AutomaticResumeBatchRunState.Empty);
        var decision = Decision(batch, AutomaticResumeSchedulingDecisionState.Finished);

        Assert.Throws<ArgumentOutOfRangeException>(() => new AutomaticResumeRunResult(
            (AutomaticResumeRunState)99, [batch], [decision], false, false));
        Assert.Throws<ArgumentException>(() => new AutomaticResumeRunResult(
            AutomaticResumeRunState.Finished, [], [decision], false, false));
        Assert.Throws<ArgumentException>(() => new AutomaticResumeRunResult(
            AutomaticResumeRunState.Finished, [batch], [], false, false));
        Assert.Throws<ArgumentException>(() => new AutomaticResumeRunResult(
            AutomaticResumeRunState.Finished, [batch, batch], [decision], false, false));
    }

    [Fact]
    public void Result_RejectsDecisionBatchIdentityMismatch()
    {
        var batch = BatchRun(AutomaticResumeBatchRunState.Empty);
        var other = BatchRun(AutomaticResumeBatchRunState.Empty);

        Assert.Throws<ArgumentException>(() => new AutomaticResumeRunResult(
            AutomaticResumeRunState.Finished,
            [batch],
            [Decision(other, AutomaticResumeSchedulingDecisionState.Finished)],
            false,
            false));
    }

    [Theory]
    [InlineData(AutomaticResumeRunState.Finished, AutomaticResumeBatchRunState.Empty, AutomaticResumeSchedulingDecisionState.Finished, false, false)]
    [InlineData(AutomaticResumeRunState.ResumeLater, AutomaticResumeBatchRunState.Pending, AutomaticResumeSchedulingDecisionState.ResumeLater, true, false)]
    [InlineData(AutomaticResumeRunState.Failed, AutomaticResumeBatchRunState.Failed, AutomaticResumeSchedulingDecisionState.StopFailed, false, false)]
    [InlineData(AutomaticResumeRunState.LimitReached, AutomaticResumeBatchRunState.LimitReached, AutomaticResumeSchedulingDecisionState.ContinueImmediately, true, true)]
    public void Result_EnforcesFinalMappingFlagsAndExactIdentities(
        AutomaticResumeRunState runState,
        AutomaticResumeBatchRunState batchState,
        AutomaticResumeSchedulingDecisionState decisionState,
        bool shouldRunAgain,
        bool immediate)
    {
        var batch = BatchRun(batchState);
        var decision = Decision(batch, decisionState);
        var result = new AutomaticResumeRunResult(
            runState, [batch], [decision], shouldRunAgain, immediate);

        Assert.Same(batch, Assert.Single(result.BatchRuns));
        Assert.Same(decision, Assert.Single(result.Decisions));
        Assert.Throws<ArgumentException>(() => new AutomaticResumeRunResult(
            runState, [batch], [decision], !shouldRunAgain, immediate));
        Assert.Throws<ArgumentException>(() => new AutomaticResumeRunResult(
            runState, [batch], [decision], shouldRunAgain, !immediate));
    }

    [Fact]
    public void Result_PreservesOrderAsReadOnlySnapshotsAndRequiresContinuationDecisions()
    {
        var first = BatchRun(AutomaticResumeBatchRunState.LimitReached);
        var second = BatchRun(AutomaticResumeBatchRunState.Pending);
        var firstDecision = Decision(first, AutomaticResumeSchedulingDecisionState.ContinueImmediately);
        var secondDecision = Decision(second, AutomaticResumeSchedulingDecisionState.ResumeLater);
        var batches = new List<AutomaticResumeBatchRunResult> { first, second };
        var decisions = new List<AutomaticResumeSchedulingDecision> { firstDecision, secondDecision };

        var result = new AutomaticResumeRunResult(
            AutomaticResumeRunState.ResumeLater, batches, decisions, true, false);
        batches.Clear();
        decisions.Clear();

        Assert.Equal([first, second], result.BatchRuns);
        Assert.Equal([firstDecision, secondDecision], result.Decisions);
        Assert.Throws<NotSupportedException>(() =>
            Assert.IsAssignableFrom<IList<AutomaticResumeBatchRunResult>>(result.BatchRuns).Add(first));
        Assert.Throws<NotSupportedException>(() =>
            Assert.IsAssignableFrom<IList<AutomaticResumeSchedulingDecision>>(result.Decisions).Add(firstDecision));
        Assert.Throws<ArgumentException>(() => new AutomaticResumeRunResult(
            AutomaticResumeRunState.ResumeLater,
            [second, second],
            [secondDecision, secondDecision],
            true,
            false));
    }

    [Fact]
    public async Task RunAsync_NullRequestRejectedBeforeDependencies()
    {
        var fixture = new Fixture([]);

        await Assert.ThrowsAsync<ArgumentNullException>(() => fixture.Service.RunAsync(null!));

        Assert.Empty(fixture.Calls);
    }

    [Theory]
    [InlineData(AutomaticResumeBatchRunState.Empty, AutomaticResumeSchedulingDecisionState.Finished, AutomaticResumeRunState.Finished, false, false)]
    [InlineData(AutomaticResumeBatchRunState.Pending, AutomaticResumeSchedulingDecisionState.ResumeLater, AutomaticResumeRunState.ResumeLater, true, false)]
    [InlineData(AutomaticResumeBatchRunState.Failed, AutomaticResumeSchedulingDecisionState.StopFailed, AutomaticResumeRunState.Failed, false, false)]
    public async Task RunAsync_TerminalFirstDecisionStopsImmediately(
        AutomaticResumeBatchRunState batchState,
        AutomaticResumeSchedulingDecisionState decisionState,
        AutomaticResumeRunState expectedState,
        bool shouldRunAgain,
        bool immediate)
    {
        var batch = BatchRun(batchState);
        var fixture = new Fixture([batch]);
        fixture.DecisionStates.Add(decisionState);

        var result = await fixture.Service.RunAsync(Request(5));

        Assert.Equal(expectedState, result.State);
        Assert.Equal(shouldRunAgain, result.ShouldRunAgain);
        Assert.Equal(immediate, result.Immediate);
        Assert.Equal(["batch", "decision"], fixture.Calls);
        Assert.Same(batch, Assert.Single(result.BatchRuns));
        Assert.Equal(1, fixture.BatchRunner.CallCount);
        Assert.Equal(1, fixture.Decider.CallCount);
    }

    [Theory]
    [InlineData(AutomaticResumeSchedulingDecisionState.Finished, AutomaticResumeRunState.Finished)]
    [InlineData(AutomaticResumeSchedulingDecisionState.ResumeLater, AutomaticResumeRunState.ResumeLater)]
    [InlineData(AutomaticResumeSchedulingDecisionState.StopFailed, AutomaticResumeRunState.Failed)]
    public async Task RunAsync_ImmediateContinuationsStopAtLaterTerminalDecision(
        AutomaticResumeSchedulingDecisionState terminalDecision,
        AutomaticResumeRunState expectedState)
    {
        var terminalBatchState = terminalDecision switch
        {
            AutomaticResumeSchedulingDecisionState.Finished => AutomaticResumeBatchRunState.Completed,
            AutomaticResumeSchedulingDecisionState.ResumeLater => AutomaticResumeBatchRunState.Pending,
            _ => AutomaticResumeBatchRunState.Failed
        };
        var fixture = new Fixture([
            BatchRun(AutomaticResumeBatchRunState.LimitReached),
            BatchRun(AutomaticResumeBatchRunState.LimitReached),
            BatchRun(terminalBatchState)]);
        fixture.DecisionStates.AddRange([
            AutomaticResumeSchedulingDecisionState.ContinueImmediately,
            AutomaticResumeSchedulingDecisionState.ContinueImmediately,
            terminalDecision]);

        var result = await fixture.Service.RunAsync(Request(5));

        Assert.Equal(expectedState, result.State);
        Assert.Equal(3, fixture.BatchRunner.CallCount);
        Assert.Equal(3, fixture.Decider.CallCount);
        Assert.Equal([
            "batch", "decision", "batch", "decision", "batch", "decision"], fixture.Calls);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public async Task RunAsync_AllImmediateDecisionsStopExactlyAtMaximum(int maximumBatchRuns)
    {
        var batches = Enumerable.Range(0, maximumBatchRuns)
            .Select(_ => BatchRun(AutomaticResumeBatchRunState.LimitReached))
            .ToArray();
        var fixture = new Fixture(batches);
        fixture.DecisionStates.AddRange(Enumerable.Repeat(
            AutomaticResumeSchedulingDecisionState.ContinueImmediately,
            maximumBatchRuns));

        var result = await fixture.Service.RunAsync(Request(maximumBatchRuns));

        Assert.Equal(AutomaticResumeRunState.LimitReached, result.State);
        Assert.True(result.ShouldRunAgain);
        Assert.True(result.Immediate);
        Assert.Equal(maximumBatchRuns, fixture.BatchRunner.CallCount);
        Assert.Equal(maximumBatchRuns, fixture.Decider.CallCount);
        Assert.Equal(batches, result.BatchRuns);
    }

    [Fact]
    public async Task RunAsync_DelegatesExactRequestTokenAndBatchIdentitySequentially()
    {
        using var source = new CancellationTokenSource();
        var batchRequest = BatchRequest();
        var request = new AutomaticResumeRunRequest(batchRequest, 2);
        var first = BatchRun(AutomaticResumeBatchRunState.LimitReached);
        var second = BatchRun(AutomaticResumeBatchRunState.Completed);
        var fixture = new Fixture([first, second]);
        fixture.DecisionStates.AddRange([
            AutomaticResumeSchedulingDecisionState.ContinueImmediately,
            AutomaticResumeSchedulingDecisionState.Finished]);

        await fixture.Service.RunAsync(request, source.Token);

        Assert.All(fixture.BatchRunner.Requests, actual => Assert.Same(batchRequest, actual));
        Assert.All(fixture.BatchRunner.Tokens, actual => Assert.Equal(source.Token, actual));
        Assert.Equal([first, second], fixture.Decider.BatchRuns);
        Assert.Equal(1, fixture.BatchRunner.MaximumConcurrentCalls);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task RunAsync_BatchExceptionPropagatesAndPreventsDecisionAndRetry(int failingCall)
    {
        var expected = new IOException("batch failed");
        var fixture = new Fixture([
            BatchRun(AutomaticResumeBatchRunState.LimitReached),
            BatchRun(AutomaticResumeBatchRunState.LimitReached)]);
        fixture.DecisionStates.Add(AutomaticResumeSchedulingDecisionState.ContinueImmediately);
        fixture.BatchRunner.ExceptionCall = failingCall;
        fixture.BatchRunner.Exception = expected;

        var exception = await Assert.ThrowsAsync<IOException>(() => fixture.Service.RunAsync(Request(5)));

        Assert.Same(expected, exception);
        Assert.Equal(failingCall, fixture.BatchRunner.CallCount);
        Assert.Equal(failingCall - 1, fixture.Decider.CallCount);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task RunAsync_DecisionExceptionPropagatesAndPreventsLaterBatch(int failingCall)
    {
        var expected = new InvalidDataException("decision failed");
        var fixture = new Fixture([
            BatchRun(AutomaticResumeBatchRunState.LimitReached),
            BatchRun(AutomaticResumeBatchRunState.LimitReached)]);
        fixture.DecisionStates.Add(AutomaticResumeSchedulingDecisionState.ContinueImmediately);
        fixture.Decider.ExceptionCall = failingCall;
        fixture.Decider.Exception = expected;

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Service.RunAsync(Request(5)));

        Assert.Same(expected, exception);
        Assert.Equal(failingCall, fixture.BatchRunner.CallCount);
        Assert.Equal(failingCall, fixture.Decider.CallCount);
    }

    [Fact]
    public async Task RunAsync_PreCancelledBatchCancellationPreventsDecision()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        var fixture = new Fixture([]);
        fixture.BatchRunner.HonorCancellation = true;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            fixture.Service.RunAsync(Request(3), source.Token));

        Assert.Equal(1, fixture.BatchRunner.CallCount);
        Assert.Equal(0, fixture.Decider.CallCount);
    }

    [Fact]
    public async Task RunAsync_LaterCancellationPreventsLaterDecisionAndBatch()
    {
        using var source = new CancellationTokenSource();
        var fixture = new Fixture([
            BatchRun(AutomaticResumeBatchRunState.LimitReached),
            BatchRun(AutomaticResumeBatchRunState.LimitReached)]);
        fixture.DecisionStates.Add(AutomaticResumeSchedulingDecisionState.ContinueImmediately);
        fixture.BatchRunner.HonorCancellation = true;
        fixture.BatchRunner.CancelAfterCall = (source, 1);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            fixture.Service.RunAsync(Request(4), source.Token));

        Assert.Equal(2, fixture.BatchRunner.CallCount);
        Assert.Equal(1, fixture.Decider.CallCount);
    }

    private static AutomaticResumeRunRequest Request(int maximumBatchRuns) =>
        new(BatchRequest(), maximumBatchRuns);

    private static AutomaticResumeBatchRunRequest BatchRequest() =>
        new(new AutomaticResumeBatchStepRequest(PullRequestMergeMethod.Squash, "title", "message", true), 2);

    private static AutomaticResumeSchedulingDecision Decision(
        AutomaticResumeBatchRunResult batchRun,
        AutomaticResumeSchedulingDecisionState state) => state switch
        {
            AutomaticResumeSchedulingDecisionState.Finished => new(state, batchRun, false, false),
            AutomaticResumeSchedulingDecisionState.ResumeLater => new(state, batchRun, true, false),
            AutomaticResumeSchedulingDecisionState.StopFailed => new(state, batchRun, false, false),
            _ => new(state, batchRun, true, true)
        };

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
        return new AutomaticResumeBatchRunResult(state, [Step(stepState, moreWork)], moreWork);
    }

    private static AutomaticResumeBatchStepResult Step(AutomaticResumeBatchStepState state, bool moreWork)
    {
        var resumeState = state switch
        {
            AutomaticResumeBatchStepState.Empty => AutomaticPersistedLifecycleResumeState.NotFound,
            AutomaticResumeBatchStepState.Pending => AutomaticPersistedLifecycleResumeState.Pending,
            AutomaticResumeBatchStepState.Failed => AutomaticPersistedLifecycleResumeState.Failed,
            _ => AutomaticPersistedLifecycleResumeState.Completed
        };
        return new AutomaticResumeBatchStepResult(state, Resume(resumeState), moreWork);
    }

    private static AutomaticPersistedLifecycleResumeResult Resume(AutomaticPersistedLifecycleResumeState state)
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
        var persistedResumeState = state switch
        {
            AutomaticPersistedLifecycleResumeState.Pending => PersistedDeveloperLifecycleResumeState.Pending,
            AutomaticPersistedLifecycleResumeState.Failed => PersistedDeveloperLifecycleResumeState.Failed,
            _ => PersistedDeveloperLifecycleResumeState.Completed
        };
        var lifecycleState = persistedResumeState switch
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
        var status = new PullRequestStatusGateResult(29, "head", gateState, []);
        DeveloperLifecycleResumeResult lifecycle;
        if (lifecycleState == DeveloperLifecycleState.Completed)
        {
            lifecycle = new DeveloperLifecycleResumeResult(
                lifecycleState,
                persisted.ResumeContext,
                status,
                new PullRequestGatedMergeResult(
                    status,
                    new PullRequestMergeResult(29, true, "merge", PullRequestMergeMethod.Squash)),
                new PostMergeCleanupResult("repository", "main", "feature/orchestration", true, true));
        }
        else
        {
            lifecycle = new DeveloperLifecycleResumeResult(lifecycleState, persisted.ResumeContext, status);
        }

        return new AutomaticPersistedLifecycleResumeResult(
            state,
            candidate,
            new PersistedDeveloperLifecycleResumeResult(
                persistedResumeState, persisted.TaskId, persisted, lifecycle));
    }

    private static DeveloperLifecyclePersistedState PersistedState() => new(
        "DEV-0029",
        null,
        new DeveloperLifecycleResumeContext(
            "repository",
            new GitHubRepositoryIdentity("owner", "repository"),
            29,
            "feature/orchestration",
            "main",
            "origin"),
        new DateTimeOffset(2026, 8, 15, 12, 0, 0, TimeSpan.Zero));

    private sealed class Fixture
    {
        public Fixture(IReadOnlyList<AutomaticResumeBatchRunResult> results)
        {
            BatchRunner = new FakeBatchRunner(Calls, results);
            Decider = new FakeDecision(Calls, DecisionStates);
            Service = new AutomaticResumeRunOrchestrator(BatchRunner, Decider);
        }

        public List<string> Calls { get; } = [];
        public List<AutomaticResumeSchedulingDecisionState> DecisionStates { get; } = [];
        public FakeBatchRunner BatchRunner { get; }
        public FakeDecision Decider { get; }
        public AutomaticResumeRunOrchestrator Service { get; }
    }

    private sealed class FakeBatchRunner : IAutomaticResumeBatchRunner
    {
        private readonly IList<string> calls;
        private readonly IReadOnlyList<AutomaticResumeBatchRunResult> results;
        private int activeCalls;

        public FakeBatchRunner(IList<string> Calls, IReadOnlyList<AutomaticResumeBatchRunResult> Results)
        {
            calls = Calls;
            results = Results;
        }

        public Exception? Exception { get; set; }
        public int? ExceptionCall { get; set; }
        public bool HonorCancellation { get; set; }
        public (CancellationTokenSource Source, int Call)? CancelAfterCall { get; set; }
        public int CallCount { get; private set; }
        public int MaximumConcurrentCalls { get; private set; }
        public List<AutomaticResumeBatchRunRequest> Requests { get; } = [];
        public List<CancellationToken> Tokens { get; } = [];

        public async Task<AutomaticResumeBatchRunResult> RunAsync(
            AutomaticResumeBatchRunRequest request,
            CancellationToken cancellationToken = default)
        {
            calls.Add("batch");
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
                var result = results[CallCount - 1];
                if (CancelAfterCall is { } cancellation && cancellation.Call == CallCount)
                {
                    cancellation.Source.Cancel();
                }

                return result;
            }
            finally
            {
                activeCalls--;
            }
        }
    }

    private sealed class FakeDecision : IAutomaticResumeSchedulingDecision
    {
        private readonly IList<string> calls;
        private readonly IList<AutomaticResumeSchedulingDecisionState> states;

        public FakeDecision(IList<string> Calls, IList<AutomaticResumeSchedulingDecisionState> States)
        {
            calls = Calls;
            states = States;
        }

        public Exception? Exception { get; set; }
        public int? ExceptionCall { get; set; }
        public int CallCount { get; private set; }
        public List<AutomaticResumeBatchRunResult> BatchRuns { get; } = [];

        public AutomaticResumeSchedulingDecision Decide(AutomaticResumeBatchRunResult batchRun)
        {
            calls.Add("decision");
            CallCount++;
            BatchRuns.Add(batchRun);
            if (ExceptionCall == CallCount)
            {
                throw Exception!;
            }

            return Decision(batchRun, states[CallCount - 1]);
        }
    }
}
