using TrailTrainer.Developer.Core;
using TrailTrainer.Developer.Tasks;

namespace TrailTrainer.Developer.Tests;

public sealed class AutomaticResumeWorkerTests
{
    [Fact]
    public void Request_RejectsNullAndPreservesExactExecutionRequest()
    {
        var executionRequest = ExecutionRequest();

        Assert.Throws<ArgumentNullException>(() => new AutomaticResumeWorkerRequest(null!));
        var request = new AutomaticResumeWorkerRequest(executionRequest);

        Assert.Same(executionRequest, request.ExecutionRequest);
    }

    [Fact]
    public void Result_RejectsNullAndPreservesExactExecutionResult()
    {
        var executionResult = ExecutionResult(RepeatedDelayedAutomaticResumeState.Finished);

        Assert.Throws<ArgumentNullException>(() => new AutomaticResumeWorkerResult(null!));
        var result = new AutomaticResumeWorkerResult(executionResult);

        Assert.Same(executionResult, result.ExecutionResult);
    }

    [Theory]
    [InlineData(RepeatedDelayedAutomaticResumeState.Finished)]
    [InlineData(RepeatedDelayedAutomaticResumeState.Failed)]
    [InlineData(RepeatedDelayedAutomaticResumeState.ImmediateWorkRemaining)]
    [InlineData(RepeatedDelayedAutomaticResumeState.RunLimitReached)]
    public async Task RunAsync_DelegatesExactlyOnceAndPreservesOutcomeWithoutInterpretation(
        RepeatedDelayedAutomaticResumeState state)
    {
        using var source = new CancellationTokenSource();
        var executionRequest = ExecutionRequest();
        var executionResult = ExecutionResult(state);
        var executor = new FakeExecutor(executionResult);
        var request = new AutomaticResumeWorkerRequest(executionRequest);

        var result = await new AutomaticResumeWorker(executor).RunAsync(request, source.Token);

        Assert.Equal(1, executor.CallCount);
        Assert.Same(executionRequest, executor.Request);
        Assert.Equal(source.Token, executor.Token);
        Assert.Same(executionResult, result.ExecutionResult);
    }

    [Fact]
    public async Task RunAsync_NullRequestRejectedBeforeExecutor()
    {
        var executor = new FakeExecutor(ExecutionResult(RepeatedDelayedAutomaticResumeState.Finished));

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            new AutomaticResumeWorker(executor).RunAsync(null!));

        Assert.Equal(0, executor.CallCount);
    }

    [Fact]
    public async Task RunAsync_ExecutorExceptionPropagatesUnchangedWithoutRetry()
    {
        var expected = new IOException("execution failed");
        var executor = new FakeExecutor(ExecutionResult(RepeatedDelayedAutomaticResumeState.Finished))
        {
            Exception = expected
        };

        var exception = await Assert.ThrowsAsync<IOException>(() =>
            new AutomaticResumeWorker(executor).RunAsync(new AutomaticResumeWorkerRequest(ExecutionRequest())));

        Assert.Same(expected, exception);
        Assert.Equal(1, executor.CallCount);
    }

    [Fact]
    public async Task RunAsync_PreCancelledExecutorCancellationPropagatesUnchanged()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        var executor = new FakeExecutor(ExecutionResult(RepeatedDelayedAutomaticResumeState.Finished))
        {
            HonorCancellation = true
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new AutomaticResumeWorker(executor).RunAsync(
                new AutomaticResumeWorkerRequest(ExecutionRequest()),
                source.Token));

        Assert.Equal(1, executor.CallCount);
        Assert.Equal(source.Token, executor.Token);
    }

    [Fact]
    public async Task RunAsync_ExecutorCancellationExceptionPropagatesUnchanged()
    {
        var expected = new OperationCanceledException("executor cancelled");
        var executor = new FakeExecutor(ExecutionResult(RepeatedDelayedAutomaticResumeState.Finished))
        {
            Exception = expected
        };

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new AutomaticResumeWorker(executor).RunAsync(
                new AutomaticResumeWorkerRequest(ExecutionRequest())));

        Assert.Same(expected, exception);
        Assert.Equal(1, executor.CallCount);
    }

    [Fact]
    public void Worker_HasExactlyOneDev0031DependencyAndNoStateEnumExists()
    {
        var parameters = Assert.Single(typeof(AutomaticResumeWorker).GetConstructors()).GetParameters();
        var assembly = typeof(AutomaticResumeWorkerRequest).Assembly;

        Assert.Single(parameters);
        Assert.Equal(typeof(IRepeatedDelayedAutomaticResumeExecutor), parameters[0].ParameterType);
        Assert.Null(assembly.GetType("TrailTrainer.Developer.Core.AutomaticResumeWorkerState"));
    }

    private static RepeatedDelayedAutomaticResumeRequest ExecutionRequest() => new(
        RunRequest(),
        TimeSpan.FromMinutes(4),
        3);

    private static AutomaticResumeRunRequest RunRequest() => new(
        new AutomaticResumeBatchRunRequest(
            new AutomaticResumeBatchStepRequest(PullRequestMergeMethod.Squash, "title", "message", true),
            2),
        2);

    private static RepeatedDelayedAutomaticResumeResult ExecutionResult(
        RepeatedDelayedAutomaticResumeState state)
    {
        var runState = state switch
        {
            RepeatedDelayedAutomaticResumeState.Finished => AutomaticResumeRunState.Finished,
            RepeatedDelayedAutomaticResumeState.Failed => AutomaticResumeRunState.Failed,
            RepeatedDelayedAutomaticResumeState.ImmediateWorkRemaining => AutomaticResumeRunState.LimitReached,
            _ => AutomaticResumeRunState.ResumeLater
        };
        var shouldRunAgain = state is RepeatedDelayedAutomaticResumeState.ImmediateWorkRemaining
            or RepeatedDelayedAutomaticResumeState.RunLimitReached;
        var immediate = state == RepeatedDelayedAutomaticResumeState.ImmediateWorkRemaining;
        return new RepeatedDelayedAutomaticResumeResult(
            state,
            [RunResult(runState)],
            0,
            shouldRunAgain,
            immediate);
    }

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

    private static AutomaticResumeBatchStepResult BatchStep(
        AutomaticResumeBatchStepState state,
        bool moreWork)
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
        var status = new PullRequestStatusGateResult(32, "head", gateState, []);
        DeveloperLifecycleResumeResult lifecycle;
        if (lifecycleState == DeveloperLifecycleState.Completed)
        {
            lifecycle = new DeveloperLifecycleResumeResult(
                lifecycleState,
                persisted.ResumeContext,
                status,
                new PullRequestGatedMergeResult(
                    status,
                    new PullRequestMergeResult(32, true, "merge", PullRequestMergeMethod.Squash)),
                new PostMergeCleanupResult("repository", "main", "feature/worker", true, true));
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
        "DEV-0032",
        null,
        new DeveloperLifecycleResumeContext(
            "repository",
            new GitHubRepositoryIdentity("owner", "repository"),
            32,
            "feature/worker",
            "main",
            "origin"),
        new DateTimeOffset(2026, 8, 16, 12, 0, 0, TimeSpan.Zero));

    private sealed class FakeExecutor(
        RepeatedDelayedAutomaticResumeResult result) : IRepeatedDelayedAutomaticResumeExecutor
    {
        public Exception? Exception { get; init; }
        public bool HonorCancellation { get; init; }
        public int CallCount { get; private set; }
        public RepeatedDelayedAutomaticResumeRequest? Request { get; private set; }
        public CancellationToken Token { get; private set; }

        public Task<RepeatedDelayedAutomaticResumeResult> ExecuteAsync(
            RepeatedDelayedAutomaticResumeRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            Request = request;
            Token = cancellationToken;
            if (HonorCancellation)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            return Exception is null
                ? Task.FromResult(result)
                : Task.FromException<RepeatedDelayedAutomaticResumeResult>(Exception);
        }
    }
}
