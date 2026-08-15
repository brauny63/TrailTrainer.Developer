using TrailTrainer.Developer.Core;
using TrailTrainer.Developer.Tasks;

namespace TrailTrainer.Developer.Tests;

public sealed class AutomaticResumeBatchRunnerTests
{
    [Fact]
    public void Request_ValidatesAndPreservesValues()
    {
        var stepRequest = StepRequest();

        Assert.Throws<ArgumentNullException>(() => new AutomaticResumeBatchRunRequest(null!, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AutomaticResumeBatchRunRequest(stepRequest, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AutomaticResumeBatchRunRequest(stepRequest, -1));
        var request = new AutomaticResumeBatchRunRequest(stepRequest, 1);

        Assert.Same(stepRequest, request.StepRequest);
        Assert.Equal(1, request.MaximumSteps);
    }

    [Fact]
    public void Result_RejectsUnsupportedStateAndEmptyOrNullSteps()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AutomaticResumeBatchRunResult(
            (AutomaticResumeBatchRunState)99, [Step(AutomaticResumeBatchStepState.Empty, false)], false));
        Assert.Throws<ArgumentException>(() => new AutomaticResumeBatchRunResult(
            AutomaticResumeBatchRunState.Empty, [], false));
        Assert.Throws<ArgumentException>(() => new AutomaticResumeBatchRunResult(
            AutomaticResumeBatchRunState.Empty, [null!], false));
    }

    [Theory]
    [InlineData(AutomaticResumeBatchRunState.Empty, AutomaticResumeBatchStepState.Empty, false)]
    [InlineData(AutomaticResumeBatchRunState.Completed, AutomaticResumeBatchStepState.Completed, false)]
    [InlineData(AutomaticResumeBatchRunState.Pending, AutomaticResumeBatchStepState.Pending, false)]
    [InlineData(AutomaticResumeBatchRunState.Pending, AutomaticResumeBatchStepState.Pending, true)]
    [InlineData(AutomaticResumeBatchRunState.Failed, AutomaticResumeBatchStepState.Failed, false)]
    [InlineData(AutomaticResumeBatchRunState.Failed, AutomaticResumeBatchStepState.Failed, true)]
    [InlineData(AutomaticResumeBatchRunState.LimitReached, AutomaticResumeBatchStepState.Completed, true)]
    public void Result_EnforcesTerminalStateAndPreservesExactStepIdentity(
        AutomaticResumeBatchRunState runState,
        AutomaticResumeBatchStepState stepState,
        bool moreWork)
    {
        var step = Step(stepState, moreWork);
        var result = new AutomaticResumeBatchRunResult(runState, [step], moreWork);

        Assert.Same(step, Assert.Single(result.Steps));
        Assert.Equal(moreWork, result.MoreWork);
        Assert.Throws<ArgumentException>(() => new AutomaticResumeBatchRunResult(
            runState, [step], !moreWork));
    }

    [Fact]
    public void Result_RejectsInvalidTerminalAndNonTerminalSteps()
    {
        var completedWithWork = Step(AutomaticResumeBatchStepState.Completed, true);

        Assert.Throws<ArgumentException>(() => new AutomaticResumeBatchRunResult(
            AutomaticResumeBatchRunState.Completed, [completedWithWork], true));
        Assert.Throws<ArgumentException>(() => new AutomaticResumeBatchRunResult(
            AutomaticResumeBatchRunState.LimitReached,
            [Step(AutomaticResumeBatchStepState.Pending, true)],
            true));
        Assert.Throws<ArgumentException>(() => new AutomaticResumeBatchRunResult(
            AutomaticResumeBatchRunState.Pending,
            [Step(AutomaticResumeBatchStepState.Failed, true), Step(AutomaticResumeBatchStepState.Pending, true)],
            true));
    }

    [Fact]
    public void Result_PreservesOrderAndExposesReadOnlySnapshot()
    {
        var first = Step(AutomaticResumeBatchStepState.Completed, true);
        var second = Step(AutomaticResumeBatchStepState.Pending, true);
        var source = new List<AutomaticResumeBatchStepResult> { first, second };

        var result = new AutomaticResumeBatchRunResult(
            AutomaticResumeBatchRunState.Pending, source, true);
        source.Clear();

        Assert.Equal(2, result.Steps.Count);
        Assert.Same(first, result.Steps[0]);
        Assert.Same(second, result.Steps[1]);
        var mutableView = Assert.IsAssignableFrom<IList<AutomaticResumeBatchStepResult>>(result.Steps);
        Assert.Throws<NotSupportedException>(() => mutableView.Add(first));
    }

    [Fact]
    public async Task RunAsync_NullRequestRejectedBeforeStep()
    {
        var fake = new FakeBatchStep([]);

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            new AutomaticResumeBatchRunner(fake).RunAsync(null!));

        Assert.Equal(0, fake.CallCount);
    }

    [Fact]
    public async Task RunAsync_FirstEmptyStopsAfterOneAndPreservesExactResult()
    {
        var step = Step(AutomaticResumeBatchStepState.Empty, false);
        var fake = new FakeBatchStep([step]);

        var result = await new AutomaticResumeBatchRunner(fake).RunAsync(Request(5));

        Assert.Equal(AutomaticResumeBatchRunState.Empty, result.State);
        Assert.False(result.MoreWork);
        Assert.Same(step, Assert.Single(result.Steps));
        Assert.Equal(1, fake.CallCount);
    }

    [Fact]
    public async Task RunAsync_CompletedWithoutMoreWorkStopsAsCompleted()
    {
        var step = Step(AutomaticResumeBatchStepState.Completed, false);
        var fake = new FakeBatchStep([step]);

        var result = await new AutomaticResumeBatchRunner(fake).RunAsync(Request(3));

        Assert.Equal(AutomaticResumeBatchRunState.Completed, result.State);
        Assert.False(result.MoreWork);
        Assert.Same(step, Assert.Single(result.Steps));
        Assert.Equal(1, fake.CallCount);
    }

    [Fact]
    public async Task RunAsync_MultipleCompletedStepsStopWhenMoreWorkBecomesFalseInOrder()
    {
        var first = Step(AutomaticResumeBatchStepState.Completed, true);
        var second = Step(AutomaticResumeBatchStepState.Completed, true);
        var third = Step(AutomaticResumeBatchStepState.Completed, false);
        var fake = new FakeBatchStep([first, second, third]);

        var result = await new AutomaticResumeBatchRunner(fake).RunAsync(Request(5));

        Assert.Equal(AutomaticResumeBatchRunState.Completed, result.State);
        Assert.Equal(3, fake.CallCount);
        Assert.Equal([first, second, third], result.Steps);
    }

    [Theory]
    [InlineData(AutomaticResumeBatchStepState.Pending, AutomaticResumeBatchRunState.Pending, false)]
    [InlineData(AutomaticResumeBatchStepState.Pending, AutomaticResumeBatchRunState.Pending, true)]
    [InlineData(AutomaticResumeBatchStepState.Failed, AutomaticResumeBatchRunState.Failed, false)]
    [InlineData(AutomaticResumeBatchStepState.Failed, AutomaticResumeBatchRunState.Failed, true)]
    public async Task RunAsync_PendingOrFailedStopsImmediately(
        AutomaticResumeBatchStepState stepState,
        AutomaticResumeBatchRunState expectedState,
        bool moreWork)
    {
        var terminal = Step(stepState, moreWork);
        var fake = new FakeBatchStep([
            Step(AutomaticResumeBatchStepState.Completed, true),
            terminal,
            Step(AutomaticResumeBatchStepState.Completed, false)]);

        var result = await new AutomaticResumeBatchRunner(fake).RunAsync(Request(5));

        Assert.Equal(expectedState, result.State);
        Assert.Equal(moreWork, result.MoreWork);
        Assert.Equal(2, fake.CallCount);
        Assert.Same(terminal, result.Steps[1]);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public async Task RunAsync_AllCompletedWithMoreWorkStopsExactlyAtLimit(int maximumSteps)
    {
        var returned = Enumerable.Range(0, maximumSteps)
            .Select(_ => Step(AutomaticResumeBatchStepState.Completed, true))
            .ToArray();
        var fake = new FakeBatchStep(returned);

        var result = await new AutomaticResumeBatchRunner(fake).RunAsync(Request(maximumSteps));

        Assert.Equal(AutomaticResumeBatchRunState.LimitReached, result.State);
        Assert.True(result.MoreWork);
        Assert.Equal(maximumSteps, fake.CallCount);
        Assert.Equal(returned, result.Steps);
    }

    [Fact]
    public async Task RunAsync_EveryStepGetsExactRequestAndCancellationTokenSequentially()
    {
        using var source = new CancellationTokenSource();
        var stepRequest = StepRequest();
        var request = new AutomaticResumeBatchRunRequest(stepRequest, 3);
        var fake = new FakeBatchStep([
            Step(AutomaticResumeBatchStepState.Completed, true),
            Step(AutomaticResumeBatchStepState.Completed, true),
            Step(AutomaticResumeBatchStepState.Completed, false)]);

        await new AutomaticResumeBatchRunner(fake).RunAsync(request, source.Token);

        Assert.All(fake.Requests, actual => Assert.Same(stepRequest, actual));
        Assert.All(fake.CancellationTokens, actual => Assert.Equal(source.Token, actual));
        Assert.Equal(1, fake.MaximumConcurrentCalls);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task RunAsync_StepExceptionPropagatesAndPreventsLaterCallsWithoutRetry(int failingCall)
    {
        var expected = new InvalidDataException("step failed");
        var fake = new FakeBatchStep([
            Step(AutomaticResumeBatchStepState.Completed, true),
            Step(AutomaticResumeBatchStepState.Completed, true)])
        {
            ExceptionCall = failingCall,
            Exception = expected
        };

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            new AutomaticResumeBatchRunner(fake).RunAsync(Request(5)));

        Assert.Same(expected, exception);
        Assert.Equal(failingCall, fake.CallCount);
    }

    [Fact]
    public async Task RunAsync_PreCancelledStepCancellationPreventsLaterCalls()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        var fake = new FakeBatchStep([]) { HonorCancellation = true };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new AutomaticResumeBatchRunner(fake).RunAsync(Request(3), source.Token));

        Assert.Equal(1, fake.CallCount);
    }

    [Fact]
    public async Task RunAsync_LaterCancellationPropagatesAndPreventsSubsequentStep()
    {
        using var source = new CancellationTokenSource();
        var fake = new FakeBatchStep([
            Step(AutomaticResumeBatchStepState.Completed, true),
            Step(AutomaticResumeBatchStepState.Completed, true)])
        {
            CancelSourceAfterCall = (source, 1),
            HonorCancellation = true
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new AutomaticResumeBatchRunner(fake).RunAsync(Request(4), source.Token));

        Assert.Equal(2, fake.CallCount);
    }

    private static AutomaticResumeBatchRunRequest Request(int maximumSteps) =>
        new(StepRequest(), maximumSteps);

    private static AutomaticResumeBatchStepRequest StepRequest() =>
        new(PullRequestMergeMethod.Squash, "title", "message", true);

    private static AutomaticResumeBatchStepResult Step(
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
        return new AutomaticResumeBatchStepResult(state, Resume(resumeState), moreWork);
    }

    private static AutomaticPersistedLifecycleResumeResult Resume(
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
        var status = new PullRequestStatusGateResult(27, "head", gateState, []);
        DeveloperLifecycleResumeResult lifecycle;
        if (lifecycleState == DeveloperLifecycleState.Completed)
        {
            var merge = new PullRequestGatedMergeResult(
                status,
                new PullRequestMergeResult(27, true, "merge", PullRequestMergeMethod.Squash));
            lifecycle = new DeveloperLifecycleResumeResult(
                lifecycleState,
                persisted.ResumeContext,
                status,
                merge,
                new PostMergeCleanupResult("repository", "main", "feature/run", true, true));
        }
        else
        {
            lifecycle = new DeveloperLifecycleResumeResult(lifecycleState, persisted.ResumeContext, status);
        }

        var resume = new PersistedDeveloperLifecycleResumeResult(
            persistedState, persisted.TaskId, persisted, lifecycle);
        return new AutomaticPersistedLifecycleResumeResult(state, candidate, resume);
    }

    private static DeveloperLifecyclePersistedState PersistedState() => new(
        "DEV-0027",
        null,
        new DeveloperLifecycleResumeContext(
            "repository",
            new GitHubRepositoryIdentity("owner", "repository"),
            27,
            "feature/run",
            "main",
            "origin"),
        new DateTimeOffset(2026, 8, 15, 12, 0, 0, TimeSpan.Zero));

    private sealed class FakeBatchStep(
        IReadOnlyList<AutomaticResumeBatchStepResult> results) : IAutomaticResumeBatchStep
    {
        private int activeCalls;

        public Exception? Exception { get; init; }
        public int? ExceptionCall { get; init; }
        public bool HonorCancellation { get; init; }
        public (CancellationTokenSource Source, int Call)? CancelSourceAfterCall { get; init; }
        public int CallCount { get; private set; }
        public int MaximumConcurrentCalls { get; private set; }
        public List<AutomaticResumeBatchStepRequest> Requests { get; } = [];
        public List<CancellationToken> CancellationTokens { get; } = [];

        public async Task<AutomaticResumeBatchStepResult> ExecuteAsync(
            AutomaticResumeBatchStepRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            Requests.Add(request);
            CancellationTokens.Add(cancellationToken);
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
                if (CancelSourceAfterCall is { } cancellation && cancellation.Call == CallCount)
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
}
