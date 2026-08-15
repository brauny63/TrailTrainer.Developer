using System.Reflection;
using System.Runtime.CompilerServices;
using TrailTrainer.Developer.Core;
using TrailTrainer.Developer.Tasks;

namespace TrailTrainer.Developer.Tests;

public sealed class AutomaticResumeSchedulingDecisionTests
{
    [Fact]
    public void Decision_RejectsNullBatchRunAndUnsupportedDecisionState()
    {
        Assert.Throws<ArgumentNullException>(() => new AutomaticResumeSchedulingDecision(
            AutomaticResumeSchedulingDecisionState.Finished, null!, false, false));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AutomaticResumeSchedulingDecision(
            (AutomaticResumeSchedulingDecisionState)99,
            BatchRun(AutomaticResumeBatchRunState.Empty),
            false,
            false));
    }

    [Theory]
    [InlineData(AutomaticResumeSchedulingDecisionState.Finished, AutomaticResumeBatchRunState.Empty, false, false)]
    [InlineData(AutomaticResumeSchedulingDecisionState.Finished, AutomaticResumeBatchRunState.Completed, false, false)]
    [InlineData(AutomaticResumeSchedulingDecisionState.ContinueImmediately, AutomaticResumeBatchRunState.LimitReached, true, true)]
    [InlineData(AutomaticResumeSchedulingDecisionState.ResumeLater, AutomaticResumeBatchRunState.Pending, true, false)]
    [InlineData(AutomaticResumeSchedulingDecisionState.StopFailed, AutomaticResumeBatchRunState.Failed, false, false)]
    public void Decision_EnforcesMappingAndFlagsAndPreservesExactBatchRun(
        AutomaticResumeSchedulingDecisionState decisionState,
        AutomaticResumeBatchRunState batchState,
        bool shouldRunAgain,
        bool immediate)
    {
        var batchRun = BatchRun(batchState);
        var decision = new AutomaticResumeSchedulingDecision(
            decisionState, batchRun, shouldRunAgain, immediate);

        Assert.Same(batchRun, decision.BatchRun);
        Assert.Equal(decisionState, decision.State);
        Assert.Equal(shouldRunAgain, decision.ShouldRunAgain);
        Assert.Equal(immediate, decision.Immediate);
        Assert.Throws<ArgumentException>(() => new AutomaticResumeSchedulingDecision(
            decisionState, batchRun, !shouldRunAgain, immediate));
        Assert.Throws<ArgumentException>(() => new AutomaticResumeSchedulingDecision(
            decisionState, batchRun, shouldRunAgain, !immediate));
    }

    [Fact]
    public void Decision_RejectsDecisionThatDoesNotMatchBatchRunState()
    {
        Assert.Throws<ArgumentException>(() => new AutomaticResumeSchedulingDecision(
            AutomaticResumeSchedulingDecisionState.StopFailed,
            BatchRun(AutomaticResumeBatchRunState.Pending),
            false,
            false));
    }

    [Theory]
    [InlineData(AutomaticResumeBatchRunState.Empty, AutomaticResumeSchedulingDecisionState.Finished, false, false)]
    [InlineData(AutomaticResumeBatchRunState.Completed, AutomaticResumeSchedulingDecisionState.Finished, false, false)]
    [InlineData(AutomaticResumeBatchRunState.Pending, AutomaticResumeSchedulingDecisionState.ResumeLater, true, false)]
    [InlineData(AutomaticResumeBatchRunState.Failed, AutomaticResumeSchedulingDecisionState.StopFailed, false, false)]
    [InlineData(AutomaticResumeBatchRunState.LimitReached, AutomaticResumeSchedulingDecisionState.ContinueImmediately, true, true)]
    public void Decide_MapsBatchStateAndPreservesExactIdentity(
        AutomaticResumeBatchRunState batchState,
        AutomaticResumeSchedulingDecisionState expectedState,
        bool expectedShouldRunAgain,
        bool expectedImmediate)
    {
        var batchRun = BatchRun(batchState);

        var decision = new AutomaticResumeSchedulingDecisionService().Decide(batchRun);

        Assert.Equal(expectedState, decision.State);
        Assert.Equal(expectedShouldRunAgain, decision.ShouldRunAgain);
        Assert.Equal(expectedImmediate, decision.Immediate);
        Assert.Same(batchRun, decision.BatchRun);
    }

    [Fact]
    public void Decide_NullInputRejected()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new AutomaticResumeSchedulingDecisionService().Decide(null!));
    }

    [Fact]
    public void Decide_UnsupportedBatchStateRejected()
    {
        var invalid = (AutomaticResumeBatchRunResult)RuntimeHelpers.GetUninitializedObject(
            typeof(AutomaticResumeBatchRunResult));
        typeof(AutomaticResumeBatchRunResult)
            .GetField("<State>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(invalid, (AutomaticResumeBatchRunState)99);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new AutomaticResumeSchedulingDecisionService().Decide(invalid));
    }

    [Fact]
    public void Service_HasNoConstructorDependenciesAndApiIsSynchronous()
    {
        var constructors = typeof(AutomaticResumeSchedulingDecisionService).GetConstructors();
        var method = typeof(IAutomaticResumeSchedulingDecision).GetMethod(
            nameof(IAutomaticResumeSchedulingDecision.Decide));

        Assert.Single(constructors);
        Assert.Empty(constructors[0].GetParameters());
        Assert.Equal(typeof(AutomaticResumeSchedulingDecision), method!.ReturnType);
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
        return new AutomaticResumeBatchRunResult(state, [Step(stepState, moreWork)], moreWork);
    }

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
        var status = new PullRequestStatusGateResult(28, "head", gateState, []);
        DeveloperLifecycleResumeResult lifecycle;
        if (lifecycleState == DeveloperLifecycleState.Completed)
        {
            lifecycle = new DeveloperLifecycleResumeResult(
                lifecycleState,
                persisted.ResumeContext,
                status,
                new PullRequestGatedMergeResult(
                    status,
                    new PullRequestMergeResult(28, true, "merge", PullRequestMergeMethod.Squash)),
                new PostMergeCleanupResult("repository", "main", "feature/decision", true, true));
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
        "DEV-0028",
        null,
        new DeveloperLifecycleResumeContext(
            "repository",
            new GitHubRepositoryIdentity("owner", "repository"),
            28,
            "feature/decision",
            "main",
            "origin"),
        new DateTimeOffset(2026, 8, 15, 12, 0, 0, TimeSpan.Zero));
}
